using System;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Codec
{
    /// <summary>
    /// hyob Object 二进制头（10 字节）+ 末尾 4 字节 CRC32 校验。
    /// 设计：02 §3.2、03 §3.1、04 §5。
    ///
    /// <code>
    /// [ magic         : 2 bytes  = "HO" (0x48 'H', 0x4F 'O') ]
    /// [ schema_ver    : 1 byte ]
    /// [ type_id       : 2 bytes  little-endian ]
    /// [ flags         : 1 byte ]
    /// [ payload_len   : 4 bytes  little-endian ]
    /// [ payload       : payload_len bytes ]
    /// [ checksum      : 4 bytes  CRC32 over [magic..payload_end] ]
    /// </code>
    ///
    /// 不存字段名：type_id + schema_ver 唯一确定 schema，字段顺序与类型固定。
    /// 这是 DWG 节省体积的核心，对 hyob 同样适用。
    /// </summary>
    public readonly struct HyobObjectHeader
    {
        // little-endian 写入：低字节 0x48 ('H')，高字节 0x4F ('O')
        public const ushort Magic = 0x4F48;
        public const int HeaderByteLength = 10;
        public const int ChecksumByteLength = 4;

        public byte SchemaVersion { get; }
        public HyobObjectKind TypeId { get; }
        public byte Flags { get; }
        public int PayloadLength { get; }

        public HyobObjectHeader(HyobObjectKind typeId, byte schemaVersion, int payloadLength, byte flags = 0)
        {
            if (payloadLength < 0) throw new ArgumentOutOfRangeException(nameof(payloadLength));
            TypeId = typeId;
            SchemaVersion = schemaVersion;
            PayloadLength = payloadLength;
            Flags = flags;
        }

        /// <summary>
        /// 序列化整个 Object：header(10) + payload(N) + crc32(4)。
        /// CRC 覆盖 header + payload，不覆盖自身。
        /// </summary>
        public byte[] Encode(byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            if (payload.Length != PayloadLength)
                throw new InvalidOperationException(
                    $"PayloadLength={PayloadLength} 与实际 payload 长度 {payload.Length} 不一致");

            var total = HeaderByteLength + payload.Length + ChecksumByteLength;
            var buf = new byte[total];

            // header
            buf[0] = (byte)(Magic & 0xFF);
            buf[1] = (byte)(Magic >> 8);
            buf[2] = SchemaVersion;
            buf[3] = (byte)((ushort)TypeId & 0xFF);
            buf[4] = (byte)((ushort)TypeId >> 8);
            buf[5] = Flags;
            buf[6] = (byte)(PayloadLength & 0xFF);
            buf[7] = (byte)((PayloadLength >> 8) & 0xFF);
            buf[8] = (byte)((PayloadLength >> 16) & 0xFF);
            buf[9] = (byte)((PayloadLength >> 24) & 0xFF);

            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, buf, HeaderByteLength, payload.Length);

            uint crc = Crc32.Compute(buf, 0, HeaderByteLength + payload.Length);
            int crcOffset = HeaderByteLength + payload.Length;
            buf[crcOffset]     = (byte)(crc & 0xFF);
            buf[crcOffset + 1] = (byte)((crc >> 8) & 0xFF);
            buf[crcOffset + 2] = (byte)((crc >> 16) & 0xFF);
            buf[crcOffset + 3] = (byte)((crc >> 24) & 0xFF);

            return buf;
        }

        /// <summary>反序列化整个 Object，返回 (header, payload)。CRC 校验失败抛 <see cref="InvalidDataException"/>。</summary>
        public static (HyobObjectHeader Header, byte[] Payload) Decode(byte[] blob)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (blob.Length < HeaderByteLength + ChecksumByteLength)
                throw new InvalidDataException(
                    $"hyob Object 至少 {HeaderByteLength + ChecksumByteLength} 字节，收到 {blob.Length}");

            ushort magic = (ushort)(blob[0] | (blob[1] << 8));
            if (magic != Magic)
                throw new InvalidDataException($"hyob magic 错误：期望 0x{Magic:X4} 实际 0x{magic:X4}");

            byte schemaVer = blob[2];
            ushort typeId = (ushort)(blob[3] | (blob[4] << 8));
            byte flags = blob[5];
            int payloadLen = blob[6] | (blob[7] << 8) | (blob[8] << 16) | (blob[9] << 24);
            if (payloadLen < 0)
                throw new InvalidDataException($"hyob payload_len 非法：{payloadLen}");

            int expectedTotal = HeaderByteLength + payloadLen + ChecksumByteLength;
            if (blob.Length != expectedTotal)
                throw new InvalidDataException($"hyob blob 长度 {blob.Length} != 期望 {expectedTotal}");

            uint crcExpected = Crc32.Compute(blob, 0, HeaderByteLength + payloadLen);
            uint crcActual =
                  (uint)blob[expectedTotal - 4]
                | ((uint)blob[expectedTotal - 3] << 8)
                | ((uint)blob[expectedTotal - 2] << 16)
                | ((uint)blob[expectedTotal - 1] << 24);
            if (crcActual != crcExpected)
                throw new InvalidDataException(
                    $"hyob CRC 校验失败：期望 0x{crcExpected:X8} 实际 0x{crcActual:X8}");

            var payload = new byte[payloadLen];
            if (payloadLen > 0)
                Buffer.BlockCopy(blob, HeaderByteLength, payload, 0, payloadLen);

            return (new HyobObjectHeader((HyobObjectKind)typeId, schemaVer, payloadLen, flags), payload);
        }
    }
}
