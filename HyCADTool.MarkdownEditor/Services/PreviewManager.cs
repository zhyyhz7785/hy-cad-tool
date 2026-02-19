using System;
using System.Collections.Generic;
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
using LayoutSpecConfig = HyCADTool.TextLayout.DesignSpecConfig;

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

    internal class PreviewManager
    {
        private bool _previewReady;
        private double _renderedPreviewScale = 1.0;
        private string _pendingPreviewHtml = "";
        private PreviewStats _latestPreviewStats = new PreviewStats();
        private LayoutResultModel _latestLayoutResult;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private readonly LayoutEngine _layoutEngine = new LayoutEngine();
        private long _markdownVersion;
        private long _previewVersion;
        private long _lastPreviewContentVersion;
        private string _lastPreviewContentHash = string.Empty;
        private string _lastMarkdownSnapshot = string.Empty;
        private string[] _lastBlockSources = Array.Empty<string>();
        private int _dirtyBlockStart = -1;
        private int _dirtyBlockEnd = -1;
        private string _lastRenderedConfigJson;
        private bool _hasInitialRender;
        private bool _incrementalFallbackFuse;
        private DateTime _lastFullRenderUtc = DateTime.MinValue;

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

        public bool TryHandlePreviewWebMessage(string webMessageAsJson, EditorViewModel viewModel, out string markdownChanged)
        {
            markdownChanged = null;
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
                    return true;
                viewModel.SetMarkdownFromEditor(markdown);
                _markdownVersion++;
                _lastMarkdownSnapshot = markdown;
                markdownChanged = markdown;
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
            bool flowExperienceMode = PreviewHtmlRenderer.IsFlowExperiencePaperMode;
            var layoutConfig = flowExperienceMode ? null : BuildLayoutConfig(config);
            string configJson = JsonConvert.SerializeObject(config);
            bool configChanged = !string.Equals(configJson, _lastRenderedConfigJson, StringComparison.Ordinal);
            string markdown = viewModel.MarkdownText ?? string.Empty;
            if (!string.Equals(_lastMarkdownSnapshot, markdown, StringComparison.Ordinal))
            {
                _markdownVersion++;
                _lastMarkdownSnapshot = markdown;
            }

            if (flowExperienceMode)
            {
                _dirtyBlockStart = -1;
                _dirtyBlockEnd = -1;
                _latestLayoutResult = null;
            }
            else
            {
                var blocks = MarkdownBlockParser.ParseTopLevelBlocks(markdown);
                UpdateDirtyRange(blocks);
                var previousLayout = _latestLayoutResult;
                bool canIncrementalLayout = !configChanged
                    && previousLayout != null
                    && _dirtyBlockStart >= 0
                    && _dirtyBlockEnd >= 0;
                _latestLayoutResult = canIncrementalLayout
                    ? _layoutEngine.DistributeIncremental(blocks, layoutConfig, previousLayout, _dirtyBlockStart, _dirtyBlockEnd)
                    : _layoutEngine.Distribute(blocks, layoutConfig);
            }

            if (!_previewReady || previewWebView?.CoreWebView2 == null)
            {
                string html = PreviewHtmlRenderer.ToInteractiveHtml(
                    markdown, viewModel.ColumnCount, viewModel.PreviewScale, config, _latestLayoutResult);
                _pendingPreviewHtml = html;
                return;
            }
            bool forceFullByReason = reason == PreviewRefreshReason.InitialLoad
                || reason == PreviewRefreshReason.ConfigChanged
                || reason == PreviewRefreshReason.ViewportChanged
                || reason == PreviewRefreshReason.FallbackRebuild;
            bool canIncremental = _hasInitialRender
                && !configChanged
                && !forceFullByReason;

            if (reason == PreviewRefreshReason.ContentInput
                && _incrementalFallbackFuse
                && _hasInitialRender)
            {
                FullNavigatePreview(previewWebView, markdown, viewModel, config);
                _lastRenderedConfigJson = configJson;
                _incrementalFallbackFuse = false;
                _renderedPreviewScale = Math.Max(0.1, viewModel.PreviewScale);
                _previewVersion = _markdownVersion;
                UpdatePageState(1, 1);
                return;
            }

            if (canIncremental)
            {
                try
                {
                    await IncrementalUpdateAsync(
                        previewWebView,
                        markdown,
                        viewModel.PreviewScale,
                        _latestLayoutResult,
                        _dirtyBlockStart,
                        _dirtyBlockEnd);
                    _incrementalFallbackFuse = false;
                    _lastRenderedConfigJson = configJson;
                }
                catch
                {
                    FullNavigatePreview(previewWebView, markdown, viewModel, config);
                    _lastRenderedConfigJson = configJson;
                    _incrementalFallbackFuse = true;
                }
            }
            else
            {
                FullNavigatePreview(previewWebView, markdown, viewModel, config);
                _lastRenderedConfigJson = configJson;
                if (forceFullByReason || configChanged)
                    _incrementalFallbackFuse = false;
            }

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

        private async Task IncrementalUpdateAsync(
            WebView2 previewWebView,
            string markdown,
            double previewScale,
            LayoutResultModel layoutResult,
            int dirtyBlockStart,
            int dirtyBlockEnd)
        {
            string bodyHtml = PreviewHtmlRenderer.RenderBodyHtml(markdown);
            string layoutJson = layoutResult == null
                ? "null"
                : JsonConvert.SerializeObject(layoutResult);
            string customWidths = PreviewHtmlRenderer.BuildCustomColumnWidthsJson(
                layoutResult, Math.Max(0.01, previewScale));
            string incrementalMetaJson = layoutResult?.IncrementalMetadata == null
                ? "null"
                : JsonConvert.SerializeObject(layoutResult.IncrementalMetadata);
            string bodyEscaped = JsonConvert.SerializeObject(bodyHtml);

            string raw = await previewWebView.CoreWebView2.ExecuteScriptAsync(
                $"updateSourceIncremental({bodyEscaped},{layoutJson},{customWidths},{dirtyBlockStart},{dirtyBlockEnd},{incrementalMetaJson})");
            if (!ParseJsBool(raw))
                throw new InvalidOperationException("incremental patch rejected");
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

        private static LayoutSpecConfig BuildLayoutConfig(EditorConfig config)
        {
            if (config == null)
                return new LayoutSpecConfig();

            var layoutConfig = new LayoutSpecConfig
            {
                Scale = config.DrawScale > 0 ? config.DrawScale : 1.0,
                PreviewScale = config.PreviewScale,
                ColumnCount = config.ColumnCount,
                ColumnGutter = config.ColumnGutter,
                CharsPerColumn = config.CharsPerColumn ?? new[] { 28, 28 },
                LineSpacingFactor = config.LineSpacingFactor,
                H1Scale = config.H1Scale,
                H2Scale = config.H2Scale,
                H3Scale = config.H3Scale,
                H1SpaceBefore = config.H1SpaceBefore,
                H1SpaceAfter = config.H1SpaceAfter,
                H2SpaceBefore = config.H2SpaceBefore,
                H2SpaceAfter = config.H2SpaceAfter,
                H3SpaceBefore = config.H3SpaceBefore,
                H3SpaceAfter = config.H3SpaceAfter,
                PSpaceAfter = config.PSpaceAfter,
                LiSpaceAfter = config.LiSpaceAfter,
                QuoteSpaceBefore = config.QuoteSpaceBefore,
                QuoteSpaceAfter = config.QuoteSpaceAfter,
                ListIndent = Math.Max(0, config.ListIndent),
                QuoteIndent = Math.Max(0, config.QuoteIndent),
                FontFileName = config.FontFileName,
                BigFontFileName = config.BigFontFileName,
                BoldFontName = config.BoldFontName,
                TextSize = config.TextSize,
                TextXScale = Math.Max(0.1, config.TextXScale),
                TotalHeight = config.TotalHeight,
                PagePreset = config.PagePreset,
                PageWidthMm = config.PageWidthMm,
                PageHeightMm = config.PageHeightMm,
                MarginLeftMm = config.MarginLeftMm,
                MarginRightMm = config.MarginRightMm,
                MarginTopMm = config.MarginTopMm,
                MarginBottomMm = config.MarginBottomMm
            };
            layoutConfig.Normalize();
            return layoutConfig;
        }

        public async Task GoToPageAsync(WebView2 previewWebView, int page)
        {
            if (!_previewReady || previewWebView?.CoreWebView2 == null) return;
            int target = Math.Max(1, page);
            await previewWebView.CoreWebView2.ExecuteScriptAsync($"jumpToPage({target});");
        }

        public Task ShiftPageAsync(WebView2 previewWebView, int delta)
        {
            int basePage = _currentPage > 0 ? _currentPage : 1;
            return GoToPageAsync(previewWebView, basePage + delta);
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

        private void UpdateDirtyRange(IReadOnlyList<DocumentBlock> blocks)
        {
            var next = blocks == null
                ? Array.Empty<string>()
                : blocks.Select(b => b?.SourceText ?? string.Empty).ToArray();

            if (_lastBlockSources.Length == 0)
            {
                if (next.Length > 0)
                {
                    _dirtyBlockStart = 0;
                    _dirtyBlockEnd = next.Length - 1;
                }
                else
                {
                    _dirtyBlockStart = -1;
                    _dirtyBlockEnd = -1;
                }

                _lastBlockSources = next;
                return;
            }

            int start = -1;
            int end = -1;
            int maxLen = Math.Max(_lastBlockSources.Length, next.Length);
            for (int i = 0; i < maxLen; i++)
            {
                string oldValue = i < _lastBlockSources.Length ? _lastBlockSources[i] : string.Empty;
                string newValue = i < next.Length ? next[i] : string.Empty;
                if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    continue;

                if (start < 0) start = i;
                end = i;
            }

            _dirtyBlockStart = start;
            _dirtyBlockEnd = end;
            _lastBlockSources = next;
        }
    }
}
