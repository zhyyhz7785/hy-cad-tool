namespace HyCADTool.TextLayout
{
    /// <summary>
    /// 统一文本显示宽度口径：ASCII=1，CJK/全角=2。
    /// </summary>
    public static class DisplayWidthCalculator
    {
        public static int GetDisplayUnits(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int units = 0;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                units += GetCharUnits(c);
            }
            return units;
        }

        public static int GetCharUnits(char c)
        {
            int code = c;
            if (code <= 0x007F) return 1;
            if ((code >= 0x3400 && code <= 0x4DBF) ||
                (code >= 0x4E00 && code <= 0x9FFF) ||
                (code >= 0xF900 && code <= 0xFAFF))
                return 2;
            if ((code >= 0x3000 && code <= 0x303F) ||
                (code >= 0xFF01 && code <= 0xFF60) ||
                (code >= 0xFFE0 && code <= 0xFFE6))
                return 2;

            return 2;
        }
    }
}
