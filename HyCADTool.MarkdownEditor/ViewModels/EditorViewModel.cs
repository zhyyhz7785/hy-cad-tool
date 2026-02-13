using HyCADTool.MarkdownEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HyCADTool.MarkdownEditor.ViewModels
{
    /// <summary>
    /// Markdown WYSIWYG 编辑器 ViewModel（独立于 AutoCAD，纯 WPF）
    /// </summary>
    public class EditorViewModel : INotifyPropertyChanged
    {
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
            set { if (SetProperty(ref _markdownText, value)) UpdateStatus(); }
        }

        /// <summary>由 Vditor 编辑器调用，更新文本但不触发回写循环</summary>
        public void SetMarkdownFromEditor(string markdown)
        {
            if (_markdownText == markdown) return;
            _markdownText = markdown;
            OnPropertyChanged(nameof(MarkdownText));
            UpdateStatus();
        }

        #endregion

        #region 可调参数

        private double _scale = 1.0;
        public double DrawScale
        {
            get => _scale;
            set { if (SetProperty(ref _scale, Math.Max(0.1, value))) UpdateStatus(); }
        }

        private int _columnCount = 2;
        public int ColumnCount
        {
            get => _columnCount;
            set { if (SetProperty(ref _columnCount, Math.Max(1, Math.Min(10, value)))) UpdateStatus(); }
        }

        public int[] CharsPerColumn { get; set; }
        public string ColumnParagraphIndices { get; set; }

        private double _columnGutter = 0;
        public double ColumnGutter
        {
            get => _columnGutter;
            set { if (SetProperty(ref _columnGutter, Math.Max(0, value))) UpdateStatus(); }
        }

        private double _textSize = 2.5;
        public double TextSize
        {
            get => _textSize;
            set { if (SetProperty(ref _textSize, Math.Max(0.5, value))) UpdateStatus(); }
        }

        private double _previewScale = 1.0;
        public double PreviewScale
        {
            get => _previewScale;
            set { if (SetProperty(ref _previewScale, Math.Max(0.1, Math.Min(3.0, value)))) UpdateStatus(); }
        }

        private double _totalHeight = 350;
        public double TotalHeight
        {
            get => _totalHeight;
            set { if (SetProperty(ref _totalHeight, Math.Max(20, value))) UpdateStatus(); }
        }

        private double _textXScale = 0.7;
        public double TextXScale
        {
            get => _textXScale;
            set { if (SetProperty(ref _textXScale, Math.Max(0.1, value))) UpdateStatus(); }
        }

        public IReadOnlyList<string> PagePresets { get; } = new[]
        {
            "A4横向", "A4竖向", "A3横向", "A3竖向", "A2横向", "A2竖向"
        };

        private string _pagePreset = "A3横向";
        public string PagePreset
        {
            get => _pagePreset;
            set
            {
                if (SetProperty(ref _pagePreset, string.IsNullOrWhiteSpace(value) ? "A3横向" : value))
                {
                    ApplyPagePreset(_pagePreset);
                    UpdateStatus();
                }
            }
        }

        private double _pageWidthMm = 420;
        public double PageWidthMm
        {
            get => _pageWidthMm;
            private set => SetProperty(ref _pageWidthMm, value);
        }

        private double _pageHeightMm = 297;
        public double PageHeightMm
        {
            get => _pageHeightMm;
            private set => SetProperty(ref _pageHeightMm, value);
        }

        private double _marginLeftMm = 20;
        public double MarginLeftMm
        {
            get => _marginLeftMm;
            private set => SetProperty(ref _marginLeftMm, value);
        }

        private double _marginRightMm = 20;
        public double MarginRightMm
        {
            get => _marginRightMm;
            private set => SetProperty(ref _marginRightMm, value);
        }

        private double _marginTopMm = 20;
        public double MarginTopMm
        {
            get => _marginTopMm;
            private set => SetProperty(ref _marginTopMm, value);
        }

        private double _marginBottomMm = 20;
        public double MarginBottomMm
        {
            get => _marginBottomMm;
            private set => SetProperty(ref _marginBottomMm, value);
        }

        // ── 段前段后间距（字高倍数） ──
        private double _h1SpaceBefore = 2.0;
        public double H1SpaceBefore { get => _h1SpaceBefore; set { if (SetProperty(ref _h1SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h1SpaceAfter = 0.8;
        public double H1SpaceAfter { get => _h1SpaceAfter; set { if (SetProperty(ref _h1SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h2SpaceBefore = 1.5;
        public double H2SpaceBefore { get => _h2SpaceBefore; set { if (SetProperty(ref _h2SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h2SpaceAfter = 0.6;
        public double H2SpaceAfter { get => _h2SpaceAfter; set { if (SetProperty(ref _h2SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _h3SpaceBefore = 1.2;
        public double H3SpaceBefore { get => _h3SpaceBefore; set { if (SetProperty(ref _h3SpaceBefore, value)) RefreshPreviewVia(); } }

        private double _h3SpaceAfter = 0.4;
        public double H3SpaceAfter { get => _h3SpaceAfter; set { if (SetProperty(ref _h3SpaceAfter, value)) RefreshPreviewVia(); } }

        private double _pSpaceAfter = 0.5;
        public double PSpaceAfter { get => _pSpaceAfter; set { if (SetProperty(ref _pSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _liSpaceAfter = 0.2;
        public double LiSpaceAfter { get => _liSpaceAfter; set { if (SetProperty(ref _liSpaceAfter, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceBefore = 0.5;
        public double QuoteSpaceBefore { get => _quoteSpaceBefore; set { if (SetProperty(ref _quoteSpaceBefore, value)) RefreshPreviewVia(); } }

        private double _quoteSpaceAfter = 0.5;
        public double QuoteSpaceAfter { get => _quoteSpaceAfter; set { if (SetProperty(ref _quoteSpaceAfter, value)) RefreshPreviewVia(); } }

        private void RefreshPreviewVia() => OnPropertyChanged("SpacingChanged");

        #endregion

        #region 状态

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool DialogConfirmed { get; set; }

        #endregion

        #region 命令

        public ICommand LoadFileCommand { get; }
        public ICommand SaveFileCommand { get; }

        #endregion

        #region 构造

        public EditorViewModel()
        {
            LoadFileCommand = new RelayCmd(ExecuteLoad);
            SaveFileCommand = new RelayCmd(ExecuteSave);
            _markdownText = DefaultMarkdown;
            ApplyPagePreset(_pagePreset);
            UpdateStatus();
        }

        public EditorViewModel(EditorInput input) : this()
        {
            if (input == null) return;
            if (!string.IsNullOrEmpty(input.Markdown))
                _markdownText = input.Markdown;

            var cfg = input.Config;
            if (cfg == null) return;

            _totalHeight = cfg.TotalHeight;
            _scale = cfg.Scale;
            _columnCount = Math.Max(1, Math.Min(10, cfg.ColumnCount));
            _columnGutter = cfg.ColumnGutter;
            _textSize = cfg.TextSize;
            _textXScale = cfg.TextXScale;
            _previewScale = cfg.PreviewScale;
            CharsPerColumn = cfg.CharsPerColumn;
            _pagePreset = string.IsNullOrWhiteSpace(cfg.PagePreset) ? _pagePreset : cfg.PagePreset;
            _pageWidthMm = cfg.PageWidthMm > 0 ? cfg.PageWidthMm : _pageWidthMm;
            _pageHeightMm = cfg.PageHeightMm > 0 ? cfg.PageHeightMm : _pageHeightMm;
            _marginLeftMm = cfg.MarginLeftMm;
            _marginRightMm = cfg.MarginRightMm;
            _marginTopMm = cfg.MarginTopMm;
            _marginBottomMm = cfg.MarginBottomMm;

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

            UpdateStatus();
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
                Scale = DrawScale,
                ColumnCount = ColumnCount,
                CharsPerColumn = cpc,
                ColumnGutter = ColumnGutter,
                TextSize = TextSize,
                TextXScale = TextXScale,
                PreviewScale = PreviewScale,
                PagePreset = PagePreset,
                PageWidthMm = PageWidthMm,
                PageHeightMm = PageHeightMm,
                MarginLeftMm = MarginLeftMm,
                MarginRightMm = MarginRightMm,
                MarginTopMm = MarginTopMm,
                MarginBottomMm = MarginBottomMm,
                H1SpaceBefore = H1SpaceBefore,
                H1SpaceAfter = H1SpaceAfter,
                H2SpaceBefore = H2SpaceBefore,
                H2SpaceAfter = H2SpaceAfter,
                H3SpaceBefore = H3SpaceBefore,
                H3SpaceAfter = H3SpaceAfter,
                PSpaceAfter = PSpaceAfter,
                LiSpaceAfter = LiSpaceAfter,
                QuoteSpaceBefore = QuoteSpaceBefore,
                QuoteSpaceAfter = QuoteSpaceAfter
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
            switch (preset)
            {
                case "A4横向":
                    PageWidthMm = 297; PageHeightMm = 210; break;
                case "A4竖向":
                    PageWidthMm = 210; PageHeightMm = 297; break;
                case "A3横向":
                    PageWidthMm = 420; PageHeightMm = 297; break;
                case "A3竖向":
                    PageWidthMm = 297; PageHeightMm = 420; break;
                case "A2横向":
                    PageWidthMm = 594; PageHeightMm = 420; break;
                case "A2竖向":
                    PageWidthMm = 420; PageHeightMm = 594; break;
                default:
                    PageWidthMm = 420; PageHeightMm = 297; break;
            }

            // 与稳定版一致：边距默认统一，作为预览参数显示
            MarginLeftMm = 20;
            MarginRightMm = 20;
            MarginTopMm = 20;
            MarginBottomMm = 20;
        }

        #endregion

        #region 命令实现

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
