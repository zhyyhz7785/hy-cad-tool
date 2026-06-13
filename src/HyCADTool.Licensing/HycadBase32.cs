using System;
using System.Linq;
namespace HyCADTool.Licensing
{
    /// <summary>Base32 变体，字母表去掉易混字符；对前 20 字节编码为 32 个字符，再 4 字符一组。</summary>
    public static class HycadBase32
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>32 位无分隔机器码是否全属本字母表。</summary>
        public static bool IsValidNormalizedCode(string normalized32)
        {
            if (string.IsNullOrEmpty(normalized32) || normalized32.Length != 32)
                return false;
            foreach (var c in normalized32)
            {
                if (Alphabet.IndexOf(c) < 0)
                    return false;
            }
            return true;
        }

        public static string Encode20Bytes(byte[] first20)
        {
            if (first20 == null) first20 = Array.Empty<byte>();
            var b = new byte[20];
            for (int i = 0; i < 20; i++) b[i] = i < first20.Length ? first20[i] : (byte)0;

            int bitBuffer = 0, bitCount = 0;
            var outChars = new char[32];
            int o = 0;
            for (int i = 0; i < 20; i++)
            {
                bitBuffer = (bitBuffer << 8) | b[i];
                bitCount += 8;
                while (bitCount >= 5)
                {
                    bitCount -= 5;
                    int v = (bitBuffer >> bitCount) & 0x1F;
                    outChars[o++] = Alphabet[v];
                }
            }
            while (o < 32) outChars[o++] = '2';
            var s = new string(outChars, 0, 32);
            return string.Join("-", Enumerable.Range(0, 8).Select(i => s.Substring(i * 4, 4)));
        }
    }
}
