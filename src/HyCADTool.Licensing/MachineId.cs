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

        public static string GetMachineCode()
        {
            var raw = new StringBuilder(256);
            try { raw.Append(Wmi("Win32_Processor", "ProcessorId")); } catch { raw.Append("p?"); }
            raw.Append('|');
            try { raw.Append(Wmi("Win32_BaseBoard", "SerialNumber")); } catch { raw.Append("b?"); }
            raw.Append('|');
            try { raw.Append(GetSystemDriveVolumeSerial()); } catch { raw.Append("v?"); }
            var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(raw.ToString() + Salt));
            return HycadBase32.Encode20Bytes(hash);
        }

        public static string NormalizeMachineCodeFromUser(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var t = s.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
            if (t.Length != 32) return s.Trim();
            // 加回 8 组
            return string.Join("-", Enumerable.Range(0, 8).Select(i => t.Substring(i * 4, 4)));
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
