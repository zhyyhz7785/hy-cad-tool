using System;

namespace HyCADTool.MarkdownEditor.Models
{
    /// <summary>
    /// 编辑器配置默认值 — 唯一来源，修改默认参数仅改此处。
    /// </summary>
    public static class EditorConfigDefaults
    {
        // ── 出图 ──
        public const double DrawScale = 1.0;
        public const double PreviewScale = 1.0;

        // ── 栏 ──
        public const int ColumnCount = 2;
        public const double ColumnGutter = 5;
        public const int MaxColumnCount = 10;
        public const int MinColumnWidthPx = 80;
        public const int MinColumnHeightPx = 80;
        public static readonly int[] CharsPerColumn = new[] { 28, 28 };
        public const double TotalHeight = 350;

        // ── 字体 ──
        public const double TextSize = 2.5;
        public const double TextXScale = 1.0;
        public const string PagePreset = "A2横向";
        public const double PageWidthMm = 594;
        public const double PageHeightMm = 420;
        public const double MarginLeftMm = 25;
        public const double MarginRightMm = 10;
        public const double MarginTopMm = 10;
        public const double MarginBottomMm = 10;
        public const double ColumnInnerPaddingMm = 5;
        public const double BorderWidth = 1;
        public const double HandleWidth = 3;
        public const double HandleActiveWidth = 6;
        public const string FontFileName = "Microsoft YaHei";
        public const string BigFontFileName = "";
        public const string BoldFontName = "Microsoft YaHei";
        public const string PreviewFontFamily = "Microsoft YaHei";

        // ── CAD 样式 ──
        public const string StyleTName = "0-hy-说明-T";
        public const string StyleTFont = "微软雅黑";
        public const string StyleSName = "0-hy-说明-S";
        public const string StyleSFont = "tssdeng.shx";
        public const string StyleSBigFont = "tssdchn.shx";
        public const string CadSyncStyleName = "0-hy-说明-S";

        // ── MText ──
        public const string MTextAttachment = "TopLeft";
        public const string MTextLineSpacingStyle = "Exactly";
        public const double MTextObliquingAngle = 0;
        public const double MTextCharSpacing = 1.0;
        public const string MTextParagraphAlign = "Left";

        // ── 标题倍率 ──
        public const double H1Scale = 1.6;
        public const double H2Scale = 1.3;
        public const double H3Scale = 1.1;
        public const double LineSpacingFactor = 1.2;

        // ── 缩进 ──
        public const double ListIndent = 4;
        public const double QuoteIndent = 5;

        // ── 段前后间距 ──
        public const double H1SpaceBefore = 2.0;
        public const double H1SpaceAfter = 0.8;
        public const double H2SpaceBefore = 1.5;
        public const double H2SpaceAfter = 0.6;
        public const double H3SpaceBefore = 1.2;
        public const double H3SpaceAfter = 0.4;
        public const double PSpaceAfter = 0.5;
        public const double LiSpaceAfter = 0.2;
        public const double QuoteSpaceBefore = 0.5;
        public const double QuoteSpaceAfter = 0.5;

        /// <summary>创建完整默认配置</summary>
        public static EditorConfig Create()
        {
            return new EditorConfig
            {
                DrawScale = DrawScale,
                PreviewScale = PreviewScale,
                ColumnCount = ColumnCount,
                ColumnGutter = ColumnGutter,
                MaxColumnCount = MaxColumnCount,
                MinColumnWidthPx = MinColumnWidthPx,
                MinColumnHeightPx = MinColumnHeightPx,
                CharsPerColumn = (int[])CharsPerColumn.Clone(),
                TotalHeight = TotalHeight,
                TextSize = TextSize,
                TextXScale = TextXScale,
                PagePreset = PagePreset,
                PageWidthMm = PageWidthMm,
                PageHeightMm = PageHeightMm,
                MarginLeftMm = MarginLeftMm,
                MarginRightMm = MarginRightMm,
                MarginTopMm = MarginTopMm,
                MarginBottomMm = MarginBottomMm,
                ColumnInnerPaddingMm = ColumnInnerPaddingMm,
                BorderWidth = BorderWidth,
                HandleWidth = HandleWidth,
                HandleActiveWidth = HandleActiveWidth,
                FontFileName = FontFileName,
                BigFontFileName = BigFontFileName,
                BoldFontName = BoldFontName,
                PreviewFontFamily = PreviewFontFamily,
                StyleTName = StyleTName,
                StyleTFont = StyleTFont,
                StyleSName = StyleSName,
                StyleSFont = StyleSFont,
                StyleSBigFont = StyleSBigFont,
                CadSyncStyleName = CadSyncStyleName,
                MTextAttachment = MTextAttachment,
                MTextLineSpacingStyle = MTextLineSpacingStyle,
                MTextObliquingAngle = MTextObliquingAngle,
                MTextCharSpacing = MTextCharSpacing,
                MTextParagraphAlign = MTextParagraphAlign,
                H1Scale = H1Scale,
                H2Scale = H2Scale,
                H3Scale = H3Scale,
                LineSpacingFactor = LineSpacingFactor,
                ListIndent = ListIndent,
                QuoteIndent = QuoteIndent,
                H1SpaceBefore = H1SpaceBefore,
                H1SpaceAfter = H1SpaceAfter,
                H2SpaceBefore = H2SpaceBefore,
                H2SpaceAfter = H2SpaceAfter,
                H3SpaceBefore = H3SpaceBefore,
                H3SpaceAfter = H3SpaceAfter,
                PSpaceAfter = PSpaceAfter,
                LiSpaceAfter = LiSpaceAfter,
                QuoteSpaceBefore = QuoteSpaceBefore,
                QuoteSpaceAfter = QuoteSpaceAfter
            };
        }
    }
}
