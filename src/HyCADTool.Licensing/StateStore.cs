using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Licensing
{
    /// <summary>加密的反时钟回拨 / 可扩展换机计数。密钥派生自本机 <see cref="MachineId.GetMachineCode"/>.</summary>
    public sealed class StateStore
    {
        private const string KdfSalt = "HYCAD-STATE-V1";

        public static StateStore Instance { get; } = new StateStore();

        private StateStore() { }

        public bool ClockRollBackLocked { get; private set; }

        /// <summary>在许可校验与启动后调用，更新高水位时间。</summary>
        public void OnStartupUtc(DateTime nowUtc, string localMachineCode)
        {
            try
            {
                Directory.CreateDirectory(LicensePaths.ProgramDataHyCAD);
                if (!File.Exists(LicensePaths.StateFile))
                {
                    WriteFile(nowUtc, nowUtc, 0, localMachineCode);
                    ClockRollBackLocked = false;
                    return;
                }
                if (!TryReadFile(localMachineCode, out var high, out var last, out var mig))
                {
                    // 损坏/换机后 machine 变：可视为新状态或锁定由上层决定
                    WriteFile(nowUtc, nowUtc, 0, localMachineCode);
                    ClockRollBackLocked = false;
                    return;
                }
                if (nowUtc < high.AddHours(-24))
                {
                    ClockRollBackLocked = true;
                    return;
                }
                if (nowUtc < last.AddHours(-24))
                {
                    ClockRollBackLocked = true;
                    return;
                }
                if (nowUtc > high) high = nowUtc;
                last = nowUtc;
                WriteFile(last, high, mig, localMachineCode);
                ClockRollBackLocked = false;
            }
            catch
            {
                // 状态失败不阻启动；仅失去防回拨
                ClockRollBackLocked = false;
            }
        }

        private void WriteFile(DateTime lastUtc, DateTime highUtc, int migrationCount, string machineCode)
        {
            var jo = new JObject
            {
                ["l"] = lastUtc.ToString("O"),
                ["h"] = highUtc.ToString("O"),
                ["m"] = migrationCount
            };
            var plain = jo.ToString(Formatting.None);
            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var key = DeriveKey(machineCode);
            var iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(iv);
            byte[] enc;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                using (var encr = aes.CreateEncryptor())
                    enc = encr.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
            var outB = new byte[iv.Length + enc.Length];
            Buffer.BlockCopy(iv, 0, outB, 0, iv.Length);
            Buffer.BlockCopy(enc, 0, outB, iv.Length, enc.Length);
            File.WriteAllBytes(LicensePaths.StateFile, outB);
        }

        private bool TryReadFile(string machineCode, out DateTime high, out DateTime last, out int mig)
        {
            high = last = default;
            mig = 0;
            var all = File.ReadAllBytes(LicensePaths.StateFile);
            if (all.Length < 32) return false;
            var iv = new byte[16];
            Buffer.BlockCopy(all, 0, iv, 0, 16);
            var enc = new byte[all.Length - 16];
            Buffer.BlockCopy(all, 16, enc, 0, enc.Length);
            var key = DeriveKey(machineCode);
            byte[] plain;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                using (var d = aes.CreateDecryptor())
                    plain = d.TransformFinalBlock(enc, 0, enc.Length);
            }
            var s = Encoding.UTF8.GetString(plain);
            var j = JObject.Parse(s);
            last = j["l"].ToObject<DateTime>();
            high = j["h"].ToObject<DateTime>();
            mig = (int)j["m"];
            return true;
        }

        private static byte[] DeriveKey(string machineCode)
        {
            var h = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes((machineCode ?? "") + KdfSalt));
            var k = new byte[32];
            Buffer.BlockCopy(h, 0, k, 0, 32);
            return k;
        }
    }
}
