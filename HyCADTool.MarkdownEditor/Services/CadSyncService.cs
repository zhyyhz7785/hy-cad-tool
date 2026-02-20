using System;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.ViewModels;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed class CadSyncService
    {
        private readonly EditorViewModel _viewModel;
        private readonly PreviewManager _previewManager;
        private bool _isAutoSyncRunning;
        private bool _autoSyncPending;

        public CadSyncService(EditorViewModel viewModel, PreviewManager previewManager)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _previewManager = previewManager ?? throw new ArgumentNullException(nameof(previewManager));
        }

        public async Task<EditorResult> BuildEditorResultAsync(
            bool confirmed,
            Func<Task> syncMarkdownFromEditorAsync,
            Func<PreviewRefreshReason, Task> refreshPreviewAsync,
            Func<Task> syncFromPreviewAsync)
        {
            if (syncMarkdownFromEditorAsync == null) throw new ArgumentNullException(nameof(syncMarkdownFromEditorAsync));
            if (refreshPreviewAsync == null) throw new ArgumentNullException(nameof(refreshPreviewAsync));
            if (syncFromPreviewAsync == null) throw new ArgumentNullException(nameof(syncFromPreviewAsync));

            await syncMarkdownFromEditorAsync();
            await refreshPreviewAsync(PreviewRefreshReason.ContentInput);
            await Task.Delay(200);
            await syncFromPreviewAsync();

            return new EditorResult
            {
                Confirmed = confirmed,
                Markdown = _viewModel.MarkdownText,
                Config = _viewModel.BuildConfig(),
                CharsPerColumn = _viewModel.CharsPerColumn,
                ColumnParagraphIndices = _viewModel.ColumnParagraphIndices,
                PreviewStats = _previewManager.LatestPreviewStats,
                LayoutResult = _previewManager.LatestLayoutResult
            };
        }

        public async Task TriggerLiveSyncAsync(
            Func<Task<EditorResult>> buildResultAsync,
            Action<EditorResult> setResult,
            Action<EditorResult> raiseLiveSync,
            Action scheduleAutoCadSync)
        {
            if (buildResultAsync == null) throw new ArgumentNullException(nameof(buildResultAsync));
            if (setResult == null) throw new ArgumentNullException(nameof(setResult));
            if (raiseLiveSync == null) throw new ArgumentNullException(nameof(raiseLiveSync));
            if (scheduleAutoCadSync == null) throw new ArgumentNullException(nameof(scheduleAutoCadSync));

            if (_isAutoSyncRunning)
            {
                _autoSyncPending = true;
                return;
            }

            _isAutoSyncRunning = true;
            try
            {
                var result = await buildResultAsync();
                setResult(result);
                raiseLiveSync(result);
            }
            finally
            {
                _isAutoSyncRunning = false;
            }

            if (_autoSyncPending)
            {
                _autoSyncPending = false;
                scheduleAutoCadSync();
            }
        }

        public static void RaiseManualSync(EditorResult result)
        {
            try
            {
                EditorLauncher.RaiseManualSync(JsonConvert.SerializeObject(result ?? new EditorResult()));
            }
            catch
            {
                // ignore host callback failures
            }
        }

        public static void RaiseLiveSync(EditorResult result)
        {
            try
            {
                EditorLauncher.RaiseLiveSync(JsonConvert.SerializeObject(result ?? new EditorResult()));
            }
            catch
            {
                // ignore host callback failures
            }
        }
    }
}
