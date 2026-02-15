namespace HyCADTool.MarkdownEditor.ViewModels
{
    /// <summary>
    /// 负责预览排版参数状态。
    /// </summary>
    public sealed class PreviewConfigViewModel
    {
        public int ColumnCount { get; set; } = 2;
        public int[] CharsPerColumn { get; set; }
        public string ColumnParagraphIndices { get; set; } = "";
        public double ColumnGutter { get; set; }
        public double TextSize { get; set; } = 2.5;
        public double TextXScale { get; set; } = 0.7;
        public double DrawScale { get; set; } = 1.0;
        public double PreviewScale { get; set; } = 1.0;
        public double TotalHeight { get; set; } = 350;
    }
}
