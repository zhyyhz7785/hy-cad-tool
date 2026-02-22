using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.Html;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.ViewModels;
using HyCADTool.MarkdownEditor.Views.Controls;
using HyCADTool.TextLayout;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;

namespace HyCADTool.MarkdownEditor.Services
{
    internal enum PreviewRefreshReason
    {
        InitialLoad = 0,
        ContentInput = 1,
        ConfigChanged = 2,
        ViewportChanged = 3,
        FallbackRebuild = 4
    }

    internal sealed class PreviewContentChange
    {
        public string Markdown { get; init; } = string.Empty;
        public long Version { get; init; }
        public string Hash { get; init; } = string.Empty;
    }

    internal class PreviewManager
    {
        private bool _previewReady;
        private double _renderedPreviewScale = 1.0;
        private string _pendingPreviewHtml = "";
        private PreviewStats _latestPreviewStats = new PreviewStats();
        private LayoutResultModel _latestLayoutResult;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private long _markdownVersion;
        private long _previewVersion;
        private long _lastPreviewContentVersion;
        private string _lastPreviewContentHash = string.Empty;
        private string _lastMarkdownSnapshot = string.Empty;
        private int _dirtyBlockStart = -1;
        private int _dirtyBlockEnd = -1;
        private string _lastRenderedConfigJson;
        private bool _hasInitialRender;
        private bool _incrementalFallbackFuse;
        private DateTime _lastFullRenderUtc = DateTime.MinValue;
        private string _pendingCaretBookmarkJson = string.Empty;
        private double? _pendingScrollTop;
        private double? _pendingScrollLeft;

        public bool IsPreviewReady
        {
            get => _previewReady;
            set => _previewReady = value;
        }

        public PreviewStats LatestPreviewStats => _latestPreviewStats ?? new PreviewStats();
        public LayoutResultModel LatestLayoutResult => _latestLayoutResult;
        public bool HasPendingHtml => !string.IsNullOrEmpty(_pendingPreviewHtml);
        public string PendingHtml => _pendingPreviewHtml;
        public double RenderedPreviewScale => _renderedPreviewScale;
        public int CurrentPage => _currentPage;
        public int TotalPages => _totalPages;
        public long MarkdownVersion => _markdownVersion;
        public long PreviewVersion => _previewVersion;
        public int DirtyBlockStart => _dirtyBlockStart;
        public int DirtyBlockEnd => _dirtyBlockEnd;
        public long LastPreviewContentVersion => _lastPreviewContentVersion;

        public void ClearPendingHtml() => _pendingPreviewHtml = "";

        public void ApplyPreviewScaleStep(EditorViewModel viewModel, double step)
        {
            if (double.IsNaN(step) || double.IsInfinity(step) || Math.Abs(step) < 0.0001) return;
            double cur = viewModel.PreviewScale;
            double factor = Math.Pow(1.06, step);
            viewModel.PreviewScale = Math.Max(0.1, Math.Min(5.0, cur * factor));
        }

        public bool TryHandlePreviewWebMessage(string webMessageAsJson, EditorViewModel viewModel, out PreviewContentChange contentChanged)
        {
            contentChanged = null;
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

            if (string.Equals(type, "pageState", StringComparison.OrdinalIgnoreCase))
            {
                int current = msg.Value<int?>("currentPage") ?? 1;
                int total = msg.Value<int?>("pageCount") ?? 1;
                UpdatePageState(current, total);
                return true;
            }

            if (string.Equals(type, "paperGeometry", StringComparison.OrdinalIgnoreCase))
            {
                double widthMm = msg.Value<double?>("pageWidthMm") ?? 0;
                double heightMm = msg.Value<double?>("pageHeightMm") ?? 0;
                if (widthMm > 0) viewModel.PageWidthMm = widthMm;
                if (heightMm > 0) viewModel.PageHeightMm = heightMm;
                return true;
            }

            if (string.Equals(type, "paperMargins", StringComparison.OrdinalIgnoreCase))
            {
                double leftMm = msg.Value<double?>("marginLeftMm") ?? 0;
                double rightMm = msg.Value<double?>("marginRightMm") ?? 0;
                double topMm = msg.Value<double?>("marginTopMm") ?? 0;
                double bottomMm = msg.Value<double?>("marginBottomMm") ?? 0;
                viewModel.MarginLeftMm = leftMm;
                viewModel.MarginRightMm = rightMm;
                viewModel.MarginTopMm = topMm;
                viewModel.MarginBottomMm = bottomMm;
                return true;
            }

            if (string.Equals(type, "contentChanged", StringComparison.OrdinalIgnoreCase))
            {
                string markdown = msg.Value<string>("markdown") ?? "";
                int blockCount = msg.Value<int?>("blockCount") ?? 0;
                long previewContentVersion = msg.Value<long?>("version") ?? 0;
                string previewContentHash = msg.Value<string>("hash") ?? string.Empty;
                if (blockCount > 0 && string.IsNullOrWhiteSpace(markdown))
                {
                    markdown = _lastMarkdownSnapshot ?? viewModel.MarkdownText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(markdown))
                        return true;
                }
                bool duplicated = !string.IsNullOrWhiteSpace(previewContentHash)
                    && string.Equals(previewContentHash, _lastPreviewContentHash, StringComparison.Ordinal)
                    && previewContentVersion <= _lastPreviewContentVersion;
                if (duplicated)
                    return true;

                if (!string.IsNullOrWhiteSpace(previewContentHash))
                    _lastPreviewContentHash = previewContentHash;
                if (previewContentVersion > 0)
                {
                    _lastPreviewContentVersion = Math.Max(_lastPreviewContentVersion, previewContentVersion);
                    _previewVersion = Math.Max(_previewVersion, _lastPreviewContentVersion);
                }

                if (string.Equals(viewModel.MarkdownText ?? "", markdown, StringComparison.Ordinal))
                {
                    _lastMarkdownSnapshot = markdown;
                    return true;
                }
                _markdownVersion++;
                _lastMarkdownSnapshot = markdown;
                contentChanged = new PreviewContentChange
                {
                    Markdown = markdown,
                    Version = previewContentVersion,
                    Hash = previewContentHash
                };
                return true;
            }

            return false;
        }

        private void UpdatePageState(int currentPage, int totalPages)
        {
            _totalPages = Math.Max(1, totalPages);
            _currentPage = Math.Max(1, Math.Min(_totalPages, currentPage));
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

        public async Task RefreshPreviewAsync(
            WebView2 previewWebView,
            EditorViewModel viewModel,
            PreviewRefreshReason reason = PreviewRefreshReason.ConfigChanged)
        {
            var config = viewModel.BuildConfig();
            string configJson = JsonConvert.SerializeObject(config);
            bool configChanged = !string.Equals(configJson, _lastRenderedConfigJson, StringComparison.Ordinal);
            string markdown = viewModel.MarkdownText ?? string.Empty;
            if (!string.Equals(_lastMarkdownSnapshot, markdown, StringComparison.Ordinal))
            {
                _markdownVersion++;
                _lastMarkdownSnapshot = markdown;
            }
            _dirtyBlockStart = -1;
            _dirtyBlockEnd = -1;
            _latestLayoutResult = null;

            if (!_previewReady || previewWebView?.CoreWebView2 == null)
            {
                string html = PreviewHtmlRenderer.ToInteractiveHtml(
                    markdown, viewModel.ColumnCount, viewModel.PreviewScale, config, _latestLayoutResult);
                _pendingPreviewHtml = html;
                return;
            }

            // 内容变更：优先原地更新 #source，避免 NavigateToString 销毁 DOM。
            if (_hasInitialRender
                && !configChanged
                && reason == PreviewRefreshReason.ContentInput)
            {
                try
                {
                    bool ok = await IncrementalUpdateFlowAsync(previewWebView, markdown);
                    if (ok)
                    {
                        _lastRenderedConfigJson = configJson;
                        _renderedPreviewScale = Math.Max(0.1, viewModel.PreviewScale);
                        _previewVersion = _markdownVersion;
                        return;
                    }
                }
                catch { /* 失败则 fall through 到完整刷新 */ }
            }

            // 配置变更（字高、字宽、图框、栏内边距、拉伸条、段间距等）必须全量导航：
            // FullUpdateInPlaceAsync 只更新 body/layout，不更新 CSS 变量，原地更新无法生效。
            // 因此 configChanged 时跳过原地更新，直接走 FullNavigatePreview。

            await CaptureCaretAndScrollAsync(previewWebView);
            FullNavigatePreview(previewWebView, markdown, viewModel, config);
            _lastRenderedConfigJson = configJson;
            _incrementalFallbackFuse = false;

            _renderedPreviewScale = Math.Max(0.1, viewModel.PreviewScale);
            _previewVersion = _markdownVersion;
            UpdatePageState(1, 1);
        }

        private void FullNavigatePreview(WebView2 previewWebView, string markdown, EditorViewModel viewModel, EditorConfig config)
        {
            string html = PreviewHtmlRenderer.ToInteractiveHtml(
                markdown, viewModel.ColumnCount, viewModel.PreviewScale, config, _latestLayoutResult);
            previewWebView.CoreWebView2.NavigateToString(html);
            _hasInitialRender = true;
            _lastFullRenderUtc = DateTime.UtcNow;
        }

        private async Task CaptureCaretAndScrollAsync(WebView2 previewWebView)
        {
            if (!_previewReady || previewWebView?.CoreWebView2 == null) return;
            try
            {
                _pendingCaretBookmarkJson = await previewWebView.CoreWebView2.ExecuteScriptAsync(
                    "JSON.stringify((function(){var col=getActionTargetColumn();var bm=captureCaretBookmark(col);return bm||window._lastKnownCaret||null;})())");
                string scrollRaw = await previewWebView.CoreWebView2.ExecuteScriptAsync(
                    "JSON.stringify((function(){if(window.viewportEl){return {top:viewportEl.scrollTop,left:viewportEl.scrollLeft};}return window._lastKnownScroll||{top:0,left:0};})())");
                var pos = JsonConvert.DeserializeObject<ScrollPos>(scrollRaw ?? string.Empty);
                _pendingScrollTop = pos?.top;
                _pendingScrollLeft = pos?.left;
            }
            catch
            {
                _pendingCaretBookmarkJson = string.Empty;
                _pendingScrollTop = null;
                _pendingScrollLeft = null;
            }
        }

        public async Task RestoreCaretAfterNavigationAsync(WebView2 previewWebView)
        {
            var bookmarkJson = _pendingCaretBookmarkJson;
            var top = _pendingScrollTop;
            var left = _pendingScrollLeft;
            _pendingCaretBookmarkJson = string.Empty;
            _pendingScrollTop = null;
            _pendingScrollLeft = null;

            if (!_previewReady || previewWebView?.CoreWebView2 == null) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(bookmarkJson) && !string.Equals(bookmarkJson, "null", StringComparison.OrdinalIgnoreCase))
                {
                    await previewWebView.CoreWebView2.ExecuteScriptAsync(
                        "(function(){"
                        + "var payload=" + bookmarkJson + ";"
                        + "if(typeof payload==='string'){try{payload=JSON.parse(payload);}catch(_){payload=null;}}"
                        + "var doRestore=function(){if(payload){restoreCaretBookmark(payload);}};"
                        + "if(typeof requestAnimationFrame==='function'){requestAnimationFrame(function(){setTimeout(doRestore,50);});}"
                        + "else{setTimeout(doRestore,100);}"
                        + "})();");
                }
                if (top.HasValue || left.HasValue)
                {
                    string topText = (top ?? 0).ToString("0.###", CultureInfo.InvariantCulture);
                    string leftText = (left ?? 0).ToString("0.###", CultureInfo.InvariantCulture);
                    await previewWebView.CoreWebView2.ExecuteScriptAsync(
                        "(function(){"
                        + "var doRestore=function(){if(window.viewportEl){viewportEl.scrollTop=" + topText + ";viewportEl.scrollLeft=" + leftText + ";}};"
                        + "if(typeof requestAnimationFrame==='function'){requestAnimationFrame(function(){setTimeout(doRestore,50);});}"
                        + "else{setTimeout(doRestore,100);}"
                        + "})();");
                }
            }
            catch
            {
                // best effort restore; ignore failures
            }
        }

        private async Task<bool> FullUpdateInPlaceAsync(
            WebView2 previewWebView,
            string markdown,
            double previewScale,
            LayoutResultModel layoutResult)
        {
            if (!_previewReady || previewWebView?.CoreWebView2 == null)
                return false;

            string bodyHtml = PreviewHtmlRenderer.RenderBodyHtml(markdown);
            string layoutJson = layoutResult == null
                ? "null"
                : JsonConvert.SerializeObject(layoutResult);
            string customWidths = PreviewHtmlRenderer.BuildCustomColumnWidthsJson(
                layoutResult, Math.Max(0.01, previewScale));
            string bodyEscaped = JsonConvert.SerializeObject(bodyHtml);

            string raw = await previewWebView.CoreWebView2.ExecuteScriptAsync(
                $"updateSource({bodyEscaped},{layoutJson},{customWidths})");
            return ParseJsBool(raw);
        }

        /// <summary>
        /// Flow 模式原地增量更新：仅更新 #source 内容并重建分栏，不销毁 DOM。
        /// </summary>
        private async Task<bool> IncrementalUpdateFlowAsync(WebView2 previewWebView, string markdown)
        {
            string bodyHtml = PreviewHtmlRenderer.RenderFlowBodyHtml(markdown);
            string bodyEscaped = JsonConvert.SerializeObject(bodyHtml);
            string raw = await previewWebView.CoreWebView2.ExecuteScriptAsync(
                $"updateSource({bodyEscaped},null,null)");
            return ParseJsBool(raw);
        }

        private static bool ParseJsBool(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "null")
                return false;
            string payload = raw.Trim();
            if (payload.StartsWith("\"", StringComparison.Ordinal))
            {
                payload = JsonConvert.DeserializeObject<string>(payload) ?? string.Empty;
            }
            return bool.TryParse(payload, out bool value) && value;
        }

        public async Task GoToPageAsync(WebView2 previewWebView, int page)
        {
            if (!_previewReady || previewWebView?.CoreWebView2 == null) return;
            int target = Math.Max(1, page);
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"jumpToPage({target});");
        }

        public async Task ApplyPaperColumnLayoutAsync(WebView2 previewWebView, int columnCount, double columnGutter, double previewScale)
        {
            if (previewWebView?.CoreWebView2 == null)
                return;

            int nextCount = Math.Max(1, columnCount);
            double gapPx = ComputePaperColumnGapPx(columnGutter, previewScale);
            string countJson = JsonConvert.SerializeObject(nextCount);
            string gapJson = JsonConvert.SerializeObject(Math.Round(gapPx, 2));
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"setPaperColumnLayout({countJson}, {gapJson})");
        }

        public async Task ApplyPaperGeometryAsync(
            WebView2 previewWebView,
            double pageWidthMm,
            double pageHeightMm,
            double marginLeftMm,
            double marginRightMm,
            double marginTopMm,
            double marginBottomMm)
        {
            if (previewWebView?.CoreWebView2 == null)
                return;

            string widthMmJson = JsonConvert.SerializeObject(Math.Round(pageWidthMm, 3));
            string heightMmJson = JsonConvert.SerializeObject(Math.Round(pageHeightMm, 3));
            string leftMmJson = JsonConvert.SerializeObject(Math.Round(marginLeftMm, 3));
            string rightMmJson = JsonConvert.SerializeObject(Math.Round(marginRightMm, 3));
            string topMmJson = JsonConvert.SerializeObject(Math.Round(marginTopMm, 3));
            string bottomMmJson = JsonConvert.SerializeObject(Math.Round(marginBottomMm, 3));

            await previewWebView.CoreWebView2.ExecuteScriptAsync($"setPaperGeometry({widthMmJson}, {heightMmJson})");
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"setPaperMargins({leftMmJson}, {rightMmJson}, {topMmJson}, {bottomMmJson})");
        }

        public async Task ApplyColumnHeightSyncAsync(WebView2 previewWebView, bool isSync)
        {
            if (previewWebView?.CoreWebView2 == null)
                return;

            string syncJson = JsonConvert.SerializeObject(isSync);
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"setColumnHeightSync({syncJson})");
        }

        public async Task ResetPaperLayoutAsync(WebView2 previewWebView)
        {
            if (previewWebView?.CoreWebView2 == null)
                return;

            await previewWebView.CoreWebView2.ExecuteScriptAsync("if(window.resetPaperLayout){resetPaperLayout();}");
        }

        public Task ShiftPageAsync(WebView2 previewWebView, int delta)
        {
            int basePage = _currentPage > 0 ? _currentPage : 1;
            return GoToPageAsync(previewWebView, basePage + delta);
        }

        private static double ComputePaperColumnGapPx(double columnGutter, double previewScale)
        {
            double safeGutter = Math.Max(0, columnGutter);
            double safeScale = Math.Max(0.1, previewScale);
            double px = safeGutter * safeScale;
            return Math.Max(6, Math.Min(240, px));
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
            UpdatePageState(stats.CurrentPage, stats.PageCount);

            int[] charsPerColumn = stats.CharsPerColumn;
            if ((charsPerColumn == null || charsPerColumn.Length == 0)
                && stats.Pages != null
                && stats.Pages.Length > 0)
            {
                charsPerColumn = stats.Pages[0]?.CharsPerColumn;
            }

            if (charsPerColumn != null && charsPerColumn.Length > 0)
            {
                var values = charsPerColumn.Where(v => v > 0).ToArray();
                if (values.Length > 0)
                    viewModel.CharsPerColumn = values;
            }

            string paraText = stats.ColumnParagraphIndicesText;
            if (string.IsNullOrWhiteSpace(paraText)
                && stats.Pages != null
                && stats.Pages.Length > 0)
            {
                paraText = stats.Pages[0]?.ColumnParagraphIndicesText;
                if (string.IsNullOrWhiteSpace(paraText))
                    paraText = BuildParagraphText(stats.Pages[0]?.ColumnParagraphIndices);
            }

            if (string.IsNullOrWhiteSpace(paraText) && stats.ColumnParagraphIndices != null)
            {
                paraText = BuildParagraphText(stats.ColumnParagraphIndices);
            }

            if (!string.IsNullOrWhiteSpace(paraText))
                viewModel.ColumnParagraphIndices = paraText;
        }

        private static string BuildParagraphText(int[][] columns)
        {
            if (columns == null || columns.Length == 0) return string.Empty;
            return string.Join("|", columns
                .Select(col => string.Join(",", (col ?? Array.Empty<int>()).Select(v => v.ToString()))));
        }

        private sealed class ScrollPos
        {
            public double top { get; set; }
            public double left { get; set; }
        }
    }
}
