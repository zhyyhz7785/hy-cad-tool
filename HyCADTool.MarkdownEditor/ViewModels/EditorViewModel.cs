using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.Html;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HyCADTool.MarkdownEditor.ViewModels
{
    /// <summary>
    /// Markdown WYSIWYG 编辑器 ViewModel（独立于 AutoCAD，纯 WPF）
    /// </summary>
    public class EditorViewModel : INotifyPropertyChanged
    {
        private const double DefaultDrawScale = 1.0;
        private const int DefaultColumnCount = 2;
        private const double DefaultColumnGutter = 10;
        private const double DefaultTextSize = 2.5;
        private const double DefaultTextXScale = 0.7;
        private const double DefaultPreviewScale = 1.0;
        private const double DefaultTotalHeight = 350;
        private const string DefaultPagePreset = "A2";
        private const bool DefaultIsLandscape = true;
        private static readonly int[] DefaultCharsPerColumn = new[] { 28, 28 };

        private const double DefaultH1SpaceBefore = 2.0;
        private const double DefaultH1SpaceAfter = 0.8;
        private const double DefaultH2SpaceBefore = 1.5;
        private const double DefaultH2SpaceAfter = 0.6;
        private const double DefaultH3SpaceBefore = 1.2;
        private const double DefaultH3SpaceAfter = 0.4;
        private const double DefaultPSpaceAfter = 0.5;
        private const double DefaultLiSpaceAfter = 0.2;
        private const double DefaultQuoteSpaceBefore = 0.5;
        private const double DefaultQuoteSpaceAfter = 0.5;
        private const double DefaultBorderWidth = 1;
        private const double DefaultHandleWidth = 3;
        private const double DefaultHandleActiveWidth = 6;
        private const double DefaultColumnInnerPaddingMm = 5;
        private const string DefaultFontFileName = "Microsoft YaHei";
        private const string DefaultBoldFontName = "Microsoft YaHei";
        private const string DefaultPreviewFontFamily = "Microsoft YaHei";

        private readonly EditorContentViewModel _content = new EditorContentViewModel();
        private readonly PreviewConfigViewModel _previewConfig = new PreviewConfigViewModel();
        private readonly UIStateViewModel _uiState = new UIStateViewModel();

        public EditorContentViewModel Content => _content;
        public PreviewConfigViewModel PreviewConfig => _previewConfig;
        public UIStateViewModel UiState => _uiState;

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        #endregion

        #region 事件

        /// <summary>请求将内容推送到编辑器（加载文件后触发）</summary>
        public event Action<string> EditorContentLoadRequested;

        #endregion

        #region Markdown 文本

        private string _markdownText = "";
        public string MarkdownText
        {
            get => _markdownText;
            set
            {
                if (SetProperty(ref _markdownText, value))
                {
                    _content.SetMarkdown(_markdownText);
                    UpdateStatus();
                }
            }
        }

        private string _currentFilePath = "";
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set
            {
                if (SetProperty(ref _currentFilePath, value ?? ""))
                    _content.SetCurrentFilePath(_currentFilePath);
            }
        }

        /// <summary>由 Vditor 编辑器调用，更新文本但不触发回写循环</summary>
        public void SetMarkdownFromEditor(string markdown)
        {
            if (_markdownText == markdown) return;
            _markdownText = markdown;
            _content.SetMarkdown(_markdownText);
            OnPropertyChanged(nameof(MarkdownText));
            UpdateStatus();
        }

        #endregion

        #region 可调参数

        private static readonly IReadOnlyList<string> _drawScaleOptions = new[]
        {
            "1", "5", "10", "15", "25", "50", "100", "150", "200", "250"
        };

        private double _scale = DefaultDrawScale;
        public double DrawScale
        {
            get => _scale;
            set
            {
                if (!SetProperty(ref _scale, Math.Max(0.1, value))) return;
                _previewConfig.DrawScale = _scale;
                SyncDrawScaleTextFromValue();
                UpdateStatus();
            }
        }

        public IReadOnlyList<string> DrawScaleOptions => _drawScaleOptions;

        private string _drawScaleText = "1";
        public string DrawScaleText
        {
            get => _drawScaleText;
            set
            {
                string next = (value ?? "").Trim();
                if (!SetProperty(ref _drawScaleText, next)) return;

                if (TryParseScale(next, out var parsed))
                {
                    DrawScale = parsed;
                }
            }
        }

        private int _columnCount = DefaultColumnCount;
        public int ColumnCount
        {
            get => _columnCount;
            set
            {
                if (SetProperty(ref _columnCount, Math.Max(1, Math.Min(10, value))))
                {
                    _previewConfig.ColumnCount = _columnCount;
                    UpdateStatus();
                }
            }
        }

        private int[] _charsPerColumn;
        public int[] CharsPerColumn
        {
            get => _charsPerColumn;
            set
            {
                if (SetProperty(ref _charsPerColumn, value))
                    _previewConfig.CharsPerColumn = _charsPerColumn;
            }
        }

        private string _columnParagraphIndices = "";
        public string ColumnParagraphIndices
        {
            get => _columnParagraphIndices;
            set
            {
                if (SetProperty(ref _columnParagraphIndices, value ?? ""))
                    _previewConfig.ColumnParagraphIndices = _columnParagraphIndices;
            }
        }

        private double _columnGutter = DefaultColumnGutter;
        public double ColumnGutter
        {
            get => _columnGutter;
            set
            {
                if (SetProperty(ref _columnGutter, Math.Max(0, value)))
                {
                    _previewConfig.ColumnGutter = _columnGutter;
                    UpdateStatus();
                }
            }
        }

        private double _textSize = DefaultTextSize;
        public double TextSize
        {
            get => _textSize;
            set
            {
                if (SetProperty(ref _textSize, Math.Max(0.5, value)))
                {
                    _previewConfig.TextSize = _textSize;
                    UpdateStatus();
                }
            }
        }

        private double _previewScale = DefaultPreviewScale;
        public double PreviewScale
        {
            get => _previewScale;
            set
            {
                if (SetProperty(ref _previewScale, Math.Max(0.1, Math.Min(5.0, value))))
                {
                    _previewConfig.PreviewScale = _previewScale;
                    UpdateStatus();
                }
            }
        }

        private double _totalHeight = DefaultTotalHeight;
        public double TotalHeight
        {
            get => _totalHeight;
            set
            {
                if (SetProperty(ref _totalHeight, Math.Max(20, value)))
                {
                    _previewConfig.TotalHeight = _totalHeight;
                    UpdateStatus();
                }
            }
        }

        private double _textXScale = DefaultTextXScale;
        public double TextXScale
        {
            get => _textXScale;
            set
            {
                if (SetProperty(ref _textXScale, Math.Max(0.1, value)))
                {
                    _previewConfig.TextXScale = _textXScale;
                    UpdateStatus();
                }
            }
        }

        private string _fontFileName = DefaultFontFileName;
        public string FontFileName
        {
            get => _fontFileName;
            set
            {
                if (SetProperty(ref _fontFileName, string.IsNullOrWhiteSpace(value) ? DefaultFontFileName : value.Trim()))
                    UpdateStatus();
            }
        }

        private string _bigFontFileName = "";
        public string BigFontFileName
        {
            get => _bigFontFileName;
            set
            {
                if (SetProperty(ref _bigFontFileName, (value ?? "").Trim()))
                    UpdateStatus();
            }
        }

        private string _boldFontName = DefaultBoldFontName;
        public string BoldFontName
        {
            get => _boldFontName;
            set
            {
                if (SetProperty(ref _boldFontName, string.IsNullOrWhiteSpace(value) ? DefaultBoldFontName : value.Trim()))
                    UpdateStatus();
            }
        }

        private string _previewFontFamily = DefaultPreviewFontFamily;
        public string PreviewFontFamily
        {
            get => _previewFontFamily;
            set
            {
                if (SetProperty(ref _previewFontFamily, string.IsNullOrWhiteSpace(value) ? DefaultPreviewFontFamily : value.Trim()))
                    UpdateStatus();
            }
        }

        // ── 图纸幅面定义（短边 b × 长边 l） ──
        private static readonly (string Name, double Short, double Long)[] PaperDefs = new[]
        {
            ("A0", 841.0, 1189.0),
            ("A1", 594.0, 841.0),
            ("A2", 420.0, 594.0),
            ("A3", 297.0, 420.0),
            ("A4", 210.0, 297.0),
        };

        /// <summary>下拉列表数据源：["A0","A1","A2","A3","A4"]</summary>
        public IReadOnlyList<string> PagePresets { get; } = PaperDefs.Select(d => d.Name).ToArray();

        private bool _isLandscape = DefaultIsLandscape;
        /// <summary>横向(true) / 竖向(false) 切换</summary>
        public bool IsLandscape
        {
            get => _isLandscape;
            set
            {
                if (!SetProperty(ref _isLandscape, value)) return;
                ApplyPagePreset(_pagePreset);
                OnPropertyChanged(nameof(PageOrientationLabel));
                OnPropertyChanged(nameof(PageSizeLabel));
            }
        }

        /// <summary>标题栏按钮文字："横向" 或 "竖向"</summary>
        public string PageOrientationLabel => _isLandscape ? "横" : "竖";

        /// <summary>标题栏尺寸标签：如 "594×420"</summary>
        public string PageSizeLabel => $"{PageWidthMm:F0}×{PageHeightMm:F0}";

        private string _pagePreset = DefaultPagePreset;
        public string PagePreset
        {
            get => _pagePreset;
            set
            {
                string normalized = NormalizePaperPreset(value);
                if (SetProperty(ref _pagePreset, normalized))
                {
                    ApplyPagePreset(_pagePreset);
                    OnPropertyChanged(nameof(PageSizeLabel));
                    UpdateStatus();
                }
            }
        }

        private double _pageWidthMm = 594;
        public double PageWidthMm
        {
            get => _pageWidthMm;
            set
            {
                double next = Math.Max(100, value);
                if (!SetProperty(ref _pageWidthMm, next)) return;
                OnPropertyChanged(nameof(PageSizeLabel));
                UpdateStatus();
            }
        }

        private double _pageHeightMm = 420;
        public double PageHeightMm
        {
            get => _pageHeightMm;
            set
            {
                double next = Math.Max(100, value);
                if (!SetProperty(ref _pageHeightMm, next)) return;
                OnPropertyChanged(nameof(PageSizeLabel));
                UpdateStatus();
            }
        }

        private double _marginLeftMm = 20;
        public double MarginLeftMm
        {
            get => _marginLeftMm;
            set
            {
                double next = Math.Max(0, value);
                if (!SetProperty(ref _marginLeftMm, next)) return;
                UpdateStatus();
            }
        }

        private double _marginRightMm = 20;
        public double MarginRightMm
        {
            get => _marginRightMm;
            set
            {
                double next = Math.Max(0, value);
                if (!SetProperty(ref _marginRightMm, next)) return;
                UpdateStatus();
            }
        }

        private double _marginTopMm = 5;
        public double MarginTopMm
        {
            get => _marginTopMm;
            set
            {
                double next = Math.Max(0, value);
                if (!SetProperty(ref _marginTopMm, next)) return;
                UpdateStatus();
            }
        }

        private double _columnInnerPaddingMm = DefaultColumnInnerPaddingMm;
        public double ColumnInnerPaddingMm
        {
            get => _columnInnerPaddingMm;
            set
            {
                double next = Math.Max(0, value);
                if (!SetProperty(ref _columnInnerPaddingMm, next)) return;
                UpdateStatus();
            }
        }

        private double _marginBottomMm = 20;
        public double MarginBottomMm
        {
            get => _marginBottomMm;
            set
            {
                double next = Math.Max(0, value);
                if (!SetProperty(ref _marginBottomMm, next)) return;
                UpdateStatus();
            }
        }

        private double _borderWidth = DefaultBorderWidth;
        public double BorderWidth
        {
            get => _borderWidth;
            set
            {
                if (SetProperty(ref _borderWidth, Math.Max(0.5, Math.Min(5, value))))
                    UpdateStatus();
            }
        }

        private double _handleWidth = DefaultHandleWidth;
        public double HandleWidth
        {
            get => _handleWidth;
            set
            {
                double next = Math.Max(1, Math.Min(10, value));
                if (!SetProperty(ref _handleWidth, next)) return;
                if (_handleActiveWidth < _handleWidth)
                {
                    _handleActiveWidth = _handleWidth;
                    OnPropertyChanged(nameof(HandleActiveWidth));
                }
                UpdateStatus();
            }
        }

        private double _handleActiveWidth = DefaultHandleActiveWidth;
        public double HandleActiveWidth
        {
            get => _handleActiveWidth;
            set
            {
                double next = Math.Max(HandleWidth, Math.Min(14, value));
                if (SetProperty(ref _handleActiveWidth, next))
                    UpdateStatus();
            }
        }

        // ── 段前段后间距（字高倍数） ──
        private double _h1SpaceBefore = DefaultH1SpaceBefore;
        public double H1SpaceBefore { get => _h1SpaceBefore; set { if (SetProperty(ref _h1SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h1SpaceAfter = DefaultH1SpaceAfter;
        public double H1SpaceAfter { get => _h1SpaceAfter; set { if (SetProperty(ref _h1SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h2SpaceBefore = DefaultH2SpaceBefore;
        public double H2SpaceBefore { get => _h2SpaceBefore; set { if (SetProperty(ref _h2SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h2SpaceAfter = DefaultH2SpaceAfter;
        public double H2SpaceAfter { get => _h2SpaceAfter; set { if (SetProperty(ref _h2SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h3SpaceBefore = DefaultH3SpaceBefore;
        public double H3SpaceBefore { get => _h3SpaceBefore; set { if (SetProperty(ref _h3SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h3SpaceAfter = DefaultH3SpaceAfter;
        public double H3SpaceAfter { get => _h3SpaceAfter; set { if (SetProperty(ref _h3SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _pSpaceAfter = DefaultPSpaceAfter;
        public double PSpaceAfter { get => _pSpaceAfter; set { if (SetProperty(ref _pSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _liSpaceAfter = DefaultLiSpaceAfter;
        public double LiSpaceAfter { get => _liSpaceAfter; set { if (SetProperty(ref _liSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceBefore = DefaultQuoteSpaceBefore;
        public double QuoteSpaceBefore { get => _quoteSpaceBefore; set { if (SetProperty(ref _quoteSpaceBefore, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceAfter = DefaultQuoteSpaceAfter;
        public double QuoteSpaceAfter { get => _quoteSpaceAfter; set { if (SetProperty(ref _quoteSpaceAfter, value)) RefreshPreviewVia(); } }

        private void RefreshPreviewVia() => OnPropertyChanged("SpacingChanged");

        private static bool TryParseScale(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;
            value = 1.0;
            return false;
        }

        private static string FormatScale(double scale)
        {
            double rounded = Math.Round(scale, 3);
            if (Math.Abs(rounded - Math.Round(rounded)) < 0.0001)
                return ((int)Math.Round(rounded)).ToString(CultureInfo.InvariantCulture);
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void SyncDrawScaleTextFromValue()
        {
            string next = FormatScale(_scale);
            if (_drawScaleText == next) return;
            _drawScaleText = next;
            OnPropertyChanged(nameof(DrawScaleText));
        }

        #endregion

        #region 状态

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (SetProperty(ref _statusText, value))
                    _uiState.StatusText = _statusText;
            }
        }

        private bool _dialogConfirmed;
        public bool DialogConfirmed
        {
            get => _dialogConfirmed;
            set
            {
                if (SetProperty(ref _dialogConfirmed, value))
                    _uiState.DialogConfirmed = _dialogConfirmed;
            }
        }

        #endregion

        #region 命令

        public ICommand LoadFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand EditorActionCommand { get; }

        private VditorJsHelper _jsHelper;
        public event Func<string, Task<bool>> PreviewEditorActionRequested;

        #endregion

        #region 构造

        public EditorViewModel()
        {
            LoadFileCommand = new RelayCmd(ExecuteLoad);
            SaveFileCommand = new RelayCmd(ExecuteSave);
            EditorActionCommand = new AsyncRelayCmd(ExecuteEditorActionAsync);
            _markdownText = DefaultMarkdown;
            _content.SetMarkdown(_markdownText);
            _content.SetCurrentFilePath(_currentFilePath);
            _previewConfig.ColumnCount = _columnCount;
            _previewConfig.CharsPerColumn = _charsPerColumn;
            _previewConfig.ColumnParagraphIndices = _columnParagraphIndices;
            _previewConfig.ColumnGutter = _columnGutter;
            _previewConfig.TextSize = _textSize;
            _previewConfig.TextXScale = _textXScale;
            _previewConfig.DrawScale = _scale;
            _previewConfig.PreviewScale = _previewScale;
            _previewConfig.TotalHeight = _totalHeight;
            ApplyPagePreset(_pagePreset);
            SyncDrawScaleTextFromValue();
            UpdateStatus();
        }

        public EditorViewModel(EditorInput input) : this()
        {
            if (input == null) return;
            if (!string.IsNullOrEmpty(input.Markdown))
                _markdownText = input.Markdown;
            _currentFilePath = input.CurrentFilePath ?? "";
            _content.SetMarkdown(_markdownText);
            _content.SetCurrentFilePath(_currentFilePath);

            var cfg = input.Config;
            if (cfg == null) return;

            _totalHeight = cfg.TotalHeight;
            _scale = cfg.DrawScale;
            _columnCount = Math.Max(1, Math.Min(10, cfg.ColumnCount));
            _columnGutter = cfg.ColumnGutter;
            _textSize = cfg.TextSize;
            _textXScale = cfg.TextXScale;
            _previewScale = cfg.PreviewScale;
            CharsPerColumn = cfg.CharsPerColumn;

            ParsePagePreset(cfg.PagePreset, cfg.PageWidthMm, cfg.PageHeightMm, out var preset, out var landscape);
            _pagePreset = preset;
            _isLandscape = landscape;
            ApplyPagePreset(_pagePreset);

            _h1SpaceBefore = cfg.H1SpaceBefore;
            _h1SpaceAfter = cfg.H1SpaceAfter;
            _h2SpaceBefore = cfg.H2SpaceBefore;
            _h2SpaceAfter = cfg.H2SpaceAfter;
            _h3SpaceBefore = cfg.H3SpaceBefore;
            _h3SpaceAfter = cfg.H3SpaceAfter;
            _pSpaceAfter = cfg.PSpaceAfter;
            _liSpaceAfter = cfg.LiSpaceAfter;
            _quoteSpaceBefore = cfg.QuoteSpaceBefore;
            _quoteSpaceAfter = cfg.QuoteSpaceAfter;
            _columnInnerPaddingMm = Math.Max(0, cfg.ColumnInnerPaddingMm);
            _fontFileName = string.IsNullOrWhiteSpace(cfg.FontFileName) ? DefaultFontFileName : cfg.FontFileName.Trim();
            _bigFontFileName = (cfg.BigFontFileName ?? "").Trim();
            _boldFontName = string.IsNullOrWhiteSpace(cfg.BoldFontName) ? DefaultBoldFontName : cfg.BoldFontName.Trim();
            _previewFontFamily = string.IsNullOrWhiteSpace(cfg.PreviewFontFamily) ? DefaultPreviewFontFamily : cfg.PreviewFontFamily.Trim();
            _borderWidth = Math.Max(0.5, Math.Min(5, cfg.BorderWidth));
            _handleWidth = Math.Max(1, Math.Min(10, cfg.HandleWidth));
            _handleActiveWidth = Math.Max(_handleWidth, Math.Min(14, cfg.HandleActiveWidth));

            SyncDrawScaleTextFromValue();
            _previewConfig.ColumnCount = _columnCount;
            _previewConfig.CharsPerColumn = CharsPerColumn;
            _previewConfig.ColumnParagraphIndices = _columnParagraphIndices;
            _previewConfig.ColumnGutter = _columnGutter;
            _previewConfig.TextSize = _textSize;
            _previewConfig.TextXScale = _textXScale;
            _previewConfig.DrawScale = _scale;
            _previewConfig.PreviewScale = _previewScale;
            _previewConfig.TotalHeight = _totalHeight;
            UpdateStatus();
        }

        private static string NormalizePaperPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return "A2";
            if (preset.StartsWith("A0", StringComparison.OrdinalIgnoreCase)) return "A0";
            if (preset.StartsWith("A1", StringComparison.OrdinalIgnoreCase)) return "A1";
            if (preset.StartsWith("A2", StringComparison.OrdinalIgnoreCase)) return "A2";
            if (preset.StartsWith("A3", StringComparison.OrdinalIgnoreCase)) return "A3";
            if (preset.StartsWith("A4", StringComparison.OrdinalIgnoreCase)) return "A4";
            return "A2";
        }

        private static void ParsePagePreset(string rawPreset, double pageWidthMm, double pageHeightMm, out string preset, out bool isLandscape)
        {
            preset = NormalizePaperPreset(rawPreset);
            if (!string.IsNullOrWhiteSpace(rawPreset))
            {
                if (rawPreset.Contains("竖向", StringComparison.OrdinalIgnoreCase))
                {
                    isLandscape = false;
                    return;
                }
                if (rawPreset.Contains("横向", StringComparison.OrdinalIgnoreCase))
                {
                    isLandscape = true;
                    return;
                }
            }

            if (pageWidthMm > 0 && pageHeightMm > 0)
            {
                isLandscape = pageWidthMm >= pageHeightMm;
                return;
            }

            isLandscape = true;
        }

        #endregion

        #region 配置

        public EditorConfig BuildConfig()
        {
            int[] cpc = CharsPerColumn;
            if (cpc == null || cpc.Length != ColumnCount)
            {
                int def = (cpc != null && cpc.Length > 0) ? cpc[0] : 28;
                cpc = Enumerable.Repeat(def, ColumnCount).ToArray();
            }

            return new EditorConfig
            {
                TotalHeight = TotalHeight,
                DrawScale = DrawScale,
                ColumnCount = ColumnCount,
                CharsPerColumn = cpc,
                ColumnGutter = ColumnGutter,
                TextSize = TextSize,
                TextXScale = TextXScale,
                PreviewScale = PreviewScale,
                PagePreset = $"{PagePreset}{(IsLandscape ? "横向" : "竖向")}",
                PageWidthMm = PageWidthMm,
                PageHeightMm = PageHeightMm,
                MarginLeftMm = MarginLeftMm,
                MarginRightMm = MarginRightMm,
                MarginTopMm = MarginTopMm,
                MarginBottomMm = MarginBottomMm,
                ColumnInnerPaddingMm = ColumnInnerPaddingMm,
                BorderWidth = BorderWidth,
                HandleWidth = HandleWidth,
                HandleActiveWidth = HandleActiveWidth,
                H1SpaceBefore = H1SpaceBefore,
                H1SpaceAfter = H1SpaceAfter,
                H2SpaceBefore = H2SpaceBefore,
                H2SpaceAfter = H2SpaceAfter,
                H3SpaceBefore = H3SpaceBefore,
                H3SpaceAfter = H3SpaceAfter,
                PSpaceAfter = PSpaceAfter,
                LiSpaceAfter = LiSpaceAfter,
                QuoteSpaceBefore = QuoteSpaceBefore,
                QuoteSpaceAfter = QuoteSpaceAfter,
                FontFileName = FontFileName,
                BigFontFileName = BigFontFileName,
                BoldFontName = BoldFontName,
                PreviewFontFamily = PreviewFontFamily
            };
        }

        private void UpdateStatus()
        {
            try
            {
                var cpc = CharsPerColumn ?? Enumerable.Repeat(28, ColumnCount).ToArray();
                var widths = cpc.Select(c => c * TextSize * TextXScale * DrawScale);
                string colInfo = string.Join("+", widths.Select(w => $"{w:F0}"));
                double totalWidth = widths.Sum() + ColumnGutter * DrawScale * Math.Max(0, ColumnCount - 1);
                StatusText = $"{ColumnCount}栏 | 栏宽={colInfo}mm | 总宽{totalWidth:F1}mm (1:{DrawScale})";
            }
            catch (Exception ex)
            {
                StatusText = $"错误: {ex.Message}";
            }
        }

        private void ApplyPagePreset(string preset)
        {
            string normalized = NormalizePaperPreset(preset);
            var def = PaperDefs.FirstOrDefault(d => d.Name == normalized);
            if (def.Name == null) def = PaperDefs[2]; // fallback A2

            if (_isLandscape)
            {
                PageWidthMm = def.Long;   // 横向：宽=长边
                PageHeightMm = def.Short;
            }
            else
            {
                PageWidthMm = def.Short;  // 竖向：宽=短边
                PageHeightMm = def.Long;
            }

            // 图框边距（GB/T 50001-2017 表3.1.1）：a=左侧装订边，c=上/下/右
            double a = 25;
            double c = string.Equals(normalized, "A4", StringComparison.OrdinalIgnoreCase) ? 5 : 10;
            MarginLeftMm = a;
            MarginRightMm = c;
            MarginTopMm = 5;
            MarginBottomMm = c;
        }

        public void ResetLayoutDefaults()
        {
            DrawScale = DefaultDrawScale;
            ColumnCount = DefaultColumnCount;
            ColumnGutter = DefaultColumnGutter;
            TextSize = DefaultTextSize;
            TextXScale = DefaultTextXScale;
            PreviewScale = DefaultPreviewScale;
            TotalHeight = DefaultTotalHeight;
            CharsPerColumn = (int[])DefaultCharsPerColumn.Clone();
            ColumnParagraphIndices = string.Empty;

            PagePreset = DefaultPagePreset;
            IsLandscape = DefaultIsLandscape;
            ApplyPagePreset(PagePreset);
            OnPropertyChanged(nameof(PageSizeLabel));
            OnPropertyChanged(nameof(PageOrientationLabel));

            H1SpaceBefore = DefaultH1SpaceBefore;
            H1SpaceAfter = DefaultH1SpaceAfter;
            H2SpaceBefore = DefaultH2SpaceBefore;
            H2SpaceAfter = DefaultH2SpaceAfter;
            H3SpaceBefore = DefaultH3SpaceBefore;
            H3SpaceAfter = DefaultH3SpaceAfter;
            PSpaceAfter = DefaultPSpaceAfter;
            LiSpaceAfter = DefaultLiSpaceAfter;
            QuoteSpaceBefore = DefaultQuoteSpaceBefore;
            QuoteSpaceAfter = DefaultQuoteSpaceAfter;
            ColumnInnerPaddingMm = DefaultColumnInnerPaddingMm;
            BorderWidth = DefaultBorderWidth;
            HandleWidth = DefaultHandleWidth;
            HandleActiveWidth = DefaultHandleActiveWidth;

            SyncDrawScaleTextFromValue();
            UpdateStatus();
        }

        #endregion

        #region 命令实现

        internal void SetJsHelper(VditorJsHelper jsHelper)
        {
            _jsHelper = jsHelper;
        }

        private async Task ExecuteEditorActionAsync(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            if (_jsHelper == null)
            {
                var fallback = PreviewEditorActionRequested;
                if (fallback != null)
                {
                    try
                    {
                        await fallback.Invoke(action);
                    }
                    catch (Exception ex)
                    {
                        StatusText = $"图纸面板命令失败: {ex.Message}";
                    }
                }
                return;
            }

            switch (action)
            {
                case "Undo": await _jsHelper.UndoAsync(); break;
                case "Redo": await _jsHelper.RedoAsync(); break;
                case "Cut": await _jsHelper.CutAsync(); break;
                case "Copy": await _jsHelper.CopyAsync(); break;
                case "Paste": await _jsHelper.PasteAsync(); break;
                case "SelectAll": await _jsHelper.SelectAllAsync(); break;
                case "FindReplace": await _jsHelper.FindReplaceAsync(); break;

                case "H1": await _jsHelper.InsertHeadingAsync(1); break;
                case "H2": await _jsHelper.InsertHeadingAsync(2); break;
                case "H3": await _jsHelper.InsertHeadingAsync(3); break;
                case "H4": await _jsHelper.InsertHeadingAsync(4); break;
                case "Paragraph": await _jsHelper.InsertParagraphAsync(); break;
                case "Quote": await _jsHelper.InsertQuoteAsync(); break;
                case "OrderedList": await _jsHelper.InsertOrderedListAsync(); break;
                case "UnorderedList": await _jsHelper.InsertUnorderedListAsync(); break;
                case "TaskList": await _jsHelper.InsertTaskListAsync(); break;
                case "Table": await _jsHelper.InsertTableAsync(); break;
                case "CodeBlock": await _jsHelper.InsertCodeBlockAsync(); break;
                case "MathBlock": await _jsHelper.InsertMathBlockAsync(); break;
                case "Toc": await _jsHelper.InsertTocAsync(); break;
                case "Footnote": await _jsHelper.InsertFootnoteAsync(); break;
                case "HorizontalRule": await _jsHelper.InsertHorizontalRuleAsync(); break;

                case "Bold": await _jsHelper.ToggleBoldAsync(); break;
                case "Italic": await _jsHelper.ToggleItalicAsync(); break;
                case "Strikethrough": await _jsHelper.ToggleStrikethroughAsync(); break;
                case "InlineCode": await _jsHelper.ToggleInlineCodeAsync(); break;
                case "Underline": await _jsHelper.ToggleUnderlineAsync(); break;
                case "Highlight": await _jsHelper.ToggleHighlightAsync(); break;
                case "Superscript": await _jsHelper.ToggleSuperscriptAsync(); break;
                case "Subscript": await _jsHelper.ToggleSubscriptAsync(); break;
                case "Comment": await _jsHelper.ToggleCommentAsync(); break;
                case "InlineMath": await _jsHelper.ToggleInlineMathAsync(); break;
                case "Link": await _jsHelper.InsertLinkAsync(); break;
                case "ClearFormatting": await _jsHelper.ClearFormattingAsync(); break;
            }
        }

        private void ExecuteLoad()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Markdown文件|*.md|所有文件|*.*",
                Title = "打开 Markdown 文件"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string content = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    MarkdownText = content;
                    CurrentFilePath = dlg.FileName;
                    StatusText = $"已加载: {System.IO.Path.GetFileName(dlg.FileName)}";
                    EditorContentLoadRequested?.Invoke(content);
                }
                catch (Exception ex) { StatusText = $"加载失败: {ex.Message}"; }
            }
        }

        private void ExecuteSave()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown文件|*.md|所有文件|*.*",
                Title = "保存 Markdown 文件",
                FileName = "设计说明.md"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(dlg.FileName, MarkdownText, System.Text.Encoding.UTF8);
                    CurrentFilePath = dlg.FileName;
                    StatusText = $"已保存: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex) { StatusText = $"保存失败: {ex.Message}"; }
            }
        }

        #endregion

        #region RelayCommand

        private class RelayCmd : ICommand
        {
            private readonly Action _execute;
            public RelayCmd(Action execute) => _execute = execute;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _execute();
        }

        private class AsyncRelayCmd : ICommand
        {
            private readonly Func<string, Task> _execute;

            public AsyncRelayCmd(Func<string, Task> execute) => _execute = execute;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public async void Execute(object parameter) => await _execute(parameter as string ?? "");
        }

        #endregion

        #region 默认文本

        private const string DefaultMarkdown = @"# 设计说明

## 一、工程概况

本工程位于XX市XX区，总建筑面积约XXXXm²。结构形式为框架结构，基础形式为筏板基础。工程建设单位为XXXX公司，设计单位为XXXX设计院。

## 二、设计依据

1. 《建筑结构荷载规范》GB 50009-2012
2. 《混凝土结构设计规范》GB 50010-2010
3. 《建筑抗震设计规范》GB 50011-2010
4. 《建筑地基基础设计规范》GB 50007-2011
5. 《砌体结构设计规范》GB 50003-2011

## 三、主要材料

### 3.1 混凝土

- 基础垫层：C15
- 基础底板：C35
- 柱、梁、板：C30

### 3.2 钢筋

- 纵向受力钢筋：HRB400
- 箍筋及构造钢筋：HPB300

## 四、结构设计

本工程采用钢筋混凝土框架结构体系，抗震设防烈度为7度，设计地震分组为第一组，场地类别为II类。结构安全等级为二级，设计使用年限为50年。

## 五、基础设计

基础采用筏板基础，基础埋深为-3.5m。地基承载力特征值为200kPa。基础混凝土强度等级为C35，抗渗等级为P6。

## 六、施工要求

1. 混凝土浇筑应连续进行，不得留设施工缝
2. 钢筋保护层厚度应符合规范要求
3. 模板拆除时混凝土强度应达到设计强度的75%以上
4. 混凝土养护时间不少于7天";

        #endregion
    }
}
