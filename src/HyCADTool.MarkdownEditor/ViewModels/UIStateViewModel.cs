namespace HyCADTool.MarkdownEditor.ViewModels
{
    /// <summary>
    /// 负责窗口级 UI 状态。
    /// </summary>
    public sealed class UIStateViewModel
    {
        public string StatusText { get; set; } = "";
        public bool DialogConfirmed { get; set; }
    }
}
