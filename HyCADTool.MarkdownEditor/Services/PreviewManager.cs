using System;
using System.Linq;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.Html;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.ViewModels;
using HyCADTool.MarkdownEditor.Views.Controls;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.MarkdownEditor.Services
{
    internal class PreviewManager
    {
        private bool _previewReady;
        private double _renderedPreviewScale = 1.0;
        private string _pendingPreviewHtml = "";
        private PreviewStats _latestPreviewStats = new PreviewStats();

        public bool IsPreviewReady
        {
            get => _previewReady;
            set => _previewReady = value;
        }

        public PreviewStats LatestPreviewStats => _latestPreviewStats ?? new PreviewStats();
        public bool HasPendingHtml => !string.IsNullOrEmpty(_pendingPreviewHtml);
        public string PendingHtml => _pendingPreviewHtml;
        public double RenderedPreviewScale => _renderedPreviewScale;

        public void ClearPendingHtml() => _pendingPreviewHtml = "";

        public void ApplyPreviewScaleStep(EditorViewModel viewModel, double step)
        {
            if (double.IsNaN(step) || double.IsInfinity(step) || Math.Abs(step) < 0.0001) return;
            double cur = viewModel.PreviewScale;
            double factor = Math.Pow(1.06, step);
            viewModel.PreviewScale = Math.Max(0.1, Math.Min(5.0, cur * factor));
        }

        public bool TryHandlePreviewWebMessage(string webMessageAsJson, EditorViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(webMessageAsJson)) return false;

            var msg = JObject.Parse(webMessageAsJson);
            string type = msg.Value<string>("type");
            if (string.Equals(type, "paperScale", StringComparison.OrdinalIgnoreCase))
            {
                double nextScale = msg.Value<double?>("value") ?? 0;
                if (nextScale <= 0) return true;
                nextScale = Math.Max(0.1, Math.Min(5.0, nextScale));
                if (Math.Abs(viewModel.PreviewScale - nextScale) < 0.001)
                    return true;

                _renderedPreviewScale = nextScale;
                viewModel.PreviewScale = nextScale;
                return true;
            }

            if (string.Equals(type, "wheelZoom", StringComparison.OrdinalIgnoreCase))
            {
                double deltaY = msg.Value<double?>("deltaY") ?? 0;
                if (Math.Abs(deltaY) < 0.01) return true;
                double step = -deltaY / 100.0;
                ApplyPreviewScaleStep(viewModel, step);
                return true;
            }

            return false;
        }

        public void UpdateRulerScale(PreviewPanelControl panel, EditorViewModel viewModel)
        {
            if (panel?.HorizontalRuler == null || panel.VerticalRuler == null) return;

            double scale = Math.Max(0.1, viewModel.PreviewScale);
            double ppm = scale;
            panel.HorizontalRuler.PixelsPerMm = Math.Max(0.01, ppm);
            panel.VerticalRuler.PixelsPerMm = Math.Max(0.01, ppm);
            panel.HorizontalRuler.SegmentCount = 1;
        }

        public void FitPaperToPreviewArea(PreviewPanelControl panel, EditorViewModel viewModel, bool previewVisible)
        {
            if (panel?.PreviewWebViewControl == null || !previewVisible) return;
            double areaWidth = panel.PreviewWebViewControl.ActualWidth;
            if (areaWidth <= 0) areaWidth = panel.PreviewGrid.ActualWidth - 24;
            if (areaWidth <= 100) return;

            double pageMm = Math.Max(100, viewModel.PageWidthMm);
            double targetScale = 0.9 * areaWidth / pageMm;
            targetScale = Math.Max(0.1, Math.Min(5.0, targetScale));
            viewModel.PreviewScale = targetScale;
        }

        public async Task TryApplyLivePreviewZoomAsync(WebView2 previewWebView, EditorViewModel viewModel, bool previewVisible)
        {
            if (!previewVisible || previewWebView?.CoreWebView2 == null) return;
            if (_renderedPreviewScale <= 0) return;

            double ratio = viewModel.PreviewScale / _renderedPreviewScale;
            ratio = Math.Max(0.2, Math.Min(8.0, ratio));
            string ratioText = ratio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"document.body.style.zoom='{ratioText}';");
        }

        public async Task RefreshPreviewAsync(WebView2 previewWebView, EditorViewModel viewModel)
        {
            var config = viewModel.BuildConfig();
            string html = PreviewHtmlRenderer.ToInteractiveHtml(
                viewModel.MarkdownText ?? "", viewModel.ColumnCount, viewModel.PreviewScale, config);
            if (!_previewReady || previewWebView?.CoreWebView2 == null)
            {
                _pendingPreviewHtml = html;
                return;
            }

            previewWebView.CoreWebView2.NavigateToString(html);
            _renderedPreviewScale = Math.Max(0.1, viewModel.PreviewScale);
            await Task.CompletedTask;
        }

        public async Task SyncFromPreviewAsync(WebView2 previewWebView, EditorViewModel viewModel)
        {
            if (!_previewReady || previewWebView?.CoreWebView2 == null)
                return;

            string raw = await previewWebView.CoreWebView2.ExecuteScriptAsync("getPreviewStats()");
            if (string.IsNullOrEmpty(raw) || raw == "null")
                return;

            string payload = raw;
            if (payload.StartsWith("\""))
                payload = JsonConvert.DeserializeObject<string>(payload) ?? "";
            if (string.IsNullOrWhiteSpace(payload))
                return;

            var stats = JObject.Parse(payload).ToObject<PreviewStats>();
            if (stats == null)
                return;

            _latestPreviewStats = stats;

            if (stats.CharsPerColumn != null && stats.CharsPerColumn.Length > 0)
            {
                var values = stats.CharsPerColumn.Where(v => v > 0).ToArray();
                if (values.Length > 0)
                    viewModel.CharsPerColumn = values;
            }

            string paraText = stats.ColumnParagraphIndicesText;
            if (string.IsNullOrWhiteSpace(paraText) && stats.ColumnParagraphIndices != null)
            {
                paraText = string.Join("|", stats.ColumnParagraphIndices
                    .Select(col => string.Join(",", (col ?? Array.Empty<int>()).Select(v => v.ToString()))));
            }

            if (!string.IsNullOrWhiteSpace(paraText))
                viewModel.ColumnParagraphIndices = paraText;
        }
    }
}
