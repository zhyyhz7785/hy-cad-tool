using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace HyCADTool.Licensing
{
    /// <summary>HYC1. 授权码：GZip(license JSON UTF-8) → Base64Url，前缀 HYC1.</summary>
    public static class LicenseCodeCodec
    {
        public const string Prefix = "HYC1.";
        private const int MaxDecompressedBytes = 256 * 1024;
        private const int MaxCompressedBytes = 64 * 1024;
        private const int MaxRawJsonChars = MaxDecompressedBytes;

        public static string Encode(string licenseJsonUtf8)
        {
            if (string.IsNullOrWhiteSpace(licenseJsonUtf8))
                throw new ArgumentException("license JSON 为空", nameof(licenseJsonUtf8));
            if (licenseJsonUtf8.Length > MaxRawJsonChars)
                throw new ArgumentException("license JSON 过长（上限 " + MaxRawJsonChars + " 字符）", nameof(licenseJsonUtf8));
            var plain = Encoding.UTF8.GetBytes(licenseJsonUtf8);
            byte[] gz;
            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
                    gzip.Write(plain, 0, plain.Length);
                gz = ms.ToArray();
            }
            return Prefix + ToBase64Url(gz);
        }

        public static bool TryDecodeToLicenseJson(string input, out string json, out string error)
        {
            json = null;
            error = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "空内容";
                return false;
            }

            var sanitized = SanitizeInput(input);
            if (sanitized.Length == 0)
            {
                error = "空内容";
                return false;
            }

            if (sanitized.Length > MaxRawJsonChars)
            {
                error = "输入内容过长（上限 " + MaxRawJsonChars + " 字符）";
                return false;
            }

            if (sanitized[0] == '{')
            {
                // 兼容 license.lic 直粘贴；签名校验在 TryVerifyDocument
                json = sanitized;
                return true;
            }

            var body = sanitized;
            if (body.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                body = body.Substring(Prefix.Length);
            else
            {
                error = "不是有效的 HYC1 授权码或 JSON";
                return false;
            }

            if (string.IsNullOrEmpty(body))
            {
                error = "HYC1 授权码内容为空";
                return false;
            }

            if (body.Length > MaxCompressedBytes * 4 / 3 + 4)
            {
                error = "HYC1 授权码过长";
                return false;
            }

            byte[] gz;
            try
            {
                gz = FromBase64Url(body);
            }
            catch (Exception ex)
            {
                error = "Base64Url 解码失败: " + ex.Message;
                return false;
            }

            if (gz.Length > MaxCompressedBytes)
            {
                error = "压缩包过大（上限 " + MaxCompressedBytes + " 字节）";
                return false;
            }

            try
            {
                using (var ms = new MemoryStream(gz))
                using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    json = ReadUtf8Limited(gzip, MaxDecompressedBytes);
                }
            }
            catch (Exception ex)
            {
                error = "GZip 解压失败: " + ex.Message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{')
            {
                error = "解压后不是有效的 license JSON";
                return false;
            }
            return true;
        }

        /// <summary>去换行/空格/全角空格/零宽字符，便于微信粘贴。</summary>
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t') continue;
                if (ch == '\u3000' || ch == '\uFEFF' || ch == '\u200B' || ch == '\u200C' || ch == '\u200D') continue;
                sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static string ReadUtf8Limited(Stream stream, int maxBytes)
        {
            var buf = new byte[8192];
            using (var ms = new MemoryStream())
            {
                int n;
                while ((n = stream.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + n > maxBytes)
                        throw new InvalidDataException("解压后内容过大（上限 " + maxBytes + " 字节）");
                    ms.Write(buf, 0, n);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static string ToBase64Url(byte[] data)
        {
            var s = Convert.ToBase64String(data);
            return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] FromBase64Url(string s)
        {
            var b64 = s.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return Convert.FromBase64String(b64);
        }
    }
}
