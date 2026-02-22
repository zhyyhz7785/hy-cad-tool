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
        private const string DefaultPagePreset = "A2";
        private const bool DefaultIsLandscape = true;

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

        /// <summary>恢复默认后触发，供外部持久化</summary>
        public event Action RestoredToDefaults;

        /// <summary>请求将当前配置存为默认（editor-config.json）</summary>
        public event Action SaveAsDefaultRequested;

        /// <summary>请求将当前配置存为设置文档（用户选择路径）</summary>
        public event Action SaveToFileRequested;

        /// <summary>请求从设置文档读取配置</summary>
        public event Action LoadFromFileRequested;

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
            "1", "5", "10", "15", "20", "25", "30", "50", "75", "100",
            "125", "150", "175", "200", "250", "300", "350", "400", "450", "500"
        };

        private double _scale = EditorConfigDefaults.DrawScale;
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

        private int _columnCount = EditorConfigDefaults.ColumnCount;
        public int ColumnCount
        {
            get => _columnCount;
            set
            {
                int maxCol = Math.Max(1, Math.Min(99, _maxColumnCount));
                if (SetProperty(ref _columnCount, Math.Max(1, Math.Min(maxCol, value))))
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

        private int _maxColumnCount = EditorConfigDefaults.MaxColumnCount;
        public int MaxColumnCount
        {
            get => _maxColumnCount;
            set
            {
                if (SetProperty(ref _maxColumnCount, Math.Max(1, Math.Min(99, value))))
                {
                    int maxCol = Math.Max(1, Math.Min(99, _maxColumnCount));
                    if (_columnCount > maxCol)
                    {
                        _columnCount = maxCol;
                        OnPropertyChanged(nameof(ColumnCount));
                        _previewConfig.ColumnCount = _columnCount;
                    }
                    UpdateStatus();
                }
            }
        }

        private int _minColumnWidthPx = EditorConfigDefaults.MinColumnWidthPx;
        public int MinColumnWidthPx
        {
            get => _minColumnWidthPx;
            set
            {
                if (SetProperty(ref _minColumnWidthPx, Math.Max(20, Math.Min(500, value))))
                    UpdateStatus();
            }
        }

        private int _minColumnHeightPx = EditorConfigDefaults.MinColumnHeightPx;
        public int MinColumnHeightPx
        {
            get => _minColumnHeightPx;
            set
            {
                if (SetProperty(ref _minColumnHeightPx, Math.Max(20, Math.Min(500, value))))
                    UpdateStatus();
            }
        }

        private double _columnGutter = EditorConfigDefaults.ColumnGutter;
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

        private double _textSize = EditorConfigDefaults.TextSize;
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

        private double _previewScale = EditorConfigDefaults.PreviewScale;
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

        private double _totalHeight = EditorConfigDefaults.TotalHeight;
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

        private double _textXScale = EditorConfigDefaults.TextXScale;
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

        private string _fontFileName = EditorConfigDefaults.FontFileName;
        public string FontFileName
        {
            get => _fontFileName;
            set
            {
                if (SetProperty(ref _fontFileName, string.IsNullOrWhiteSpace(value) ? EditorConfigDefaults.FontFileName : value.Trim()))
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

        private string _boldFontName = EditorConfigDefaults.BoldFontName;
        public string BoldFontName
        {
            get => _boldFontName;
            set
            {
                if (SetProperty(ref _boldFontName, string.IsNullOrWhiteSpace(value) ? EditorConfigDefaults.BoldFontName : value.Trim()))
                    UpdateStatus();
            }
        }

        private string _previewFontFamily = EditorConfigDefaults.PreviewFontFamily;
        public string PreviewFontFamily
        {
            get => _previewFontFamily;
            set
            {
                if (SetProperty(ref _previewFontFamily, string.IsNullOrWhiteSpace(value) ? EditorConfigDefaults.PreviewFontFamily : value.Trim()))
                    UpdateStatus();
            }
        }

        // ── CAD 文字样式（同步到 AutoCAD 时使用） ──
        private string _styleTName = "0-hy-说明-T";
        public string StyleTName
        {
            get => _styleTName;
            set
            {
                if (SetProperty(ref _styleTName, (value ?? "").Trim()))
                {
                    OnPropertyChanged(nameof(CadStyleNames));
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
                }
            }
        }

        private string _styleTFont = "微软雅黑";
        public string StyleTFont
        {
            get => _styleTFont;
            set
            {
                if (SetProperty(ref _styleTFont, string.IsNullOrWhiteSpace(value) ? "微软雅黑" : value.Trim()))
                {
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
                    OnPropertyChanged(nameof(StyleTFontOptionsWithCurrent));
                }
            }
        }

        private string _styleSName = "0-hy-说明-S";
        public string StyleSName
        {
            get => _styleSName;
            set
            {
                if (SetProperty(ref _styleSName, (value ?? "").Trim()))
                {
                    OnPropertyChanged(nameof(CadStyleNames));
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
                }
            }
        }

        private string _styleSFont = "tssdeng.shx";
        public string StyleSFont
        {
            get => _styleSFont;
            set
            {
                if (SetProperty(ref _styleSFont, (value ?? "").Trim()))
                {
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
                    OnPropertyChanged(nameof(StyleSFontOptionsWithCurrent));
                }
            }
        }

        private string _styleSBigFont = "tssdchn.shx";
        public string StyleSBigFont
        {
            get => _styleSBigFont;
            set
            {
                if (SetProperty(ref _styleSBigFont, (value ?? "").Trim()))
                {
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
                    OnPropertyChanged(nameof(StyleSBigFontOptionsWithCurrent));
                }
            }
        }

        /// <summary>转入 CAD 时使用的样式名称</summary>
        private string _cadSyncStyleName = "0-hy-说明-S";
        public string CadSyncStyleName
        {
            get => _cadSyncStyleName;
            set
            {
                if (SetProperty(ref _cadSyncStyleName, (value ?? "").Trim()))
                    OnPropertyChanged(nameof(CurrentCadStyleFileDisplay));
            }
        }

        /// <summary>点击转入样式按钮时在 StyleTName 与 StyleSName 之间切换</summary>
        public ICommand ToggleCadSyncStyleCommand { get; }

        /// <summary>恢复全部默认配置</summary>
        public ICommand ResetAllDefaultsCommand { get; }

        /// <summary>将当前配置存为默认</summary>
        public ICommand SaveAsDefaultCommand { get; }

        /// <summary>将当前配置存为设置文档</summary>
        public ICommand SaveToFileCommand { get; }

        /// <summary>从设置文档读取配置</summary>
        public ICommand LoadFromFileCommand { get; }

        // ── MText 显示参数 ──
        private string _mTextAttachment = "TopLeft";
        public string MTextAttachment
        {
            get => _mTextAttachment;
            set => SetProperty(ref _mTextAttachment, string.IsNullOrWhiteSpace(value) ? "TopLeft" : value.Trim());
        }

        private string _mTextLineSpacingStyle = "Exactly";
        public string MTextLineSpacingStyle
        {
            get => _mTextLineSpacingStyle;
            set => SetProperty(ref _mTextLineSpacingStyle, string.IsNullOrWhiteSpace(value) ? "Exactly" : value.Trim());
        }

        private double _mTextObliquingAngle = 0;
        public double MTextObliquingAngle
        {
            get => _mTextObliquingAngle;
            set => SetProperty(ref _mTextObliquingAngle, Math.Max(-85, Math.Min(85, value)));
        }

        private double _mTextCharSpacing = 1.0;
        public double MTextCharSpacing
        {
            get => _mTextCharSpacing;
            set => SetProperty(ref _mTextCharSpacing, Math.Max(0.75, Math.Min(4.0, value)));
        }

        private string _mTextParagraphAlign = "Left";
        public string MTextParagraphAlign
        {
            get => _mTextParagraphAlign;
            set => SetProperty(ref _mTextParagraphAlign, string.IsNullOrWhiteSpace(value) ? "Left" : value.Trim());
        }

        private double _lineSpacingFactor = EditorConfigDefaults.LineSpacingFactor;
        public double LineSpacingFactor
        {
            get => _lineSpacingFactor;
            set => SetProperty(ref _lineSpacingFactor, Math.Max(0.5, Math.Min(3.0, value)));
        }

        /// <summary>转入 CAD 时可选样式列表（StyleTName, StyleSName）</summary>
        public IReadOnlyList<string> CadStyleNames => new[] { StyleTName, StyleSName };

        /// <summary>当前选中样式对应的字体文件显示文本。</summary>
        public string CurrentCadStyleFileDisplay
        {
            get
            {
                if (string.Equals(CadSyncStyleName, StyleTName, StringComparison.OrdinalIgnoreCase))
                    return $"{StyleTName}  ->  {StyleTFont} (TrueType)";
                return $"{StyleSName}  ->  {StyleSFont}" +
                    (string.IsNullOrWhiteSpace(StyleSBigFont) ? string.Empty : $" + {StyleSBigFont}");
            }
        }

        /// <summary>常用 TrueType 字体（标题/说明用）— ComboBox 数据源</summary>
        public static IReadOnlyList<string> TrueTypeFontOptions { get; } = new[]
        {
            "微软雅黑", "Microsoft YaHei", "宋体", "SimSun", "黑体", "SimHei",
            "楷体", "KaiTi", "仿宋", "FangSong", "Arial", "Times New Roman",
            "Calibri", "Consolas", "Cambria", "Tahoma", "Verdana"
        };

        /// <summary>常用 SHX 字体 — ComboBox 数据源</summary>
        public static IReadOnlyList<string> ShxFontOptions { get; } = new[]
        {
            "tssdeng.shx", "tssdchn.shx", "simplex.shx", "romans.shx", "romand.shx",
            "txt.shx", "hztxt.shx", "gbcbig.shx", "chineset.shx"
        };

        /// <summary>常用大字体（SHX 中文）— ComboBox 数据源</summary>
        public static IReadOnlyList<string> BigFontOptions { get; } = new[]
        {
            "tssdchn.shx", "hztxt.shx", "gbcbig.shx", "chineset.shx"
        };

        /// <summary>TrueType 字体选项（含当前值，确保 ComboBox 能正确显示当前选择）</summary>
        public IReadOnlyList<string> StyleTFontOptionsWithCurrent =>
            TrueTypeFontOptions.Contains(_styleTFont)
                ? TrueTypeFontOptions
                : new[] { _styleTFont }.Concat(TrueTypeFontOptions).ToArray();

        /// <summary>SHX 字体选项（含当前值）</summary>
        public IReadOnlyList<string> StyleSFontOptionsWithCurrent =>
            ShxFontOptions.Contains(_styleSFont)
                ? ShxFontOptions
                : new[] { _styleSFont }.Concat(ShxFontOptions).ToArray();

        /// <summary>大字体选项（含当前值）</summary>
        public IReadOnlyList<string> StyleSBigFontOptionsWithCurrent =>
            BigFontOptions.Contains(_styleSBigFont)
                ? BigFontOptions
                : new[] { _styleSBigFont }.Concat(BigFontOptions).ToArray();

        public IReadOnlyList<string> MTextAttachmentOptions => new[]
        {
            "TopLeft", "TopCenter", "TopRight",
            "MiddleLeft", "MiddleCenter", "MiddleRight",
            "BottomLeft", "BottomCenter", "BottomRight"
        };
        public IReadOnlyList<string> MTextLineSpacingStyleOptions => new[] { "AtLeast", "Exactly" };
        public IReadOnlyList<string> MTextParagraphAlignOptions => new[] { "Left", "Center", "Right", "Justify" };

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

        private double _marginTopMm = 10;
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

        private double _columnInnerPaddingMm = EditorConfigDefaults.ColumnInnerPaddingMm;
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

        private double _borderWidth = EditorConfigDefaults.BorderWidth;
        public double BorderWidth
        {
            get => _borderWidth;
            set
            {
                if (SetProperty(ref _borderWidth, Math.Max(0.5, Math.Min(5, value))))
                    UpdateStatus();
            }
        }

        private double _handleWidth = EditorConfigDefaults.HandleWidth;
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

        private double _handleActiveWidth = EditorConfigDefaults.HandleActiveWidth;
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
        private double _h1SpaceBefore = EditorConfigDefaults.H1SpaceBefore;
        public double H1SpaceBefore { get => _h1SpaceBefore; set { if (SetProperty(ref _h1SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h1SpaceAfter = EditorConfigDefaults.H1SpaceAfter;
        public double H1SpaceAfter { get => _h1SpaceAfter; set { if (SetProperty(ref _h1SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h2SpaceBefore = EditorConfigDefaults.H2SpaceBefore;
        public double H2SpaceBefore { get => _h2SpaceBefore; set { if (SetProperty(ref _h2SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h2SpaceAfter = EditorConfigDefaults.H2SpaceAfter;
        public double H2SpaceAfter { get => _h2SpaceAfter; set { if (SetProperty(ref _h2SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h3SpaceBefore = EditorConfigDefaults.H3SpaceBefore;
        public double H3SpaceBefore { get => _h3SpaceBefore; set { if (SetProperty(ref _h3SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h3SpaceAfter = EditorConfigDefaults.H3SpaceAfter;
        public double H3SpaceAfter { get => _h3SpaceAfter; set { if (SetProperty(ref _h3SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _pSpaceAfter = EditorConfigDefaults.PSpaceAfter;
        public double PSpaceAfter { get => _pSpaceAfter; set { if (SetProperty(ref _pSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _liSpaceAfter = EditorConfigDefaults.LiSpaceAfter;
        public double LiSpaceAfter { get => _liSpaceAfter; set { if (SetProperty(ref _liSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceBefore = EditorConfigDefaults.QuoteSpaceBefore;
        public double QuoteSpaceBefore { get => _quoteSpaceBefore; set { if (SetProperty(ref _quoteSpaceBefore, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceAfter = EditorConfigDefaults.QuoteSpaceAfter;
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
            ToggleCadSyncStyleCommand = new RelayCmd(ToggleCadSyncStyle);
            ResetAllDefaultsCommand = new RelayCmd(ResetAllDefaults);
            SaveAsDefaultCommand = new RelayCmd(() => SaveAsDefaultRequested?.Invoke());
            SaveToFileCommand = new RelayCmd(() => SaveToFileRequested?.Invoke());
            LoadFromFileCommand = new RelayCmd(() => LoadFromFileRequested?.Invoke());
            _charsPerColumn = (int[])EditorConfigDefaults.CharsPerColumn.Clone();
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

            ApplyConfig(cfg);
        }

        /// <summary>将配置应用到 ViewModel，供「读取设置文档」等场景复用</summary>
        public void ApplyConfig(EditorConfig cfg)
        {
            if (cfg == null) return;

            _totalHeight = cfg.TotalHeight;
            _scale = cfg.DrawScale;
            _maxColumnCount = cfg.MaxColumnCount > 0 ? Math.Max(1, Math.Min(99, cfg.MaxColumnCount)) : EditorConfigDefaults.MaxColumnCount;
            _minColumnWidthPx = cfg.MinColumnWidthPx > 0 ? Math.Max(20, Math.Min(500, cfg.MinColumnWidthPx)) : EditorConfigDefaults.MinColumnWidthPx;
            _minColumnHeightPx = cfg.MinColumnHeightPx > 0 ? Math.Max(20, Math.Min(500, cfg.MinColumnHeightPx)) : EditorConfigDefaults.MinColumnHeightPx;
            int maxCol = Math.Max(1, _maxColumnCount);
            _columnCount = Math.Max(1, Math.Min(maxCol, cfg.ColumnCount));
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
            _fontFileName = string.IsNullOrWhiteSpace(cfg.FontFileName) ? EditorConfigDefaults.FontFileName : cfg.FontFileName.Trim();
            _bigFontFileName = (cfg.BigFontFileName ?? "").Trim();
            _boldFontName = string.IsNullOrWhiteSpace(cfg.BoldFontName) ? EditorConfigDefaults.BoldFontName : cfg.BoldFontName.Trim();
            _previewFontFamily = string.IsNullOrWhiteSpace(cfg.PreviewFontFamily) ? EditorConfigDefaults.PreviewFontFamily : cfg.PreviewFontFamily.Trim();
            _styleTName = (cfg.StyleTName ?? "0-hy-说明-T").Trim();
            _styleTFont = string.IsNullOrWhiteSpace(cfg.StyleTFont) ? "微软雅黑" : cfg.StyleTFont.Trim();
            _styleSName = (cfg.StyleSName ?? "0-hy-说明-S").Trim();
            _styleSFont = (cfg.StyleSFont ?? "tssdeng.shx").Trim();
            _styleSBigFont = (cfg.StyleSBigFont ?? "tssdchn.shx").Trim();
            _cadSyncStyleName = (cfg.CadSyncStyleName ?? "0-hy-说明-S").Trim();
            _mTextAttachment = string.IsNullOrWhiteSpace(cfg.MTextAttachment) ? "TopLeft" : cfg.MTextAttachment.Trim();
            _mTextLineSpacingStyle = string.IsNullOrWhiteSpace(cfg.MTextLineSpacingStyle) ? "Exactly" : cfg.MTextLineSpacingStyle.Trim();
            _mTextObliquingAngle = Math.Max(-85, Math.Min(85, cfg.MTextObliquingAngle));
            _mTextCharSpacing = Math.Max(0.75, Math.Min(4.0, cfg.MTextCharSpacing));
            _mTextParagraphAlign = string.IsNullOrWhiteSpace(cfg.MTextParagraphAlign) ? "Left" : cfg.MTextParagraphAlign.Trim();
            _lineSpacingFactor = Math.Max(0.5, Math.Min(3.0, cfg.LineSpacingFactor));
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

            OnPropertyChanged(nameof(TotalHeight));
            OnPropertyChanged(nameof(DrawScale));
            OnPropertyChanged(nameof(DrawScaleText));
            OnPropertyChanged(nameof(MaxColumnCount));
            OnPropertyChanged(nameof(MinColumnWidthPx));
            OnPropertyChanged(nameof(MinColumnHeightPx));
            OnPropertyChanged(nameof(ColumnCount));
            OnPropertyChanged(nameof(ColumnGutter));
            OnPropertyChanged(nameof(TextSize));
            OnPropertyChanged(nameof(TextXScale));
            OnPropertyChanged(nameof(PreviewScale));
            OnPropertyChanged(nameof(CharsPerColumn));
            OnPropertyChanged(nameof(PagePreset));
            OnPropertyChanged(nameof(IsLandscape));
            OnPropertyChanged(nameof(PageSizeLabel));
            OnPropertyChanged(nameof(PageOrientationLabel));
            OnPropertyChanged(nameof(PageWidthMm));
            OnPropertyChanged(nameof(PageHeightMm));
            OnPropertyChanged(nameof(MarginLeftMm));
            OnPropertyChanged(nameof(MarginRightMm));
            OnPropertyChanged(nameof(MarginTopMm));
            OnPropertyChanged(nameof(MarginBottomMm));
            OnPropertyChanged(nameof(ColumnInnerPaddingMm));
            OnPropertyChanged(nameof(BorderWidth));
            OnPropertyChanged(nameof(HandleWidth));
            OnPropertyChanged(nameof(HandleActiveWidth));
            OnPropertyChanged(nameof(FontFileName));
            OnPropertyChanged(nameof(BigFontFileName));
            OnPropertyChanged(nameof(BoldFontName));
            OnPropertyChanged(nameof(PreviewFontFamily));
            OnPropertyChanged(nameof(StyleTFont));
            OnPropertyChanged(nameof(StyleSFont));
            OnPropertyChanged(nameof(StyleSBigFont));
            OnPropertyChanged(nameof(CadSyncStyleName));
            OnPropertyChanged(nameof(MTextAttachment));
            OnPropertyChanged(nameof(MTextLineSpacingStyle));
            OnPropertyChanged(nameof(MTextObliquingAngle));
            OnPropertyChanged(nameof(MTextCharSpacing));
            OnPropertyChanged(nameof(MTextParagraphAlign));
            OnPropertyChanged(nameof(LineSpacingFactor));
            OnPropertyChanged(nameof(H1SpaceBefore));
            OnPropertyChanged(nameof(H1SpaceAfter));
            OnPropertyChanged(nameof(H2SpaceBefore));
            OnPropertyChanged(nameof(H2SpaceAfter));
            OnPropertyChanged(nameof(H3SpaceBefore));
            OnPropertyChanged(nameof(H3SpaceAfter));
            OnPropertyChanged(nameof(PSpaceAfter));
            OnPropertyChanged(nameof(LiSpaceAfter));
            OnPropertyChanged(nameof(QuoteSpaceBefore));
            OnPropertyChanged(nameof(QuoteSpaceAfter));
            OnPropertyChanged("SpacingChanged");
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
                MaxColumnCount = MaxColumnCount,
                MinColumnWidthPx = MinColumnWidthPx,
                MinColumnHeightPx = MinColumnHeightPx,
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
                FontFileName = string.Equals(CadSyncStyleName, StyleTName, StringComparison.OrdinalIgnoreCase) ? StyleTFont : StyleSFont,
                BigFontFileName = string.Equals(CadSyncStyleName, StyleTName, StringComparison.OrdinalIgnoreCase) ? "" : StyleSBigFont,
                BoldFontName = BoldFontName,
                PreviewFontFamily = PreviewFontFamily,
                StyleTName = StyleTName,
                StyleTFont = StyleTFont,
                StyleSName = StyleSName,
                StyleSFont = StyleSFont,
                StyleSBigFont = StyleSBigFont,
                CadSyncStyleName = CadSyncStyleName,
                MTextAttachment = MTextAttachment,
                MTextLineSpacingStyle = MTextLineSpacingStyle,
                MTextObliquingAngle = MTextObliquingAngle,
                MTextCharSpacing = MTextCharSpacing,
                MTextParagraphAlign = MTextParagraphAlign,
                LineSpacingFactor = LineSpacingFactor
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
            MarginTopMm = c;
            MarginBottomMm = c;
        }

        /// <summary>恢复全部默认配置（含 CAD 样式、MText 等）</summary>
        public void ResetAllDefaults()
        {
            var d = EditorConfigDefaults.Create();
            DrawScale = d.DrawScale;
            MaxColumnCount = d.MaxColumnCount;
            MinColumnWidthPx = d.MinColumnWidthPx;
            MinColumnHeightPx = d.MinColumnHeightPx;
            ColumnCount = d.ColumnCount;
            ColumnGutter = d.ColumnGutter;
            TextSize = d.TextSize;
            TextXScale = d.TextXScale;
            PreviewScale = d.PreviewScale;
            TotalHeight = d.TotalHeight;
            CharsPerColumn = (int[])d.CharsPerColumn.Clone();
            ColumnParagraphIndices = string.Empty;

            PagePreset = d.PagePreset;
            IsLandscape = d.PagePreset.Contains("横");
            ApplyPagePreset(PagePreset);
            OnPropertyChanged(nameof(PageSizeLabel));
            OnPropertyChanged(nameof(PageOrientationLabel));

            H1SpaceBefore = d.H1SpaceBefore;
            H1SpaceAfter = d.H1SpaceAfter;
            H2SpaceBefore = d.H2SpaceBefore;
            H2SpaceAfter = d.H2SpaceAfter;
            H3SpaceBefore = d.H3SpaceBefore;
            H3SpaceAfter = d.H3SpaceAfter;
            PSpaceAfter = d.PSpaceAfter;
            LiSpaceAfter = d.LiSpaceAfter;
            QuoteSpaceBefore = d.QuoteSpaceBefore;
            QuoteSpaceAfter = d.QuoteSpaceAfter;
            ColumnInnerPaddingMm = d.ColumnInnerPaddingMm;
            BorderWidth = d.BorderWidth;
            HandleWidth = d.HandleWidth;
            HandleActiveWidth = d.HandleActiveWidth;

            FontFileName = d.FontFileName;
            BigFontFileName = d.BigFontFileName;
            BoldFontName = d.BoldFontName;
            PreviewFontFamily = d.PreviewFontFamily;
            StyleTName = d.StyleTName;
            StyleTFont = d.StyleTFont;
            StyleSName = d.StyleSName;
            StyleSFont = d.StyleSFont;
            StyleSBigFont = d.StyleSBigFont;
            CadSyncStyleName = d.CadSyncStyleName;
            MTextAttachment = d.MTextAttachment;
            MTextLineSpacingStyle = d.MTextLineSpacingStyle;
            MTextObliquingAngle = d.MTextObliquingAngle;
            MTextCharSpacing = d.MTextCharSpacing;
            MTextParagraphAlign = d.MTextParagraphAlign;
            LineSpacingFactor = d.LineSpacingFactor;

            SyncDrawScaleTextFromValue();
            UpdateStatus();
            RestoredToDefaults?.Invoke();
        }

        /// <summary>恢复图纸排版默认（保留 CAD 样式等），供顶部工具栏「重置」按钮</summary>
        public void ResetLayoutDefaults()
        {
            ResetAllDefaults();
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

        private void ToggleCadSyncStyle()
        {
            CadSyncStyleName = string.Equals(CadSyncStyleName, StyleTName, StringComparison.OrdinalIgnoreCase)
                ? StyleSName
                : StyleTName;
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
