using System;

namespace HyCADTool.MarkdownEditor.ViewModels
{
    /// <summary>
    /// 负责编辑内容与文件路径状态。
    /// </summary>
    public sealed class EditorContentViewModel
    {
        public string MarkdownText { get; private set; } = "";
        public string CurrentFilePath { get; private set; } = "";

        public event Action<string> ContentChanged;
        public event Action<string> FilePathChanged;

        public void SetMarkdown(string markdown)
        {
            string next = markdown ?? "";
            if (string.Equals(MarkdownText, next, StringComparison.Ordinal))
                return;

            MarkdownText = next;
            ContentChanged?.Invoke(next);
        }

        public void SetCurrentFilePath(string filePath)
        {
            string next = filePath ?? "";
            if (string.Equals(CurrentFilePath, next, StringComparison.Ordinal))
                return;

            CurrentFilePath = next;
            FilePathChanged?.Invoke(next);
        }
    }
}
