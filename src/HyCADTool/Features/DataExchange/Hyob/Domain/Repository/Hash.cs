using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// hyob 的 SHA-256 内容寻址哈希（32 字节）。不可变值类型。
    /// 设计来源：02 §3.2 / 04 §5。
    /// </summary>
    public readonly struct Hash : IEquatable<Hash>
    {
        public const int ByteLength = 32;
        public const int HexLength = 64;

        // null 表示零哈希（未初始化值类型默认值）
        private readonly byte[] _bytes;

        public Hash(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != ByteLength)
                throw new ArgumentException($"Hash 必须 {ByteLength} 字节，收到 {bytes.Length}", nameof(bytes));
            _bytes = (byte[])bytes.Clone();
        }

        public bool IsZero => _bytes == null;

        public byte[] ToArray()
        {
            return _bytes != null ? (byte[])_bytes.Clone() : new byte[ByteLength];
        }

        public string ToHex()
        {
            var b = _bytes ?? new byte[ByteLength];
            var sb = new StringBuilder(HexLength);
            for (int i = 0; i < ByteLength; i++) sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }

        public static Hash FromHex(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            if (hex.Length != HexLength)
                throw new ArgumentException($"Hex 必须 {HexLength} 字符，收到 {hex.Length}", nameof(hex));
            var b = new byte[ByteLength];
            for (int i = 0; i < ByteLength; i++)
            {
                b[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }
            return new Hash(b);
        }

        /// <summary>对任意字节序列计算 SHA-256，得到 hyob Hash。</summary>
        public static Hash OfPayload(byte[] payload)
        {
            using (var sha = SHA256.Create())
            {
                return new Hash(sha.ComputeHash(payload ?? Array.Empty<byte>()));
            }
        }

        public bool Equals(Hash other)
        {
            var a = _bytes ?? new byte[ByteLength];
            var b = other._bytes ?? new byte[ByteLength];
            for (int i = 0; i < ByteLength; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is Hash h && Equals(h);

        public override int GetHashCode()
        {
            // SHA-256 已经均匀分布，取前 4 字节作 32-bit 散列足够。
            var b = _bytes;
            if (b == null) return 0;
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        public override string ToString() => ToHex();

        public static bool operator ==(Hash a, Hash b) => a.Equals(b);
        public static bool operator !=(Hash a, Hash b) => !a.Equals(b);
    }
}
