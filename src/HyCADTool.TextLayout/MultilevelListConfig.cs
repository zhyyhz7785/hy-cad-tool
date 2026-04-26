using System;
using System.Linq;

namespace HyCADTool.TextLayout
{
    public enum ListNumberStyle
    {
        Decimal,
        LowerLetter,
        UpperLetter,
        LowerRoman,
        UpperRoman,
        Bullet
    }

    public class ListLevelDefinition
    {
        public bool IsOrdered { get; set; }
        public ListNumberStyle NumberStyle { get; set; }
        public string BulletChar { get; set; } = "\u2022";
        public double IndentChars { get; set; }
        public double? FontSizeOverride { get; set; }

        public ListLevelDefinition Clone() => new ListLevelDefinition
        {
            IsOrdered = IsOrdered,
            NumberStyle = NumberStyle,
            BulletChar = BulletChar,
            IndentChars = IndentChars,
            FontSizeOverride = FontSizeOverride
        };
    }

    public class MultilevelListConfig
    {
        public const int MaxLevels = 7;

        public ListLevelDefinition[] Levels { get; set; } = new ListLevelDefinition[MaxLevels];
        public int UnorderedFromLevel { get; set; } = -1;
        public bool ForceAllOrdered { get; set; }

        public ListLevelDefinition GetLevel(int depth)
        {
            int idx = Math.Max(0, Math.Min(depth, Levels.Length - 1));
            return Levels[idx] ?? CreateFallbackLevel(idx);
        }

        public bool ResolveIsOrdered(int depth, bool markdownIsOrdered)
        {
            if (ForceAllOrdered) return true;
            if (UnorderedFromLevel >= 0 && depth >= UnorderedFromLevel) return false;
            return GetLevel(depth).IsOrdered;
        }

        public void ApplyUniformStep(double step)
        {
            if (Levels == null) return;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (Levels[i] == null)
                    Levels[i] = CreateFallbackLevel(i);
                Levels[i].IndentChars = i * step;
            }
        }

        public MultilevelListConfig Clone()
        {
            var c = new MultilevelListConfig
            {
                UnorderedFromLevel = UnorderedFromLevel,
                ForceAllOrdered = ForceAllOrdered,
                Levels = new ListLevelDefinition[Levels.Length]
            };
            for (int i = 0; i < Levels.Length; i++)
                c.Levels[i] = Levels[i]?.Clone() ?? CreateFallbackLevel(i);
            return c;
        }

        public static MultilevelListConfig CreateDefault()
        {
            return new MultilevelListConfig
            {
                UnorderedFromLevel = -1,
                ForceAllOrdered = false,
                Levels = new[]
                {
                    new ListLevelDefinition { IsOrdered = true,  NumberStyle = ListNumberStyle.Decimal,     BulletChar = "\u2022", IndentChars = 0 },
                    new ListLevelDefinition { IsOrdered = true,  NumberStyle = ListNumberStyle.Decimal,     BulletChar = "\u2022", IndentChars = 2 },
                    new ListLevelDefinition { IsOrdered = true,  NumberStyle = ListNumberStyle.LowerLetter, BulletChar = "\u2022", IndentChars = 4 },
                    new ListLevelDefinition { IsOrdered = true,  NumberStyle = ListNumberStyle.LowerRoman,  BulletChar = "\u2022", IndentChars = 6 },
                    new ListLevelDefinition { IsOrdered = false, NumberStyle = ListNumberStyle.Bullet,      BulletChar = "\u2022", IndentChars = 8 },
                    new ListLevelDefinition { IsOrdered = false, NumberStyle = ListNumberStyle.Bullet,      BulletChar = "\u2013", IndentChars = 10 },
                    new ListLevelDefinition { IsOrdered = false, NumberStyle = ListNumberStyle.Bullet,      BulletChar = "\u2022", IndentChars = 12 },
                }
            };
        }

        private static ListLevelDefinition CreateFallbackLevel(int index)
        {
            return new ListLevelDefinition
            {
                IsOrdered = index < 4,
                NumberStyle = index < 4 ? ListNumberStyle.Decimal : ListNumberStyle.Bullet,
                BulletChar = "\u2022",
                IndentChars = index * 2.0
            };
        }
    }
}
