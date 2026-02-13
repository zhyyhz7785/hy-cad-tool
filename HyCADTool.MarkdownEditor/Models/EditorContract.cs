namespace HyCADTool.MarkdownEditor.Models
{
    /// <summary>
    /// 编辑器输入参数（由主项目序列化为 JSON 传入）
    /// </summary>
    public class EditorInput
    {
        public string Markdown { get; set; } = "";
        public EditorConfig Config { get; set; } = new EditorConfig();
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
    }

    /// <summary>
    /// 编辑器配置（镜像 DesignSpecConfig 的 UI 可调字段）
    /// </summary>
    public class EditorConfig
    {
        // ── 出图 ──
        public double Scale { get; set; } = 1.0;
        public double PreviewScale { get; set; } = 1.0;

        // ── 栏 ──
        public int ColumnCount { get; set; } = 2;
        public double ColumnGutter { get; set; } = 0;
        public int[] CharsPerColumn { get; set; } = new[] { 28, 28 };
        public double TotalHeight { get; set; } = 350;

        // ── 字体 ──
        public double TextSize { get; set; } = 2.5;
        public double TextXScale { get; set; } = 0.7;
        public string PagePreset { get; set; } = "A3横向";
        public double PageWidthMm { get; set; } = 420;
        public double PageHeightMm { get; set; } = 297;
        public double MarginLeftMm { get; set; } = 20;
        public double MarginRightMm { get; set; } = 20;
        public double MarginTopMm { get; set; } = 20;
        public double MarginBottomMm { get; set; } = 20;
        public string FontFileName { get; set; } = "tssdeng.shx";
        public string BigFontFileName { get; set; } = "hztxt.shx";
        public string BoldFontName { get; set; } = "SimHei";

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
}
