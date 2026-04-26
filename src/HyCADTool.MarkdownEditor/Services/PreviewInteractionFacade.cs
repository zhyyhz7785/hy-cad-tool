using System;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.ViewModels;
using HyCADTool.MarkdownEditor.Views.Controls;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed class PreviewInteractionFacade
    {
        private readonly PreviewManager _previewManager;
        private readonly PreviewPanelControl _previewPanel;
        private readonly EditorViewModel _viewModel;
        private readonly Func<bool> _isPreviewVisible;
        private readonly Func<bool> _isPaperPrimaryEditMode;
        private readonly Action<string, Exception> _log;

        public PreviewInteractionFacade(
            PreviewManager previewManager,
            PreviewPanelControl previewPanel,
            EditorViewModel viewModel,
            Func<bool> isPreviewVisible,
            Func<bool> isPaperPrimaryEditMode,
            Action<string, Exception> log)
        {
            _previewManager = previewManager;
            _previewPanel = previewPanel;
            _viewModel = viewModel;
            _isPreviewVisible = isPreviewVisible;
            _isPaperPrimaryEditMode = isPaperPrimaryEditMode;
            _log = log;
        }

        public async Task ShiftPreviewPageAsync(int delta)
        {
            try
            {
                await _previewManager.ShiftPageAsync(_previewPanel.PreviewWebViewControl, delta);
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(ShiftPreviewPageAsync), ex);
            }
        }

        public async Task JumpPreviewPageAsync(int page)
        {
            try
            {
                await _previewManager.GoToPageAsync(_previewPanel.PreviewWebViewControl, page);
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(JumpPreviewPageAsync), ex);
            }
        }

        public async Task TryApplyLivePreviewZoomAsync()
        {
            try
            {
                await _previewManager.TryApplyLivePreviewZoomAsync(
                    _previewPanel.PreviewWebViewControl,
                    _viewModel,
                    _isPreviewVisible?.Invoke() == true);
            }
            catch
            {
                // 文档尚未就绪时忽略，等待防抖后的完整刷新
            }
        }

        public async Task ApplyPaperColumnLayoutAsync()
        {
            try
            {
                await _previewManager.ApplyPaperColumnLayoutAsync(
                    _previewPanel.PreviewWebViewControl,
                    _viewModel.ColumnCount,
                    _viewModel.ColumnGutter,
                    _viewModel.PreviewScale);
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(ApplyPaperColumnLayoutAsync), ex);
            }
        }

        public async Task ApplyPaperGeometryAsync()
        {
            try
            {
                await _previewManager.ApplyPaperGeometryAsync(
                    _previewPanel.PreviewWebViewControl,
                    _viewModel.PageWidthMm,
                    _viewModel.PageHeightMm,
                    _viewModel.MarginLeftMm,
                    _viewModel.MarginRightMm,
                    _viewModel.MarginTopMm,
                    _viewModel.MarginBottomMm);
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(ApplyPaperGeometryAsync), ex);
            }
        }

        public async Task ResetPaperLayoutAsync()
        {
            try
            {
                await _previewManager.ResetPaperLayoutAsync(_previewPanel.PreviewWebViewControl);
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(ResetPaperLayoutAsync), ex);
            }
        }

        public async Task<bool> ApplyPreviewEditorActionAsync(string action, Func<Task> onSuccessAsync)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            if (_isPaperPrimaryEditMode?.Invoke() != true || _isPreviewVisible?.Invoke() != true) return false;
            if (_previewPanel?.PreviewWebViewControl?.CoreWebView2 == null) return false;

            try
            {
                string escaped = JsonConvert.SerializeObject(action);
                string raw = await _previewPanel.PreviewWebViewControl.CoreWebView2.ExecuteScriptAsync($"applyEditorAction({escaped})");
                bool handled = ParseJsBoolean(raw);
                if (!handled)
                    return false;

                if (onSuccessAsync != null)
                    await onSuccessAsync();
                return true;
            }
            catch (Exception ex)
            {
                _log?.Invoke(nameof(ApplyPreviewEditorActionAsync), ex);
                return false;
            }
        }

        private static bool ParseJsBoolean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "null")
                return false;

            string payload = raw.Trim();
            if (payload.StartsWith("\"", StringComparison.Ordinal))
                payload = JsonConvert.DeserializeObject<string>(payload) ?? string.Empty;

            return bool.TryParse(payload, out bool value) && value;
        }
    }
}
