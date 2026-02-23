using System;
using System.Threading.Tasks;

namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// Vditor JS Interop 封装层。
    /// 所有 C# → Vditor 的调用都经过此类，统一管理 JS 函数名和参数。
    /// </summary>
    internal class VditorJsHelper
    {
        private readonly Func<string, Task<string>> _exec;

        public VditorJsHelper(Func<string, Task<string>> executeScriptAsync)
        {
            _exec = executeScriptAsync ?? throw new ArgumentNullException(nameof(executeScriptAsync));
        }

        // ═══ 编辑 ═══

        public Task UndoAsync() => _exec("vditor.undo()");
        public Task RedoAsync() => _exec("vditor.redo()");
        public Task FocusAsync() => _exec("vditor.focus()");

        public Task CutAsync() => _exec("document.execCommand('cut')");
        public Task CopyAsync() => _exec("document.execCommand('copy')");
        public Task PasteAsync() => _exec("document.execCommand('paste')");
        public Task SelectAllAsync() => _exec("document.execCommand('selectAll')");

        /// <summary>获取当前 Markdown 文本（用于 "复制为 Markdown"）</summary>
        public async Task<string> GetContentAsync()
        {
            string result = await _exec("getContent()");
            if (string.IsNullOrEmpty(result) || result == "null") return "";
            try { return Newtonsoft.Json.JsonConvert.DeserializeObject<string>(result) ?? ""; }
            catch { return ""; }
        }

        /// <summary>设置编辑器内容</summary>
        public Task SetContentAsync(string markdown)
        {
            string escaped = Newtonsoft.Json.JsonConvert.SerializeObject(markdown ?? "");
            return _exec($"setContent({escaped})");
        }

        // ═══ 段落 ═══

        public Task InsertHeadingAsync(int level) => _exec($"insertHeading({level})");
        public Task InsertParagraphAsync() => _exec("insertParagraph()");
        public Task InsertTableAsync() => _exec("clickToolbar('table')");
        public Task InsertCodeBlockAsync() => _exec("clickToolbar('code')");
        public Task InsertQuoteAsync() => _exec("clickToolbar('quote')");
        public Task InsertOrderedListAsync() => _exec("clickToolbar('ordered-list')");
        public Task InsertUnorderedListAsync() => _exec("clickToolbar('list')");
        public Task InsertTaskListAsync() => _exec("clickToolbar('check')");
        public Task InsertHorizontalRuleAsync() => _exec("clickToolbar('line')");
        public Task InsertTocAsync() => _exec("vditor.insertValue('\\n[toc]\\n')");
        public Task InsertFootnoteAsync() => _exec("vditor.insertValue('[^1]\\n\\n[^1]: ')");
        public Task InsertMathBlockAsync() => _exec("vditor.insertValue('\\n$$\\n\\n$$\\n')");

        public Task PromoteHeadingAsync() => _exec("promoteHeading()");
        public Task DemoteHeadingAsync() => _exec("demoteHeading()");
        public Task IndentListAsync() => _exec("indentList()");
        public Task OutdentListAsync() => _exec("outdentList()");
        public Task InsertParagraphAboveAsync() => _exec("insertParagraphAbove()");
        public Task InsertParagraphBelowAsync() => _exec("insertParagraphBelow()");
        public Task ToggleTaskStatusAsync() => _exec("toggleTaskStatus()");
        public Task InsertImageAsync() => _exec("insertImage('', '')");
        public Task InsertLinkReferenceAsync() => _exec("insertLinkReference()");

        // ═══ 格式 ═══

        public Task ToggleBoldAsync() => _exec("clickToolbar('bold')");
        public Task ToggleItalicAsync() => _exec("clickToolbar('italic')");
        public Task ToggleStrikethroughAsync() => _exec("clickToolbar('strike')");
        public Task ToggleInlineCodeAsync() => _exec("clickToolbar('inline-code')");
        public Task InsertLinkAsync() => _exec("clickToolbar('link')");

        public Task ToggleUnderlineAsync() => _exec("wrapSelection('<u>', '</u>')");
        public Task ToggleHighlightAsync() => _exec("wrapSelection('==', '==')");
        public Task ToggleSuperscriptAsync() => _exec("wrapSelection('<sup>', '</sup>')");
        public Task ToggleSubscriptAsync() => _exec("wrapSelection('<sub>', '</sub>')");
        public Task ToggleCommentAsync() => _exec("wrapSelection('<!-- ', ' -->')");
        public Task ToggleInlineMathAsync() => _exec("wrapSelection('$', '$')");
        public Task ClearFormattingAsync() => _exec("clearFormatting()");

        // ═══ 查找 ═══

        public Task FindReplaceAsync() => _exec("triggerFind()");

        // ═══ 主题 ═══

        public Task SwitchThemeAsync(bool isDark) => _exec($"switchEditorTheme({(isDark ? "true" : "false")})");
    }
}
