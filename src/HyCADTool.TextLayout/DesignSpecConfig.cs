using System;
using System.Linq;

namespace HyCADTool.TextLayout
{
    /// <summary>
    /// 设计说明排版配置（平台无关）
    /// </summary>
    public class DesignSpecConfig
    {
        public double Scale { get; set; } = 1.0;
        public double PreviewScale { get; set; } = 1.0;
        public int ColumnCount { get; set; } = 2;
        public double ColumnGutter { get; set; } = 5;
        public int[] CharsPerColumn { get; set; } = new[] { 28, 28 };
        public double LineSpacingFactor { get; set; } = 1.2;
        public double H1Scale { get; set; } = 1.6;
        public double H2Scale { get; set; } = 1.3;
        public double H3Scale { get; set; } = 1.1;
        public double H1SpaceBefore { get; set; } = 2.0;
        public double H1SpaceAfter { get; set; } = 0.8;
        public double H2SpaceBefore { get; set; } = 1.5;
        public double H2SpaceAfter { get; set; } = 0.6;
        public double H3SpaceBefore { get; set; } = 1.2;
        public double H3SpaceAfter { get; set; } = 0.4;
        public double PSpaceAfter { get; set; } = 0.5;
        public double LiSpaceAfter { get; set; } = 0.2;
        public double QuoteSpaceBefore { get; set; } = 0.5;
        public double QuoteSpaceAfter { get; set; } = 0.5;
        public double ListIndent { get; set; } = 4;
        public double QuoteIndent { get; set; } = 5;
        public MultilevelListConfig MultilevelList { get; set; }
        public string FontFileName { get; set; } = "Microsoft YaHei";
        public string BigFontFileName { get; set; } = "";
        public double TextSize { get; set; } = 2.5;
        public double TextXScale { get; set; } = 0.7;
        public string BoldFontName { get; set; } = "Microsoft YaHei";
        public double TotalHeight { get; set; } = 350;
        public string PagePreset { get; set; } = "A2横向";
        public double PageWidthMm { get; set; } = 594;
        public double PageHeightMm { get; set; } = 420;
        public double MarginLeftMm { get; set; } = 25;
        public double MarginRightMm { get; set; } = 10;
        public double MarginTopMm { get; set; } = 10;
        public double MarginBottomMm { get; set; } = 10;
        public double ColumnInnerPaddingMm { get; set; } = 5;

        public double ActualTextHeight => TextSize * Scale;
        public double ActualTotalHeight => TotalHeight * Scale;
        public double H1Height => TextSize * H1Scale * Scale;
        public double H2Height => TextSize * H2Scale * Scale;
        public double H3Height => TextSize * H3Scale * Scale;
        public double ActualListIndent => ListIndent * Scale;
        public double ActualQuoteIndent => QuoteIndent * Scale;
        public double CharWidth => TextSize * TextXScale * Scale;

        public double GetListIndentMm(int depth)
        {
            if (MultilevelList != null)
            {
                var level = MultilevelList.GetLevel(depth);
                return level.IndentChars * CharWidth;
            }
            return ActualListIndent * depth;
        }
        public double ActualColumnGutter => ColumnGutter * Scale;
        public double ActualColumnInnerPadding => ColumnInnerPaddingMm * Scale;
        public double ActualPSpaceAfter => PSpaceAfter * TextSize * Scale;
        public double ActualLiSpaceAfter => LiSpaceAfter * TextSize * Scale;
        public double ActualQuoteSpaceBefore => QuoteSpaceBefore * TextSize * Scale;
        public double ActualQuoteSpaceAfter => QuoteSpaceAfter * TextSize * Scale;

        public int GetCharsForColumn(int colIndex)
        {
            if (CharsPerColumn == null || CharsPerColumn.Length == 0)
                return 28;
            if (colIndex < CharsPerColumn.Length)
                return CharsPerColumn[colIndex];
            return CharsPerColumn[CharsPerColumn.Length - 1];
        }

        public double GetColumnWidth(int colIndex)
        {
            if (CanUsePageDrivenWidth())
                return GetPageDrivenColumnWidth();

            int chars = GetCharsForColumn(colIndex);
            return chars * TextSize * TextXScale * Scale;
        }

        public double GetPageDrivenColumnWidth()
        {
            double availableWidth = (PageWidthMm - MarginLeftMm - MarginRightMm) * Scale;
            int cols = Math.Max(1, ColumnCount);
            double gutterTotal = ActualColumnGutter * Math.Max(0, cols - 1);
            double width = (availableWidth - gutterTotal) / cols;
            return Math.Max(TextSize * Scale, width);
        }

        public int DeriveCharsPerColumn()
        {
            double colWidth = GetColumnWidth(0);
            double charWidth = Math.Max(0.01, TextSize * TextXScale * Scale);
            return Math.Max(1, (int)Math.Floor(colWidth / charWidth));
        }

        public double GetHeadingSpaceBefore(int level)
        {
            double factor;
            switch (level)
            {
                case 1: factor = H1SpaceBefore; break;
                case 2: factor = H2SpaceBefore; break;
                case 3: factor = H3SpaceBefore; break;
                default: factor = 0; break;
            }
            return factor * TextSize * Scale;
        }

        public double GetHeadingSpaceAfter(int level)
        {
            double factor;
            switch (level)
            {
                case 1: factor = H1SpaceAfter; break;
                case 2: factor = H2SpaceAfter; break;
                case 3: factor = H3SpaceAfter; break;
                default: factor = 0; break;
            }
            return factor * TextSize * Scale;
        }

        [Obsolete("使用 CharsPerColumn 数组")]
        public int CharsPerLine
        {
            get => CharsPerColumn != null && CharsPerColumn.Length > 0 ? CharsPerColumn[0] : 28;
            set => CharsPerColumn = Enumerable.Repeat(value, Math.Max(1, ColumnCount)).ToArray();
        }

        public void Normalize()
        {
            ColumnCount = Math.Max(1, Math.Min(10, ColumnCount));
            Scale = Scale > 0 ? Scale : 1.0;
            PreviewScale = PreviewScale > 0 ? PreviewScale : 1.0;
            ColumnGutter = Math.Max(0, ColumnGutter);
            TextSize = TextSize > 0 ? TextSize : 2.5;
            TextXScale = TextXScale > 0 ? TextXScale : 0.7;
            TotalHeight = TotalHeight > 0 ? TotalHeight : 350;
            PageWidthMm = PageWidthMm > 0 ? PageWidthMm : 594;
            PageHeightMm = PageHeightMm > 0 ? PageHeightMm : 420;
            MarginLeftMm = Math.Max(0, MarginLeftMm);
            MarginRightMm = Math.Max(0, MarginRightMm);
            MarginTopMm = Math.Max(0, MarginTopMm);
            MarginBottomMm = Math.Max(0, MarginBottomMm);
            ColumnInnerPaddingMm = Math.Max(0, ColumnInnerPaddingMm);

            if (CharsPerColumn == null || CharsPerColumn.Length == 0)
            {
                CharsPerColumn = Enumerable.Repeat(28, ColumnCount).ToArray();
                return;
            }

            CharsPerColumn = CharsPerColumn.Select(v => Math.Max(1, v)).ToArray();
            if (CharsPerColumn.Length != ColumnCount)
            {
                int fill = CharsPerColumn[CharsPerColumn.Length - 1];
                CharsPerColumn = Enumerable.Range(0, ColumnCount)
                    .Select(i => i < CharsPerColumn.Length ? CharsPerColumn[i] : fill)
                    .ToArray();
            }
        }

        public void Validate()
        {
            if (Scale <= 0) throw new ArgumentException("Scale 必须大于 0");
            if (PreviewScale <= 0) throw new ArgumentException("PreviewScale 必须大于 0");
            if (ColumnCount <= 0) throw new ArgumentException("ColumnCount 必须大于 0");
            if (ColumnGutter < 0) throw new ArgumentException("ColumnGutter 不能为负数");
            if (TextSize <= 0) throw new ArgumentException("TextSize 必须大于 0");
            if (TextXScale <= 0) throw new ArgumentException("TextXScale 必须大于 0");
            if (TotalHeight <= 0) throw new ArgumentException("TotalHeight 必须大于 0");
            if (PageWidthMm <= 0) throw new ArgumentException("PageWidthMm 必须大于 0");
            if (PageHeightMm <= 0) throw new ArgumentException("PageHeightMm 必须大于 0");
            if (MarginLeftMm < 0 || MarginRightMm < 0 || MarginTopMm < 0 || MarginBottomMm < 0)
                throw new ArgumentException("Margin 不能为负数");
            if (ColumnInnerPaddingMm < 0)
                throw new ArgumentException("ColumnInnerPaddingMm 不能为负数");
            if (PageWidthMm <= MarginLeftMm + MarginRightMm)
                throw new ArgumentException("PageWidthMm 必须大于左右边距之和");
            if (PageHeightMm <= MarginTopMm + MarginBottomMm)
                throw new ArgumentException("PageHeightMm 必须大于上下边距之和");
            if (CharsPerColumn == null || CharsPerColumn.Length == 0)
                throw new ArgumentException("CharsPerColumn 不能为空");
            if (CharsPerColumn.Any(v => v <= 0))
                throw new ArgumentException("CharsPerColumn 的值必须大于 0");
        }

        private bool CanUsePageDrivenWidth()
        {
            if (PageWidthMm <= 0) return false;
            if (MarginLeftMm < 0 || MarginRightMm < 0) return false;
            return PageWidthMm > MarginLeftMm + MarginRightMm;
        }
    }
}
