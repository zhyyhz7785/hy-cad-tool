using System;
using System.Security.Cryptography;
using System.Text;
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

    internal enum FocusedPanel
    {
        None = 0,
        Editor = 1,
        Preview = 2
    }

    internal sealed class MarkdownSyncCoordinator
    {
        private readonly EditorViewModel _viewModel;
        private bool _isUpdatingFromPreview;
        private string _pendingPreviewSyncHash = string.Empty;
        private FocusedPanel _lastFocusedPanel = FocusedPanel.None;

        public MarkdownSyncCoordinator(EditorViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public EditorInputSyncResult HandleEditorInput(string markdown)
        {
            string incomingHash = ComputeTextHash(markdown ?? string.Empty);
            if (_isUpdatingFromPreview)
            {
                if (string.Equals(_pendingPreviewSyncHash, incomingHash, StringComparison.Ordinal))
                {
                    ClearPendingPreviewSyncState();
                    return EditorInputSyncResult.EchoDropped;
                }

                ClearPendingPreviewSyncState();
            }

            return ApplyMarkdownFromSource(markdown, MarkdownSyncSource.Editor)
                ? EditorInputSyncResult.Applied
                : EditorInputSyncResult.Ignored;
        }

        public bool HandlePreviewContentChanged(PreviewContentChange change, out string appliedMarkdown)
        {
            appliedMarkdown = change?.Markdown ?? string.Empty;
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
            if (_isUpdatingFromPreview) return;

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
            Action<Action> beginInvokeOnUi,
            Action<string, Exception> logger)
        {
            if (canUseEditorScriptPipeline == null || executeEditorScriptAsync == null)
            {
                ClearPendingPreviewSyncState();
                return;
            }
            if (!canUseEditorScriptPipeline())
            {
                ClearPendingPreviewSyncState();
                return;
            }

            try
            {
                _isUpdatingFromPreview = true;
                _pendingPreviewSyncHash = ComputeTextHash(markdown ?? string.Empty);
                string escaped = JsonConvert.SerializeObject(markdown ?? string.Empty);
                bool previewHadFocus = isPreviewFocused?.Invoke() == true;
                await executeEditorScriptAsync($"setContent({escaped})");
                if (previewHadFocus)
                    focusPreview?.Invoke();

                string expectedHash = _pendingPreviewSyncHash;
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    beginInvokeOnUi?.Invoke(() =>
                    {
                        if (_isUpdatingFromPreview && string.Equals(_pendingPreviewSyncHash, expectedHash, StringComparison.Ordinal))
                            ClearPendingPreviewSyncState();
                    });
                });
            }
            catch (Exception ex)
            {
                ClearPendingPreviewSyncState();
                logger?.Invoke(nameof(SyncEditorFromPreviewAsync), ex);
            }
        }

        public async Task SyncCurrentMarkdownToEditorAsync(
            string markdown,
            bool editorReady,
            Func<string, Task<string>> executeEditorScriptAsync,
            Action<Action> beginInvokeOnUi,
            Action<string, Exception> logger)
        {
            if (!editorReady || executeEditorScriptAsync == null)
                return;

            try
            {
                string content = markdown ?? string.Empty;
                string escaped = JsonConvert.SerializeObject(content);
                _isUpdatingFromPreview = true;
                _pendingPreviewSyncHash = ComputeTextHash(content);
                await executeEditorScriptAsync($"setContent({escaped})");

                string expectedHash = _pendingPreviewSyncHash;
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    beginInvokeOnUi?.Invoke(() =>
                    {
                        if (_isUpdatingFromPreview && string.Equals(_pendingPreviewSyncHash, expectedHash, StringComparison.Ordinal))
                            ClearPendingPreviewSyncState();
                    });
                });
            }
            catch (Exception ex)
            {
                ClearPendingPreviewSyncState();
                logger?.Invoke(nameof(SyncCurrentMarkdownToEditorAsync), ex);
            }
        }

        public void SaveFocusState(bool editorFocused, bool previewFocused)
        {
            if (editorFocused)
                _lastFocusedPanel = FocusedPanel.Editor;
            else if (previewFocused)
                _lastFocusedPanel = FocusedPanel.Preview;
            else
                _lastFocusedPanel = FocusedPanel.None;
        }

        public void RestoreFocusAfterNavigation(
            Action<Action> beginInvokeOnUi,
            bool editorVisible,
            bool previewVisible,
            Action focusEditor,
            Action focusPreview,
            Func<Task> restoreCaretAsync)
        {
            beginInvokeOnUi?.Invoke(() =>
            {
                switch (_lastFocusedPanel)
                {
                    case FocusedPanel.Editor:
                        if (editorVisible)
                            focusEditor?.Invoke();
                        break;
                    case FocusedPanel.Preview:
                        if (previewVisible)
                            focusPreview?.Invoke();
                        break;
                }

                _lastFocusedPanel = FocusedPanel.None;
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

        private static string ComputeTextHash(string text)
        {
            string safe = text ?? string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(safe);
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private void ClearPendingPreviewSyncState()
        {
            _isUpdatingFromPreview = false;
            _pendingPreviewSyncHash = string.Empty;
        }
    }
}
