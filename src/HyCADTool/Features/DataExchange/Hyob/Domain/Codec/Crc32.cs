namespace HyCADTool.Features.DataExchange.Hyob.Domain.Codec
{
    /// <summary>
    /// CRC32 IEEE 802.3（poly = 0xEDB88320，反射变体），与 zlib / PNG / Ethernet 一致。
    /// 仅用于 hyob Object header 末尾的快速完整性校验，不用于内容寻址（那个用 SHA-256）。
    /// 标准测试向量：CRC32("123456789") = 0xCBF43926。
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table;

        static Crc32()
        {
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                Table[i] = c;
            }
        }

        public static uint Compute(byte[] data)
        {
            return Compute(data, 0, data?.Length ?? 0);
        }

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < count; i++)
                crc = Table[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }
    }
}
