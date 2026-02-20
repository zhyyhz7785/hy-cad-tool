using System;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.ViewModels;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.Services
{
    internal enum MarkdownSyncSource
    {
        Editor = 0,
        Preview = 1
    }

    internal enum EditorInputSyncResult
    {
        Ignored = 0,
        Applied = 1,
        EchoDropped = 2
    }

    internal enum ContentOwner
    {
        CSharp = 0,
        Preview = 1
    }

    internal sealed class MarkdownSyncCoordinator
    {
        private readonly EditorViewModel _viewModel;
        private ContentOwner _contentOwner = ContentOwner.CSharp;

        public MarkdownSyncCoordinator(EditorViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public ContentOwner Owner => _contentOwner;

        public void ClaimOwnership(ContentOwner owner)
        {
            _contentOwner = owner;
        }

        public EditorInputSyncResult HandleEditorInput(string markdown)
        {
            if (_contentOwner != ContentOwner.CSharp)
                return EditorInputSyncResult.Ignored;

            return ApplyMarkdownFromSource(markdown, MarkdownSyncSource.Editor)
                ? EditorInputSyncResult.Applied
                : EditorInputSyncResult.Ignored;
        }

        public bool HandlePreviewContentChanged(PreviewContentChange change, out string appliedMarkdown)
        {
            appliedMarkdown = change?.Markdown ?? string.Empty;
            if (_contentOwner != ContentOwner.Preview)
                return false;
            return change != null && ApplyMarkdownFromSource(appliedMarkdown, MarkdownSyncSource.Preview, change.Version);
        }

        public async Task SyncMarkdownFromEditorAsync(
            Func<bool> canUseEditorScriptPipeline,
            Func<string, Task<string>> executeEditorScriptAsync,
            Action<string, Exception> logger)
        {
            if (canUseEditorScriptPipeline == null || executeEditorScriptAsync == null)
                return;
            if (!canUseEditorScriptPipeline()) return;

            try
            {
                string result = await executeEditorScriptAsync("getContent()");
                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    string md = JsonConvert.DeserializeObject<string>(result);
                    if (md != null)
                        ApplyMarkdownFromSource(md, MarkdownSyncSource.Editor);
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke(nameof(SyncMarkdownFromEditorAsync), ex);
            }
        }

        public async Task SyncEditorFromPreviewAsync(
            string markdown,
            Func<bool> canUseEditorScriptPipeline,
            Func<string, Task<string>> executeEditorScriptAsync,
            Func<bool> isPreviewFocused,
            Action focusPreview,
            Action<string, Exception> logger)
        {
            if (canUseEditorScriptPipeline == null || executeEditorScriptAsync == null)
                return;
            if (!canUseEditorScriptPipeline())
                return;

            try
            {
                string escaped = JsonConvert.SerializeObject(markdown ?? string.Empty);
                bool previewHadFocus = isPreviewFocused?.Invoke() == true;
                await executeEditorScriptAsync($"setContent({escaped})");
                if (previewHadFocus)
                    focusPreview?.Invoke();
            }
            catch (Exception ex)
            {
                logger?.Invoke(nameof(SyncEditorFromPreviewAsync), ex);
            }
        }

        public async Task SyncCurrentMarkdownToEditorAsync(
            string markdown,
            bool editorReady,
            Func<string, Task<string>> executeEditorScriptAsync,
            Action<string, Exception> logger)
        {
            if (!editorReady || executeEditorScriptAsync == null)
                return;

            try
            {
                string content = markdown ?? string.Empty;
                string escaped = JsonConvert.SerializeObject(content);
                await executeEditorScriptAsync($"setContent({escaped})");
            }
            catch (Exception ex)
            {
                logger?.Invoke(nameof(SyncCurrentMarkdownToEditorAsync), ex);
            }
        }

        public void RestoreFocusAfterNavigation(
            Action<Action> beginInvokeOnUi,
            Func<Task> restoreCaretAsync)
        {
            beginInvokeOnUi?.Invoke(() =>
            {
                _ = restoreCaretAsync?.Invoke();
            });
        }

        private bool ApplyMarkdownFromSource(string markdown, MarkdownSyncSource source, long previewVersion = 0)
        {
            string next = markdown ?? string.Empty;
            _ = source;
            _ = previewVersion;

            if (string.Equals(_viewModel.MarkdownText ?? string.Empty, next, StringComparison.Ordinal))
                return false;

            _viewModel.SetMarkdownFromEditor(next);
            return true;
        }

    }
}
