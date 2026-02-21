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
        public double DrawScale { get; set; } = 1.0;
        public double PreviewScale { get; set; } = 1.0;

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
        public int ColumnCount { get; set; } = 2;
        public double ColumnGutter { get; set; } = 5;
        /// <summary>栏最大数量（1～99）</summary>
        public int MaxColumnCount { get; set; } = 10;
        /// <summary>最小栏宽（px）</summary>
        public int MinColumnWidthPx { get; set; } = 80;
        /// <summary>最小栏高（px）</summary>
        public int MinColumnHeightPx { get; set; } = 80;
        public int[] CharsPerColumn { get; set; } = new[] { 28, 28 };
        public double TotalHeight { get; set; } = 350;

        // ── 字体 ──
        public double TextSize { get; set; } = 2.5;
        public double TextXScale { get; set; } = 0.7;
        public string PagePreset { get; set; } = "A2横向";
        public double PageWidthMm { get; set; } = 594;
        public double PageHeightMm { get; set; } = 420;
        public double MarginLeftMm { get; set; } = 25;
        public double MarginRightMm { get; set; } = 10;
        public double MarginTopMm { get; set; } = 10;
        public double MarginBottomMm { get; set; } = 10;
        public double ColumnInnerPaddingMm { get; set; } = 5;
        public double BorderWidth { get; set; } = 1;
        public double HandleWidth { get; set; } = 3;
        public double HandleActiveWidth { get; set; } = 6;
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
        public string FontFileName { get; set; } = "Microsoft YaHei";
        public string BigFontFileName { get; set; } = "";
        public string BoldFontName { get; set; } = "Microsoft YaHei";
        public string PreviewFontFamily { get; set; } = "Microsoft YaHei";

        /// <summary>CAD 样式1：0-hy-说明-T，TrueType 标题/说明</summary>
        public string StyleTName { get; set; } = "0-hy-说明-T";
        public string StyleTFont { get; set; } = "微软雅黑";
        /// <summary>CAD 样式2：0-hy-说明-S，SHX 标注/引线/表格</summary>
        public string StyleSName { get; set; } = "0-hy-说明-S";
        public string StyleSFont { get; set; } = "tssdeng.shx";
        public string StyleSBigFont { get; set; } = "tssdchn.shx";
        /// <summary>转入 CAD 时使用的样式名称（StyleTName 或 StyleSName）</summary>
        public string CadSyncStyleName { get; set; } = "0-hy-说明-S";

        // ── MText 显示参数 ──
        /// <summary>MText 对齐点：TopLeft/TopCenter/TopRight/MiddleLeft/MiddleCenter/MiddleRight/BottomLeft/BottomCenter/BottomRight</summary>
        public string MTextAttachment { get; set; } = "TopLeft";
        /// <summary>行间距模式：AtLeast / Exactly</summary>
        public string MTextLineSpacingStyle { get; set; } = "Exactly";
        /// <summary>文字倾斜角（度），-85～85</summary>
        public double MTextObliquingAngle { get; set; } = 0;
        /// <summary>字符间距倍数，0.75～4.0</summary>
        public double MTextCharSpacing { get; set; } = 1.0;
        /// <summary>段落对齐：Left/Center/Right/Justify</summary>
        public string MTextParagraphAlign { get; set; } = "Left";

        // ── 标题倍率 ──
        public double H1Scale { get; set; } = 1.6;
        public double H2Scale { get; set; } = 1.3;
        public double H3Scale { get; set; } = 1.1;
        public double LineSpacingFactor { get; set; } = 1.2;

        // ── 缩进 ──
        public double ListIndent { get; set; } = 4;
        public double QuoteIndent { get; set; } = 5;

        // ── 段前段后间距（字高倍数） ──
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
