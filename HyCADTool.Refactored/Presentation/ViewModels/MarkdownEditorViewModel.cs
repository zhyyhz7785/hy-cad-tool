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

        #endregion

        #region Markdown 文本

        private string _markdownText = "";
        public string MarkdownText
        {
            get => _markdownText;
            set { if (SetProperty(ref _markdownText, value)) UpdateStatus(); }
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
            if (vm != null) _textSize = vm.TextSize;

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
                CharsPerColumn = existingConfig.CharsPerColumn;
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
                PreviewScale = PreviewScale
            };
            var vm = SettingsPanelViewModel.Current;
            if (vm != null)
            {
                cfg.FontFileName = vm.FontFileName;
                cfg.BigFontFileName = vm.BigFontFileName;
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

        private void ExecuteInsert()
        {
            DialogResult = true;
            var config = BuildConfig();

            // 按栏拆分 Markdown → 各自渲染 MText
            var columnMarkdowns = SplitMarkdownByColumns(MarkdownText, ColumnParagraphIndices);
            var columnMTexts = new string[columnMarkdowns.Length];
            for (int i = 0; i < columnMarkdowns.Length; i++)
            {
                var renderer = new MarkdownToMTextRenderer(config);
                columnMTexts[i] = renderer.Convert(columnMarkdowns[i]);
            }

            InsertRequested?.Invoke(columnMTexts, MarkdownText, config);
        }

        /// <summary>
        /// 按 JS 预览中的段落分配拆分 Markdown
        /// paraIndices 格式: "0,1,2|3,4,5" — 段落索引按栏分组
        /// </summary>
        private string[] SplitMarkdownByColumns(string markdown, string paraIndices)
        {
            if (string.IsNullOrEmpty(markdown))
                return new[] { "" };

            // 将 Markdown 按空行拆分为"段落块"
            var blocks = SplitMarkdownBlocks(markdown);

            if (string.IsNullOrEmpty(paraIndices))
            {
                // 没有分配信息 → 全部放第一栏
                return new[] { markdown };
            }

            var colGroups = paraIndices.Split('|');
            var result = new string[colGroups.Length];

            for (int c = 0; c < colGroups.Length; c++)
            {
                if (string.IsNullOrWhiteSpace(colGroups[c]))
                {
                    result[c] = "";
                    continue;
                }

                var indices = colGroups[c].Split(',')
                    .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : -1; })
                    .Where(v => v >= 0 && v < blocks.Length)
                    .ToArray();

                result[c] = string.Join("\n\n", indices.Select(i => blocks[i]));
            }

            return result;
        }

        /// <summary>
        /// 将 Markdown 按空行（两个换行）拆分为段落块
        /// 与 Markdig 解析的 block 顺序一致
        /// </summary>
        private static string[] SplitMarkdownBlocks(string markdown)
        {
            // Markdig 中每个 block（heading, paragraph, list, etc.）对应
            // Markdown 源码中由空行分隔的段落
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var blocks = new System.Collections.Generic.List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current.Length > 0)
                    {
                        blocks.Add(current.ToString().TrimEnd());
                        current.Clear();
                    }
                }
                else
                {
                    if (current.Length > 0) current.Append('\n');
                    current.Append(line);
                }
            }
            if (current.Length > 0)
                blocks.Add(current.ToString().TrimEnd());

            return blocks.ToArray();
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
                    MarkdownText = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    StatusText = $"已加载: {System.IO.Path.GetFileName(dlg.FileName)}";
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
