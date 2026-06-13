using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Licensing
{
    public sealed class LicenseService
    {
        private static readonly TimeSpan RefreshMinInterval = TimeSpan.FromMinutes(5);

        public static LicenseService Instance { get; } = new LicenseService();

        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private DateTime _licenseFileMtimeUtc = DateTime.MinValue;
        private DateTime _stateFileMtimeUtc = DateTime.MinValue;

        private LicenseService() { }

        public LicenseStatus LastStatus { get; private set; } = new LicenseStatus();

        public void Refresh() => Refresh(false);

        public void Refresh(bool force)
        {
            var licenseMtime = GetFileMtimeUtc(LicensePaths.LicenseFile);
            var stateMtime = GetFileMtimeUtc(LicensePaths.StateFile);
            if (!force
                && _lastRefreshUtc != DateTime.MinValue
                && DateTime.UtcNow - _lastRefreshUtc < RefreshMinInterval
                && licenseMtime == _licenseFileMtimeUtc
                && stateMtime == _stateFileMtimeUtc)
            {
                return;
            }

            _licenseFileMtimeUtc = licenseMtime;
            _stateFileMtimeUtc = stateMtime;
            _lastRefreshUtc = DateTime.UtcNow;

            var m = MachineId.GetMachineCode();
            StateStore.Instance.OnStartupUtc(DateTime.UtcNow, m);

            var status = BuildStatusFromLicenseFile(m);
            if (StateStore.Instance.ClockRollBackLocked)
                ApplyClockRollBackLock(status);
            LastStatus = status;
        }

        public LicenseProductTier GetEffectiveProductTier()
        {
            Refresh();
            return LastStatus.Tier;
        }

        public static string NormalizeMc(string c) => MachineId.NormalizeMc(c);

        public static bool IsMachineCodeMatch(string licenseMc, string localMc)
        {
            return string.Equals(NormalizeMc(licenseMc), NormalizeMc(localMc), StringComparison.Ordinal);
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

        /// <summary>验签 + 机器码匹配后写入 ProgramData。</summary>
        public static bool TryImportLicenseText(string input, out string error)
        {
            error = null;
            if (!LicenseCodeCodec.TryDecodeToLicenseJson(input, out var jsonText, out error))
                return false;
            if (!TryVerifyDocument(jsonText, out var dto, out error))
                return false;
            if (!IsMachineCodeMatch(dto.MachineCode, MachineId.GetMachineCode()))
            {
                error = "license 与本机机器码不匹配。";
                return false;
            }
            try
            {
                Directory.CreateDirectory(LicensePaths.ProgramDataHyCAD);
                File.WriteAllText(LicensePaths.LicenseFile, jsonText.Trim(), new System.Text.UTF8Encoding(false));
                Instance.Refresh(true);
                return true;
            }
            catch (Exception ex)
            {
                error = "写入 license 失败：" + ex.Message;
                return false;
            }
        }

        public static bool TryImportToProgramData(string sourceFile, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
            {
                error = "文件不存在";
                return false;
            }
            var t = File.ReadAllText(sourceFile);
            return TryImportLicenseText(t, out error);
        }

        public static string StateRecoveryHint =>
            "请删除 " + LicensePaths.StateFile + " 后完全退出并重启 AutoCAD。";

        private static LicenseStatus BuildStatusFromLicenseFile(string machineCode)
        {
            if (!File.Exists(LicensePaths.LicenseFile))
            {
                return new LicenseStatus
                {
                    Ok = true,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = null
                };
            }

            string err;
            LicenseDocumentDto dto;
            try
            {
                var text = File.ReadAllText(LicensePaths.LicenseFile);
                if (!TryVerifyDocument(text, out dto, out err))
                {
                    return new LicenseStatus
                    {
                        Ok = false,
                        Tier = LicenseProductTier.Freemium,
                        ErrorMessage = err
                    };
                }
            }
            catch (Exception ex)
            {
                return new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = ex.GetType().Name + ": " + ex.Message
                };
            }

            if (!IsMachineCodeMatch(dto.MachineCode, machineCode))
            {
                return new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = "license.lic 与本机机器码不匹配。"
                };
            }

            var tier = LicenseEditionHelper.TryParseTier(dto.Edition);
            if (tier == LicenseProductTier.Freemium)
            {
                return new LicenseStatus
                {
                    Ok = false,
                    Tier = LicenseProductTier.Freemium,
                    ErrorMessage = "无法识别的 license edition。"
                };
            }

            if (!dto.Perpetual)
            {
                var now = DateTime.UtcNow;
                if (now > dto.ExpiresAt)
                {
                    return new LicenseStatus
                    {
                        Ok = false,
                        Tier = LicenseProductTier.Freemium,
                        ErrorMessage = "License 已过期（截止 " + dto.ExpiresAt.ToString("u") + "）。"
                    };
                }
            }

            return new LicenseStatus
            {
                Ok = true,
                Tier = tier,
                RecognizedTier = tier,
                ErrorMessage = null,
                Document = dto,
                ExpiresAt = dto.ExpiresAt,
                Perpetual = dto.Perpetual,
                MachineCode = machineCode
            };
        }

        private static void ApplyClockRollBackLock(LicenseStatus status)
        {
            status.ClockRollBackLocked = true;
            if (status.Ok && status.Tier > LicenseProductTier.Freemium)
            {
                status.RecognizedTier = status.Tier;
                status.ErrorMessage =
                    "授权文件有效（" + status.Tier + "），但防回拨状态异常。\n" + StateRecoveryHint;
            }
            else if (!status.Ok && !string.IsNullOrWhiteSpace(status.ErrorMessage))
            {
                status.ErrorMessage += "\n\n另：state.bin 异常，可尝试：" + StateRecoveryHint;
            }
            else
            {
                status.ErrorMessage =
                    "系统时间曾异常回拨，或 state.bin 损坏。\n" + StateRecoveryHint;
            }
            status.Ok = false;
            status.Tier = LicenseProductTier.Freemium;
        }

        private static DateTime GetFileMtimeUtc(string path)
        {
            try
            {
                if (File.Exists(path))
                    return File.GetLastWriteTimeUtc(path);
            }
            catch
            {
            }
            return DateTime.MinValue;
        }
    }

    public sealed class LicenseStatus
    {
        public bool Ok { get; set; }
        public LicenseProductTier Tier { get; set; } = LicenseProductTier.Freemium;
        /// <summary>license 文件解析出的档位（state 锁定时仍保留，供激活窗提示）。</summary>
        public LicenseProductTier RecognizedTier { get; set; } = LicenseProductTier.Freemium;
        public bool ClockRollBackLocked { get; set; }
        public string ErrorMessage { get; set; }
        public LicenseDocumentDto Document { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool Perpetual { get; set; }
        public string MachineCode { get; set; }
    }
}
