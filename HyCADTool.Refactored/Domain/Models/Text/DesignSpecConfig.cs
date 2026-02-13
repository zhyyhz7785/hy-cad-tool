using System;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Models.Text
{
    /// <summary>
    /// 设计说明排版配置（平台无关）
    /// 每栏有独立的字符数，对应独立的栏宽
    /// </summary>
    public class DesignSpecConfig
    {
        // ── 出图比例（用户可调，默认1，独立于 SettingsPanel） ──
        public double Scale { get; set; } = 1.0;

        // ── 预览缩放（仅预览用，默认1.0） ──
        public double PreviewScale { get; set; } = 1.0;

        // ── 栏 ──
        public int ColumnCount { get; set; } = 2;
        public double ColumnGutter { get; set; } = 10;     // 图纸 mm

        /// <summary>
        /// 每栏的字符数数组（长度 = ColumnCount）
        /// 每栏独立宽度 = CharsPerColumn[i] × TextSize × TextXScale × Scale
        /// </summary>
        public int[] CharsPerColumn { get; set; } = new int[] { 28, 28 };

        // ── 行间距 ──
        public double LineSpacingFactor { get; set; } = 1.2;

        // ── 标题字号倍率（相对于 TextSize） ──
        public double H1Scale { get; set; } = 1.6;
        public double H2Scale { get; set; } = 1.3;
        public double H3Scale { get; set; } = 1.1;

        // ── 段前段后间距（字高倍数，× TextSize × Scale → 模型空间 mm） ──
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

        // ── 列表缩进（图纸 mm） ──
        public double ListIndent { get; set; } = 4;
        public double QuoteIndent { get; set; } = 5;

        // ── 字体 ──
        public string FontFileName { get; set; } = "tssdeng.shx";
        public string BigFontFileName { get; set; } = "hztxt.shx";
        public double TextSize { get; set; } = 2.5;        // 图纸 mm
        public double TextXScale { get; set; } = 0.7;

        // ── 粗体用字体（SHX 不支持 Bold，切 TTF 模拟） ──
        public string BoldFontName { get; set; } = "SimHei";

        // ── 计算属性（× Scale → 模型空间 mm） ──

        public double ActualTextHeight => TextSize * Scale;

        /// <summary>获取第 i 栏的宽度（模型空间 mm）</summary>
        public double GetColumnWidth(int colIndex)
        {
            int chars = GetCharsForColumn(colIndex);
            return chars * TextSize * TextXScale * Scale;
        }

        /// <summary>获取第 i 栏的字符数</summary>
        public int GetCharsForColumn(int colIndex)
        {
            if (CharsPerColumn == null || CharsPerColumn.Length == 0)
                return 28;
            if (colIndex < CharsPerColumn.Length)
                return CharsPerColumn[colIndex];
            return CharsPerColumn[CharsPerColumn.Length - 1]; // 超出范围取最后一个
        }

        /// <summary>总宽度 = 各栏宽度之和 + 栏间距 × (栏数-1)</summary>
        public double ActualTotalWidth
        {
            get
            {
                double w = 0;
                int cols = Math.Max(1, ColumnCount);
                for (int i = 0; i < cols; i++)
                    w += GetColumnWidth(i);
                w += ActualColumnGutter * (cols - 1);
                return w;
            }
        }

        // TotalHeight 保留用于控制每栏最大高度
        public double TotalHeight { get; set; } = 350;
        public double ActualTotalHeight => TotalHeight * Scale;

        public double H1Height => TextSize * H1Scale * Scale;
        public double H2Height => TextSize * H2Scale * Scale;
        public double H3Height => TextSize * H3Scale * Scale;

        public double ActualListIndent => ListIndent * Scale;
        public double ActualQuoteIndent => QuoteIndent * Scale;
        public double ActualColumnGutter => ColumnGutter * Scale;

        /// <summary>获取指定标题级别的段前间距（模型空间 mm）</summary>
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

        /// <summary>获取指定标题级别的段后间距（模型空间 mm）</summary>
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

        /// <summary>正文段后间距（模型空间 mm）</summary>
        public double ActualPSpaceAfter => PSpaceAfter * TextSize * Scale;
        /// <summary>列表项段后间距（模型空间 mm）</summary>
        public double ActualLiSpaceAfter => LiSpaceAfter * TextSize * Scale;
        /// <summary>引用段前间距（模型空间 mm）</summary>
        public double ActualQuoteSpaceBefore => QuoteSpaceBefore * TextSize * Scale;
        /// <summary>引用段后间距（模型空间 mm）</summary>
        public double ActualQuoteSpaceAfter => QuoteSpaceAfter * TextSize * Scale;

        // ── 兼容：旧版单值 CharsPerLine 的序列化映射 ──
        // （JSON 反序列化时如果旧数据只有 CharsPerLine 字段，可通过此属性恢复）
        [Obsolete("使用 CharsPerColumn 数组")]
        public int CharsPerLine
        {
            get => CharsPerColumn != null && CharsPerColumn.Length > 0 ? CharsPerColumn[0] : 28;
            set
            {
                // 向后兼容：单值设置 → 所有栏相同
                CharsPerColumn = Enumerable.Repeat(value, Math.Max(1, ColumnCount)).ToArray();
            }
        }
    }
}
