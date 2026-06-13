using System;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace HyCADTool.Licensing
{
    /// <summary>本机稳定指纹 → 机器码（与签发 license 绑定）。</summary>
    public static class MachineId
    {
        private const string Salt = "HYCAD-V1-SALT";
        private static string _cachedMachineCode;

        public static string GetMachineCode()
        {
            if (_cachedMachineCode != null) return _cachedMachineCode;

            var raw = new StringBuilder(256);
            try { raw.Append(Wmi("Win32_Processor", "ProcessorId")); } catch { raw.Append("p?"); }
            raw.Append('|');
            try { raw.Append(Wmi("Win32_BaseBoard", "SerialNumber")); } catch { raw.Append("b?"); }
            raw.Append('|');
            try { raw.Append(GetSystemDriveVolumeSerial()); } catch { raw.Append("v?"); }
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw.ToString() + Salt));
                _cachedMachineCode = HycadBase32.Encode20Bytes(hash);
            }
            return _cachedMachineCode;
        }

        /// <summary>去分隔符/空格并大写，供比较与格式校验。</summary>
        public static string NormalizeMc(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return "";
            return c.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
        }

        public static string NormalizeMachineCodeFromUser(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var t = NormalizeMc(s);
            if (t.Length != 32) return s.Trim();
            return string.Join("-", Enumerable.Range(0, 8).Select(i => t.Substring(i * 4, 4)));
        }

        public static bool IsValidMachineCodeFormat(string s)
        {
            return HycadBase32.IsValidNormalizedCode(NormalizeMc(s));
        }

        private static string Wmi(string wmiClass, string prop)
        {
            var q = $"SELECT {prop} FROM {wmiClass}";
            using (var s = new ManagementObjectSearcher(new ObjectQuery(q)))
            using (var e = s.Get())
            {
                foreach (ManagementObject o in e)
                {
                    var v = o[prop]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(v) && v != "To be filled by O.E.M." && v != "Default string")
                        return v.Trim();
                }
            }
            return "?";
        }

        private static string GetSystemDriveVolumeSerial()
        {
            var sys = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(sys) || sys.Length < 2) return "?";
            var root = char.ToUpper(sys[0]) + ":\\";
            var q = $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE Name='{char.ToUpper(sys[0])}:'";
            using (var s = new ManagementObjectSearcher(new ObjectQuery(q)))
            using (var e = s.Get())
            {
                foreach (ManagementObject o in e)
                {
                    var v = o["VolumeSerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(v)) return v.Trim();
                }
            }
            return root;
        }
    }
}
