using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using HyCADTool.TextLayout;

namespace HyCADTool.MarkdownEditor.Models
{
    /// <summary>
    /// 编辑器输入参数（由主项目序列化为 JSON 传入）
    /// </summary>
    public class EditorInput
    {
        public string Markdown { get; set; } = "";
        public EditorConfig Config { get; set; } = new EditorConfig();
        public string CurrentFilePath { get; set; } = "";
    }

    /// <summary>
    /// 编辑器返回结果（序列化为 JSON 返回给主项目）
    /// </summary>
    public class EditorResult
    {
        public bool Confirmed { get; set; }
        public string Markdown { get; set; } = "";
        public EditorConfig Config { get; set; } = new EditorConfig();
        /// <summary>每栏字符数（由预览 JS 回传）</summary>
        public int[] CharsPerColumn { get; set; }
        /// <summary>每栏段落索引（格式: "0,1,2|3,4,5"）</summary>
        public string ColumnParagraphIndices { get; set; }
        /// <summary>结构化预览统计结果（用于回归与精度分析）</summary>
        public PreviewStats PreviewStats { get; set; } = new PreviewStats();
        /// <summary>统一布局引擎结果（预览与 CAD 共用）</summary>
        public LayoutResult LayoutResult { get; set; }
    }

    /// <summary>
    /// 编辑器配置（镜像 DesignSpecConfig 的 UI 可调字段）
    /// </summary>
    public class EditorConfig
    {
        // ── 出图 ──
        public double DrawScale { get; set; } = EditorConfigDefaults.DrawScale;
        public double PreviewScale { get; set; } = EditorConfigDefaults.PreviewScale;

        /// <summary>
        /// 兼容旧字段：Scale -> DrawScale。
        /// 仅用于反序列化历史数据，不再参与序列化输出。
        /// </summary>
        [Obsolete("Use DrawScale instead.")]
        [JsonProperty("Scale")]
        public double Scale
        {
            get => DrawScale;
            set => DrawScale = value;
        }

        public bool ShouldSerializeScale() => false;

        // ── 栏 ──
        public int ColumnCount { get; set; } = EditorConfigDefaults.ColumnCount;
        public double ColumnGutter { get; set; } = EditorConfigDefaults.ColumnGutter;
        /// <summary>栏最大数量（1～99）</summary>
        public int MaxColumnCount { get; set; } = EditorConfigDefaults.MaxColumnCount;
        /// <summary>最小栏宽（px）</summary>
        public int MinColumnWidthPx { get; set; } = EditorConfigDefaults.MinColumnWidthPx;
        /// <summary>最小栏高（px）</summary>
        public int MinColumnHeightPx { get; set; } = EditorConfigDefaults.MinColumnHeightPx;
        /// <summary>栏高度是否跨页同步（true=同步，false=各页独立）</summary>
        public bool IsColumnHeightSync { get; set; } = EditorConfigDefaults.IsColumnHeightSync;
        public int[] CharsPerColumn { get; set; } = (int[])EditorConfigDefaults.CharsPerColumn.Clone();
        public double TotalHeight { get; set; } = EditorConfigDefaults.TotalHeight;

        // ── 字体 ──
        public double TextSize { get; set; } = EditorConfigDefaults.TextSize;
        public double TextXScale { get; set; } = EditorConfigDefaults.TextXScale;
        public string PagePreset { get; set; } = EditorConfigDefaults.PagePreset;
        public double PageWidthMm { get; set; } = EditorConfigDefaults.PageWidthMm;
        public double PageHeightMm { get; set; } = EditorConfigDefaults.PageHeightMm;
        public double MarginLeftMm { get; set; } = EditorConfigDefaults.MarginLeftMm;
        public double MarginRightMm { get; set; } = EditorConfigDefaults.MarginRightMm;
        public double MarginTopMm { get; set; } = EditorConfigDefaults.MarginTopMm;
        public double MarginBottomMm { get; set; } = EditorConfigDefaults.MarginBottomMm;
        public double ColumnInnerPaddingMm { get; set; } = EditorConfigDefaults.ColumnInnerPaddingMm;
        public double BorderWidth { get; set; } = EditorConfigDefaults.BorderWidth;
        public double HandleWidth { get; set; } = EditorConfigDefaults.HandleWidth;
        public double HandleActiveWidth { get; set; } = EditorConfigDefaults.HandleActiveWidth;
        /// <summary>
        /// 兼容旧字段：HandleHeight -> HandleWidth。
        /// 仅用于反序列化历史数据，不再参与序列化输出。
        /// </summary>
        [Obsolete("Use HandleWidth instead.")]
        [JsonProperty("HandleHeight")]
        public double HandleHeight
        {
            get => HandleWidth;
            set => HandleWidth = value;
        }
        public bool ShouldSerializeHandleHeight() => false;
        public string FontFileName { get; set; } = EditorConfigDefaults.FontFileName;
        public string BigFontFileName { get; set; } = EditorConfigDefaults.BigFontFileName;
        public string BoldFontName { get; set; } = EditorConfigDefaults.BoldFontName;
        public string PreviewFontFamily { get; set; } = EditorConfigDefaults.PreviewFontFamily;

        /// <summary>CAD 样式1：0-hy-说明-T，TrueType 标题/说明</summary>
        public string StyleTName { get; set; } = EditorConfigDefaults.StyleTName;
        public string StyleTFont { get; set; } = EditorConfigDefaults.StyleTFont;
        /// <summary>CAD 样式2：0-hy-说明-S，SHX 标注/引线/表格</summary>
        public string StyleSName { get; set; } = EditorConfigDefaults.StyleSName;
        public string StyleSFont { get; set; } = EditorConfigDefaults.StyleSFont;
        public string StyleSBigFont { get; set; } = EditorConfigDefaults.StyleSBigFont;
        /// <summary>转入 CAD 时使用的样式名称（StyleTName 或 StyleSName）</summary>
        public string CadSyncStyleName { get; set; } = EditorConfigDefaults.CadSyncStyleName;

        // ── MText 显示参数 ──
        public string MTextAttachment { get; set; } = EditorConfigDefaults.MTextAttachment;
        public string MTextLineSpacingStyle { get; set; } = EditorConfigDefaults.MTextLineSpacingStyle;
        public double MTextObliquingAngle { get; set; } = EditorConfigDefaults.MTextObliquingAngle;
        public double MTextCharSpacing { get; set; } = EditorConfigDefaults.MTextCharSpacing;
        public string MTextParagraphAlign { get; set; } = EditorConfigDefaults.MTextParagraphAlign;
        public string TableBreakMode { get; set; } = EditorConfigDefaults.TableBreakMode;

        // ── 标题倍率 ──
        public double H1Scale { get; set; } = EditorConfigDefaults.H1Scale;
        public double H2Scale { get; set; } = EditorConfigDefaults.H2Scale;
        public double H3Scale { get; set; } = EditorConfigDefaults.H3Scale;
        public double LineSpacingFactor { get; set; } = EditorConfigDefaults.LineSpacingFactor;

        // ── 缩进 ──
        public double ListIndent { get; set; } = EditorConfigDefaults.ListIndent;
        public double QuoteIndent { get; set; } = EditorConfigDefaults.QuoteIndent;

        // ── 段前段后间距 ──
        public double H1SpaceBefore { get; set; } = EditorConfigDefaults.H1SpaceBefore;
        public double H1SpaceAfter { get; set; } = EditorConfigDefaults.H1SpaceAfter;
        public double H2SpaceBefore { get; set; } = EditorConfigDefaults.H2SpaceBefore;
        public double H2SpaceAfter { get; set; } = EditorConfigDefaults.H2SpaceAfter;
        public double H3SpaceBefore { get; set; } = EditorConfigDefaults.H3SpaceBefore;
        public double H3SpaceAfter { get; set; } = EditorConfigDefaults.H3SpaceAfter;
        public double PSpaceAfter { get; set; } = EditorConfigDefaults.PSpaceAfter;
        public double LiSpaceAfter { get; set; } = EditorConfigDefaults.LiSpaceAfter;
        public double QuoteSpaceBefore { get; set; } = EditorConfigDefaults.QuoteSpaceBefore;
        public double QuoteSpaceAfter { get; set; } = EditorConfigDefaults.QuoteSpaceAfter;
    }

    /// <summary>
    /// 预览统计数据（由预览 JS 返回）
    /// </summary>
    public class PreviewStats
    {
        public int SchemaVersion { get; set; } = 3;
        public int CurrentPage { get; set; } = 1;
        public int PageCount { get; set; } = 1;
        public int[] CharsPerColumn { get; set; } = Array.Empty<int>();
        public string ColumnParagraphIndicesText { get; set; } = "";
        public int[][] ColumnParagraphIndices { get; set; } = Array.Empty<int[]>();
        public PreviewColumnStats[] Columns { get; set; } = Array.Empty<PreviewColumnStats>();
        public PreviewPageStats[] Pages { get; set; } = Array.Empty<PreviewPageStats>();
        public Dictionary<string, int> BlockTypeCounts { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// 单页统计信息
    /// </summary>
    public class PreviewPageStats
    {
        public int PageIndex { get; set; }
        public int[] CharsPerColumn { get; set; } = Array.Empty<int>();
        public string ColumnParagraphIndicesText { get; set; } = "";
        public int[][] ColumnParagraphIndices { get; set; } = Array.Empty<int[]>();
        public PreviewColumnStats[] Columns { get; set; } = Array.Empty<PreviewColumnStats>();
        public Dictionary<string, int> BlockTypeCounts { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// 单栏统计信息
    /// </summary>
    public class PreviewColumnStats
    {
        public int Index { get; set; }
        public int CharsPerLine { get; set; }
        public int ParagraphCount { get; set; }
        public int TotalChars { get; set; }
        public int TotalDisplayUnits { get; set; }
        public double AvgDisplayUnitsPerChar { get; set; }
        public Dictionary<string, int> BlockTypes { get; set; } = new Dictionary<string, int>();
    }
}
