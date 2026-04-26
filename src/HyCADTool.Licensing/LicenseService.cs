using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Licensing
{
    public sealed class LicenseService
    {
        public static LicenseService Instance { get; } = new LicenseService();

        private LicenseService() { }

        public LicenseStatus LastStatus { get; private set; } = new LicenseStatus();

        public void Refresh()
        {
            var m = MachineId.GetMachineCode();
            StateStore.Instance.OnStartupUtc(DateTime.UtcNow, m);
            if (StateStore.Instance.ClockRollBackLocked)
            {
                LastStatus = new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = "系统时间曾异常回拨。请校时后联系销售重新签发，或继续免费版。"
                };
                return;
            }

            if (!File.Exists(LicensePaths.LicenseFile))
            {
                LastStatus = new LicenseStatus
                {
                    Ok = true,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = null
                };
                return;
            }

            string err;
            LicenseDocumentDto dto;
            try
            {
                var text = File.ReadAllText(LicensePaths.LicenseFile);
                if (!TryVerifyDocument(text, out dto, out err))
                {
                    LastStatus = new LicenseStatus
                    {
                        Ok = false,
                        Tier = LicenseProductTier.Freemium,
                        ErrorMessage = err
                    };
                    return;
                }
            }
            catch (Exception ex)
            {
                LastStatus = new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = ex.GetType().Name + ": " + ex.Message
                };
                return;
            }

            if (!string.Equals(
                    NormalizeMc(dto.MachineCode),
                    NormalizeMc(m),
                    StringComparison.OrdinalIgnoreCase))
            {
                LastStatus = new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = "license.lic 与本机机器码不匹配。"
                };
                return;
            }

            var tier = LicenseEditionHelper.TryParseTier(dto.Edition);
            if (tier == LicenseProductTier.Freemium)
            {
                LastStatus = new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = "无法识别的 license edition。"
                };
                return;
            }

            if (!dto.Perpetual)
            {
                var now = DateTime.UtcNow;
                if (now > dto.ExpiresAt)
                {
                    LastStatus = new LicenseStatus
                    {
                        Ok = false,
                        Tier = LicenseProductTier.Freemium,
                        ErrorMessage = "License 已过期（截止 " + dto.ExpiresAt.ToString("u") + "）。"
                    };
                    return;
                }
            }

            LastStatus = new LicenseStatus
            {
                Ok = true,
                Tier = tier,
                ErrorMessage = null,
                Document = dto,
                ExpiresAt = dto.ExpiresAt,
                Perpetual = dto.Perpetual,
                MachineCode = m
            };
        }

        public LicenseProductTier GetEffectiveProductTier()
        {
            if (LastStatus == null) Refresh();
            return LastStatus.Tier;
        }

        public static string NormalizeMc(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return "";
            return c.Trim().Replace("-", "").Replace(" ", "");
        }

        public static bool TryVerifyDocument(string jsonText, out LicenseDocumentDto dto, out string error)
        {
            dto = null;
            error = null;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                error = "空内容";
                return false;
            }
            JObject j;
            try
            {
                j = JObject.Parse(jsonText);
            }
            catch (Exception ex)
            {
                error = "JSON: " + ex.Message;
                return false;
            }
            if (j["signature"] == null)
            {
                error = "无 signature 字段";
                return false;
            }
            var sig = j["signature"]?.ToString();
            var payload = LicenseCanonicalizer.GetUtf8SignPayloadFromJObject(j);
            if (!RsaLicenseVerifier.VerifyDataUtf8(EmbeddedPublicKey.RsaKeyXml, payload, sig))
            {
                error = "签名校验失败或公钥不匹配";
                return false;
            }
            try
            {
                dto = JsonConvert.DeserializeObject<LicenseDocumentDto>(jsonText);
            }
            catch (Exception ex)
            {
                error = "反序列化: " + ex.Message;
                return false;
            }
            if (dto == null)
            {
                error = "空 license";
                return false;
            }
            return true;
        }

        public static bool TryImportToProgramData(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile)) return false;
            var t = File.ReadAllText(sourceFile);
            if (!TryVerifyDocument(t, out _, out _)) return false;
            Directory.CreateDirectory(LicensePaths.ProgramDataHyCAD);
            File.Copy(sourceFile, LicensePaths.LicenseFile, true);
            return true;
        }
    }

    public sealed class LicenseStatus
    {
        public bool Ok { get; set; }
        public LicenseProductTier Tier { get; set; } = LicenseProductTier.Freemium;
        public string ErrorMessage { get; set; }
        public LicenseDocumentDto Document { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool Perpetual { get; set; }
        public string MachineCode { get; set; }
    }
}
