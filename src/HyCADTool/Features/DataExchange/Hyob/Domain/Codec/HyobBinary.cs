using System.IO;
using System.Text;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Codec
{
    /// <summary>
    /// hyob 紧凑二进制读写辅助。所有定长字段固定 little-endian（与 BCL BinaryWriter 默认一致）。
    /// 字符串采用 (uint32 byteLen + UTF-8 bytes) 长度前缀格式（不复用 BCL 的 7-bit varint，避免歧义）；
    /// byteLen == 0xFFFFFFFF 表示 null，0 表示空串。
    /// 设计：02 §3.2 / 04 §5。
    /// </summary>
    public static class HyobBinary
    {
        private const uint NullStringSentinel = 0xFFFFFFFFu;

        public static void WriteHash(BinaryWriter w, Hash h)
        {
            var b = h.ToArray();
            w.Write(b, 0, Hash.ByteLength);
        }

        public static Hash ReadHash(BinaryReader r)
        {
            var b = r.ReadBytes(Hash.ByteLength);
            if (b.Length != Hash.ByteLength)
                throw new InvalidDataException($"读 Hash 期望 {Hash.ByteLength} 字节，实际 {b.Length}");
            return new Hash(b);
        }

        public static void WriteUtf8(BinaryWriter w, string s)
        {
            if (s == null) { w.Write(NullStringSentinel); return; }
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write((uint)bytes.Length);
            if (bytes.Length > 0) w.Write(bytes);
        }

        public static string ReadUtf8(BinaryReader r)
        {
            uint len = r.ReadUInt32();
            if (len == NullStringSentinel) return null;
            if (len == 0) return string.Empty;
            var b = r.ReadBytes((int)len);
            if (b.Length != (int)len)
                throw new InvalidDataException($"UTF-8 字符串长度不足：期望 {len}，实际 {b.Length}");
            return Encoding.UTF8.GetString(b);
        }

        /// <summary>写入定长字节数组（不带长度前缀）。读取时必须知道长度。</summary>
        public static void WriteRawBytes(BinaryWriter w, byte[] data)
        {
            if (data != null && data.Length > 0) w.Write(data);
        }

        /// <summary>写入带 uint32 长度前缀的字节数组。null 也写为 sentinel。</summary>
        public static void WriteLengthPrefixedBytes(BinaryWriter w, byte[] data)
        {
            if (data == null) { w.Write(NullStringSentinel); return; }
            w.Write((uint)data.Length);
            if (data.Length > 0) w.Write(data);
        }

        public static byte[] ReadLengthPrefixedBytes(BinaryReader r)
        {
            uint len = r.ReadUInt32();
            if (len == NullStringSentinel) return null;
            if (len == 0) return new byte[0];
            var b = r.ReadBytes((int)len);
            if (b.Length != (int)len)
                throw new InvalidDataException($"字节数组长度不足：期望 {len}，实际 {b.Length}");
            return b;
        }
    }
}
