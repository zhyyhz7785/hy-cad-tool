using HyCADTool.Refactored.Domain.Models.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// Markdown 编辑器 ViewModel v6
    /// 每栏独立字符数 → 独立栏宽 → 多个独立 MText
    /// </summary>
    public class MarkdownEditorViewModel : INotifyPropertyChanged
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

        /// <summary>
        /// 插入请求事件
        /// 参数: (每栏MText内容数组, Markdown原文, 配置)
        /// </summary>
        public event Action<string[], string, DesignSpecConfig> InsertRequested;

        /// <summary>
        /// 实时同步请求事件（防抖后触发）
        /// 参数: (每栏MText内容数组, Markdown原文, 配置)
        /// </summary>
        public event Action<string[], string, DesignSpecConfig> LiveSyncRequested;

        /// <summary>
        /// 请求将内容推送到编辑器（加载文件后触发）
        /// 参数: Markdown 字符串
        /// </summary>
        public event Action<string> EditorContentLoadRequested;

        #endregion

        #region Markdown 文本

        private string _markdownText = "";
        public string MarkdownText
        {
            get => _markdownText;
            set { if (SetProperty(ref _markdownText, value)) UpdateStatus(); }
        }

        /// <summary>
        /// 由编辑器（Vditor WebView2）调用，更新 MarkdownText 但不触发回写循环。
        /// 直接设置字段并触发 PropertyChanged（供预览刷新），但不触发编辑器内容重设。
        /// </summary>
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
        /// <summary>出图比例（默认1，用户可调）</summary>
        public double DrawScale
        {
            get => _scale;
            set { if (SetProperty(ref _scale, Math.Max(0.1, value))) UpdateStatus(); }
        }

        private int _columnCount = 2;
        public int ColumnCount
        {
            get => _columnCount;
            set { if (SetProperty(ref _columnCount, Math.Max(1, Math.Min(6, value)))) UpdateStatus(); }
        }

        /// <summary>
        /// 每栏的字符数数组（由 JS 预览回传）
        /// 长度 = ColumnCount，每个元素是该栏最长行的字符数
        /// </summary>
        public int[] CharsPerColumn { get; set; }

        /// <summary>
        /// 每栏的段落索引（由 JS 预览回传，格式: "0,1,2|3,4,5"）
        /// </summary>
        public string ColumnParagraphIndices { get; set; }

        private double _columnGutter = 10;
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

        // TextXScale 在回退编辑器不暴露输入框，采用“已有配置优先，缺失才回退 Settings”。
        private double _resolvedTextXScale = 0.7;

        private double _previewScale = 1.0;
        /// <summary>预览缩放比例（仅预览用）</summary>
        public double PreviewScale
        {
            get => _previewScale;
            set { if (SetProperty(ref _previewScale, Math.Max(0.1, Math.Min(3.0, value)))) UpdateStatus(); }
        }

        private double _totalHeight = 350;
        /// <summary>总高度（图纸 mm）</summary>
        public double TotalHeight
        {
            get => _totalHeight;
            set { if (SetProperty(ref _totalHeight, Math.Max(20, value))) UpdateStatus(); }
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

        /// <summary>间距属性变化时触发 SpacingChanged 以通知 View 刷新预览</summary>
        private void RefreshPreviewVia()
        {
            OnPropertyChanged("SpacingChanged");
        }

        #endregion

        #region 状态

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool DialogResult { get; set; }

        #endregion

        #region 命令

        public ICommand InsertCommand { get; }
        public ICommand LoadFileCommand { get; }
        public ICommand SaveFileCommand { get; }

        #endregion

        #region 构造

        /// <summary>新建模式</summary>
        public MarkdownEditorViewModel()
        {
            var vm = SettingsPanelViewModel.Current;
            if (vm != null)
            {
                _textSize = vm.TextSize;
                _resolvedTextXScale = vm.TextXScale > 0 ? vm.TextXScale : 0.7;
            }

            InsertCommand = new RelayCmd(ExecuteInsert);
            LoadFileCommand = new RelayCmd(ExecuteLoad);
            SaveFileCommand = new RelayCmd(ExecuteSave);

            _markdownText = DefaultMarkdown;
            UpdateStatus();
        }

        /// <summary>二次编辑模式：从已有 MText 读取配置</summary>
        public MarkdownEditorViewModel(string markdown, DesignSpecConfig existingConfig)
            : this()
        {
            if (!string.IsNullOrEmpty(markdown))
                _markdownText = markdown;
            if (existingConfig != null)
            {
                _totalHeight = existingConfig.TotalHeight;
                _scale = existingConfig.Scale;
                _columnCount = Math.Max(1, existingConfig.ColumnCount);
                _columnGutter = existingConfig.ColumnGutter;
                _textSize = existingConfig.TextSize;
                _previewScale = existingConfig.PreviewScale;
                _resolvedTextXScale = existingConfig.TextXScale > 0 ? existingConfig.TextXScale : _resolvedTextXScale;
                CharsPerColumn = existingConfig.CharsPerColumn;
                // 恢复间距
                _h1SpaceBefore = existingConfig.H1SpaceBefore;
                _h1SpaceAfter = existingConfig.H1SpaceAfter;
                _h2SpaceBefore = existingConfig.H2SpaceBefore;
                _h2SpaceAfter = existingConfig.H2SpaceAfter;
                _h3SpaceBefore = existingConfig.H3SpaceBefore;
                _h3SpaceAfter = existingConfig.H3SpaceAfter;
                _pSpaceAfter = existingConfig.PSpaceAfter;
                _liSpaceAfter = existingConfig.LiSpaceAfter;
                _quoteSpaceBefore = existingConfig.QuoteSpaceBefore;
                _quoteSpaceAfter = existingConfig.QuoteSpaceAfter;
            }
            UpdateStatus();
        }

        #endregion

        #region 配置

        public DesignSpecConfig BuildConfig()
        {
            // 确保 CharsPerColumn 数组长度匹配 ColumnCount
            int[] cpc = CharsPerColumn;
            if (cpc == null || cpc.Length != ColumnCount)
            {
                // 没有预览数据时，用默认值 28 填充
                int def = (cpc != null && cpc.Length > 0) ? cpc[0] : 28;
                cpc = Enumerable.Repeat(def, ColumnCount).ToArray();
            }

            var cfg = new DesignSpecConfig
            {
                TotalHeight = TotalHeight,
                Scale = DrawScale,
                ColumnCount = ColumnCount,
                CharsPerColumn = cpc,
                ColumnGutter = ColumnGutter,
                TextSize = TextSize,
                TextXScale = _resolvedTextXScale,
                PreviewScale = PreviewScale,
                // 段前段后间距
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
            var vm = SettingsPanelViewModel.Current;
            if (vm != null)
            {
                if (string.IsNullOrWhiteSpace(cfg.FontFileName))
                    cfg.FontFileName = vm.FontFileName;
                if (string.IsNullOrWhiteSpace(cfg.BigFontFileName))
                    cfg.BigFontFileName = vm.BigFontFileName;
                if (cfg.TextXScale <= 0)
                    cfg.TextXScale = vm.TextXScale;
            }
            return cfg;
        }

        private void UpdateStatus()
        {
            try
            {
                var config = BuildConfig();
                var area = TextAreaCalculator.Calculate(config);
                string colInfo = string.Join("+", area.ColumnWidths.Select(w => $"{w:F0}"));
                StatusText = $"{ColumnCount}栏 | 栏宽={colInfo}mm | 总宽{area.TotalWidth:F1}mm (1:{DrawScale})";
            }
            catch (System.Exception ex)
            {
                StatusText = $"错误: {ex.Message}";
            }
        }

        #endregion

        #region 命令实现

        public void ExecuteInsert()
        {
            DialogResult = true;
            var config = BuildConfig();
            var columnMTexts = BuildColumnMTextContents(config);

            InsertRequested?.Invoke(columnMTexts, MarkdownText, config);
        }

        public void ExecuteLiveSync()
        {
            var config = BuildConfig();
            var columnMTexts = BuildColumnMTextContents(config);
            LiveSyncRequested?.Invoke(columnMTexts, MarkdownText, config);
        }

        /// <summary>
        /// 按 JS 预览中的段落分配拆分 Markdown
        /// paraIndices 格式: "0,1,2|3,4,5" — 段落索引按栏分组
        /// </summary>
        private string[] SplitMarkdownByColumns(string markdown, string paraIndices)
        {
            return MarkdownColumnSplitter.SplitByColumnIndices(markdown, paraIndices);
        }

        private string[] BuildColumnMTextContents(DesignSpecConfig config)
        {
            var columnMarkdowns = SplitMarkdownByColumns(MarkdownText, ColumnParagraphIndices);
            var columnMTexts = new string[columnMarkdowns.Length];
            for (int i = 0; i < columnMarkdowns.Length; i++)
            {
                var renderer = new MarkdownToMTextRenderer(config);
                columnMTexts[i] = renderer.Convert(columnMarkdowns[i]);
            }

            return columnMTexts;
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
                    StatusText = $"已加载: {System.IO.Path.GetFileName(dlg.FileName)}";
                    // 通知 View 将内容推送到 Vditor 编辑器
                    EditorContentLoadRequested?.Invoke(content);
                }
                catch (System.Exception ex) { StatusText = $"加载失败: {ex.Message}"; }
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
                catch (System.Exception ex) { StatusText = $"保存失败: {ex.Message}"; }
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
