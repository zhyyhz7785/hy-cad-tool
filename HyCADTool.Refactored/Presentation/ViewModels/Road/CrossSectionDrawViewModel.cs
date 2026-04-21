using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// "横断绘制"窗口（v2 BlenderUI 风格四区布局）的 ViewModel。
    ///
    /// <para>职责（在 <see cref="CrossSectionDesignerViewModel"/> 之上扩展）：</para>
    /// <list type="bullet">
    ///   <item>底部 StatusBar：实时汇总左/中分带/右板块数 + 总宽 + 规范通过率。</item>
    ///   <item>左 Outliner / 右 PropertyEditor 折叠状态（<see cref="IsOutlinerVisible"/> /
    ///       <see cref="IsPropertyPaneVisible"/>）。</item>
    ///   <item>"复制选中条带"/"粘贴条带"命令（提升复用工作流效率）。</item>
    ///   <item><see cref="DrawCommand"/>：与 ConfirmCommand 等价（语义命名）。</item>
    /// </list>
    ///
    /// <para>预览 / 重算 / 命令路由 / 镜像 / 规范检查 全部沿用基类，本类不重复实现。</para>
    /// </summary>
    public sealed class CrossSectionDrawViewModel : CrossSectionDesignerViewModel
    {
        public CrossSectionDrawViewModel(CrossSectionLayout initialLayout = null, Guid? existingTemplateId = null)
            : base(initialLayout, existingTemplateId)
        {
            CopySelectedBandCommand = new RelayCommand(ExecuteCopySelected, () => SelectedBand != null);
            PasteBandCommand = new RelayCommand(ExecutePasteToSelectedSide, () => _bandClipboard != null);
            ToggleOutlinerCommand = new RelayCommand(() => IsOutlinerVisible = !IsOutlinerVisible);
            TogglePropertyPaneCommand = new RelayCommand(() => IsPropertyPaneVisible = !IsPropertyPaneVisible);
            DrawCommand = ConfirmCommand;

            // 三段 Outliner：中央隔离带 / 左侧 / 右侧
            _medianNode = new MedianOutlineNode(this);
            _leftSideNode = new SideOutlineNode("左侧", LeftBands);
            _rightSideNode = new SideOutlineNode("右侧", RightBands);
            OutlineRoot = new List<OutlineNodeBase> { _medianNode, _leftSideNode, _rightSideNode };

            PreviewRequested += (_, figure) => UpdateStatus(figure);
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedBand))
                {
                    OnPropertyChanged(nameof(SelectedSideLabel));
                    SyncTreeSelection();
                }
                else if (e.PropertyName == nameof(CenterMedianWidth))
                {
                    _medianNode.RaiseCenterWidthChanged();
                }
            };

            UpdateStatus(LastFigure);
        }

        /// <summary>
        /// 让 TreeView 的 IsSelected 标志与 VM 三路选中态（SelectedBand / SelectedMedianNode /
        /// SelectedSideNode）保持一致。任何一路变化都会调用此方法统一扫一遍。
        /// </summary>
        private void SyncTreeSelection()
        {
            if (_isSyncingSelection) return;
            _isSyncingSelection = true;
            try
            {
                // Band：遍历左右集合，仅被选中的那条置 true
                var target = SelectedBand;
                foreach (var row in LeftBands) row.IsSelected = ReferenceEquals(row, target);
                foreach (var row in RightBands) row.IsSelected = ReferenceEquals(row, target);

                _medianNode.IsSelected = ReferenceEquals(_selectedMedianNode, _medianNode);
                _leftSideNode.IsSelected = ReferenceEquals(_selectedSideNode, _leftSideNode);
                _rightSideNode.IsSelected = ReferenceEquals(_selectedSideNode, _rightSideNode);
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private bool _isSyncingSelection;

        // ============================== M7.4 绘图模式 ==============================

        private bool _useSingleLineMode;
        /// <summary>
        /// M7.4：是否使用"单线出图"模式（仅顶面投影 + 中心线，不画结构厚度）。
        /// false = 带结构厚度轮廓（默认，与 v1 行为一致）。
        /// 命令层收尾阶段读取本属性决定 <c>RoadStandardSectionDrawService.Draw</c> 的 mode 参数。
        /// </summary>
        public bool UseSingleLineMode
        {
            get => _useSingleLineMode;
            set { if (SetProperty(ref _useSingleLineMode, value)) OnPropertyChanged(nameof(UseStructureThicknessMode)); }
        }

        /// <summary>与 <see cref="UseSingleLineMode"/> 互补，用于 RadioButton 的 IsChecked 绑定。</summary>
        public bool UseStructureThicknessMode
        {
            get => !_useSingleLineMode;
            set { if (value) UseSingleLineMode = false; }
        }

        // ============================== UI 折叠状态 ==============================

        private bool _isOutlinerVisible = true;
        public bool IsOutlinerVisible
        {
            get => _isOutlinerVisible;
            set => SetProperty(ref _isOutlinerVisible, value);
        }

        private bool _isPropertyPaneVisible = true;
        public bool IsPropertyPaneVisible
        {
            get => _isPropertyPaneVisible;
            set => SetProperty(ref _isPropertyPaneVisible, value);
        }

        // ============================== 状态文本 ==============================

        private string _statusMessage = string.Empty;
        /// <summary>底部状态栏文本：板块数 / 总宽 / 规范通过状况。</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }

        /// <summary>UI 显示用：当前选中条带所在侧别的中文标签。</summary>
        public string SelectedSideLabel
        {
            get
            {
                if (SelectedBand == null) return "未选中";
                switch (SelectedBand.Side)
                {
                    case BandSide.Left: return "左半";
                    case BandSide.Right: return "右半";
                    case BandSide.Center: return "中央";
                    default: return SelectedBand.Side.ToString();
                }
            }
        }

        // ============================== 复制 / 粘贴条带 ==============================

        private CrossSectionBand? _bandClipboard;

        /// <summary>UI 绑定（按钮 IsEnabled）：剪贴板是否有可粘贴条带。</summary>
        public bool HasClipboard => _bandClipboard.HasValue;

        public ICommand CopySelectedBandCommand { get; }
        public ICommand PasteBandCommand { get; }
        public ICommand ToggleOutlinerCommand { get; }
        public ICommand TogglePropertyPaneCommand { get; }

        /// <summary>语义化别名：与 <see cref="CrossSectionDesignerViewModel.ConfirmCommand"/> 同一命令实例。</summary>
        public ICommand DrawCommand { get; }

        private void ExecuteCopySelected()
        {
            if (SelectedBand == null) return;
            _bandClipboard = SelectedBand.ToBand();
            OnPropertyChanged(nameof(HasClipboard));
        }

        private void ExecutePasteToSelectedSide()
        {
            if (_bandClipboard == null) return;
            var band = _bandClipboard.Value;
            // 粘贴到当前选中条带所在侧；未选中时默认左半。
            BandSide targetSide;
            if (SelectedBand?.Side == BandSide.Right) targetSide = BandSide.Right;
            else if (SelectedBand?.Side == BandSide.Left) targetSide = BandSide.Left;
            else targetSide = BandSide.Left;

            var collection = targetSide == BandSide.Right ? RightBands : LeftBands;
            var newRow = new BandRowViewModel(band.WithSide(targetSide));
            collection.Add(newRow);
            SelectedBand = newRow;
        }

        // ============================== Outliner 三段节点 + 选中路由 ==============================

        private readonly MedianOutlineNode _medianNode;
        private readonly SideOutlineNode _leftSideNode;
        private readonly SideOutlineNode _rightSideNode;

        /// <summary>
        /// 左侧 TreeView 的 ItemsSource。三条根：中央隔离带 / 左侧 / 右侧。
        /// </summary>
        public IReadOnlyList<OutlineNodeBase> OutlineRoot { get; }

        /// <summary>大纲根"中央隔离带"节点。对外暴露用于 XAML 精确绑定可见性。</summary>
        public MedianOutlineNode MedianNode => _medianNode;

        /// <summary>大纲根"左侧"节点。</summary>
        public SideOutlineNode LeftSideNode => _leftSideNode;

        /// <summary>大纲根"右侧"节点。</summary>
        public SideOutlineNode RightSideNode => _rightSideNode;

        private object _selectedOutlineNode;
        /// <summary>
        /// TreeView 当前选中节点（MedianOutlineNode / SideOutlineNode / BandRowViewModel 三者之一）。
        /// setter 负责分发到 SelectedBand / SelectedMedianNode / SelectedSideNode 三者之一并置空另外两者。
        /// </summary>
        public object SelectedOutlineNode
        {
            get => _selectedOutlineNode;
            set
            {
                if (ReferenceEquals(_selectedOutlineNode, value)) return;
                _selectedOutlineNode = value;

                switch (value)
                {
                    case BandRowViewModel row:
                        _selectedMedianNode = null;
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = row;
                        break;
                    case MedianOutlineNode m:
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = null;
                        SelectedMedianNode = m;
                        break;
                    case SideOutlineNode s:
                        _selectedMedianNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        SelectedBand = null;
                        SelectedSideNode = s;
                        break;
                    default:
                        _selectedMedianNode = null;
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = null;
                        break;
                }

                SyncTreeSelection();
                OnPropertyChanged();
            }
        }

        private MedianOutlineNode _selectedMedianNode;
        /// <summary>当前选中的「中央隔离带」节点（非 null 时右侧属性面板显示中央隔离带 Expander）。</summary>
        public MedianOutlineNode SelectedMedianNode
        {
            get => _selectedMedianNode;
            set
            {
                if (!SetProperty(ref _selectedMedianNode, value)) return;
                SyncTreeSelection();
            }
        }

        private SideOutlineNode _selectedSideNode;
        /// <summary>当前选中的「左/右侧」根节点（属性面板显示该侧批量操作提示）。</summary>
        public SideOutlineNode SelectedSideNode
        {
            get => _selectedSideNode;
            set
            {
                if (!SetProperty(ref _selectedSideNode, value)) return;
                SyncTreeSelection();
            }
        }

        // ============================== 状态计算 ==============================

        private void UpdateStatus(CrossSectionFigure figure)
        {
            var sb = new StringBuilder();
            sb.Append("L=").Append(LeftBands.Count)
              .Append(" / 中=").Append(CenterMedianWidth > 0 ? "1" : "0")
              .Append(" / R=").Append(RightBands.Count);
            sb.Append("  总宽 ").Append(TotalWidth.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");

            int passed = CheckItems.Count(i => i.Passed);
            int total = CheckItems.Count;
            sb.Append("  规范 ").Append(passed).Append("/").Append(total);
            sb.Append(AllChecksPassed ? "（全部通过）" : "（存在未通过项）");

            if (figure != null)
            {
                sb.Append("  顶点 ").Append(figure.Vertices.Count);
                sb.Append("  面板 ").Append(figure.Panels.Count);
            }

            StatusMessage = sb.ToString();
        }
    }

    // =======================================================================
    //  Outliner 三段节点模型（仅供 CrossSectionDrawViewModel 使用）
    // =======================================================================

    /// <summary>
    /// TreeView 根节点基类。用于在 XAML 用 <c>HierarchicalDataTemplate DataType</c>
    /// 按类型匹配不同的模板（中央隔离带 / 侧 / 条带）。
    /// <para>
    /// <see cref="IsSelected"/> / <see cref="IsExpanded"/> 供 TreeViewItem.IsSelected /
    /// IsExpanded TwoWay 绑定，实现 VM→TreeView 选中回流。
    /// </para>
    /// </summary>
    public abstract class OutlineNodeBase : INotifyPropertyChanged
    {
        public abstract string DisplayTitle { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 中央隔离带根节点。<see cref="IsActive"/> 与 <see cref="Width"/> 双向绑定到
    /// <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/>（IsActive=false → 0；true → Width）。
    /// </summary>
    public sealed class MedianOutlineNode : OutlineNodeBase
    {
        private readonly CrossSectionDrawViewModel _owner;
        private double _lastNonZeroWidth = 2.0;

        internal MedianOutlineNode(CrossSectionDrawViewModel owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (_owner.CenterMedianWidth > 0) _lastNonZeroWidth = _owner.CenterMedianWidth;
        }

        public override string DisplayTitle => "中央隔离带";

        /// <summary>是否启用中央隔离带。false → 将 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/> 置 0。</summary>
        public bool IsActive
        {
            get => _owner.CenterMedianWidth > 0;
            set
            {
                if (value == IsActive) return;
                if (value)
                {
                    _owner.CenterMedianWidth = _lastNonZeroWidth > 0 ? _lastNonZeroWidth : 2.0;
                }
                else
                {
                    if (_owner.CenterMedianWidth > 0)
                        _lastNonZeroWidth = _owner.CenterMedianWidth;
                    _owner.CenterMedianWidth = 0;
                }
                // CenterMedianWidth setter 会触发 PropertyChanged 冒泡到本节点的 RaiseCenterWidthChanged。
            }
        }

        /// <summary>
        /// 中央隔离带宽度（m）。启用时绑定到 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/>；
        /// 未启用时（IsActive=false）编辑值会被缓存，下次启用时恢复。
        /// </summary>
        public double Width
        {
            get => IsActive ? _owner.CenterMedianWidth : _lastNonZeroWidth;
            set
            {
                double v = value < 0 ? 0 : value;
                if (IsActive)
                {
                    if (Math.Abs(_owner.CenterMedianWidth - v) < 1e-9) return;
                    _owner.CenterMedianWidth = v;
                    if (v > 0) _lastNonZeroWidth = v;
                }
                else
                {
                    if (Math.Abs(_lastNonZeroWidth - v) < 1e-9) return;
                    _lastNonZeroWidth = v;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        /// <summary>由 ViewModel 在 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/> 变更时调用，刷新两路绑定。</summary>
        internal void RaiseCenterWidthChanged()
        {
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(Width));
        }
    }

    /// <summary>
    /// 「左侧 (N)」/「右侧 (N)」根节点。Children 直接引用 VM 的 <c>LeftBands</c> / <c>RightBands</c>，
    /// <see cref="DisplayTitle"/> 随集合元素数量自动更新。
    /// </summary>
    public sealed class SideOutlineNode : OutlineNodeBase
    {
        private readonly string _baseTitle;

        public SideOutlineNode(string baseTitle, ObservableCollection<BandRowViewModel> children)
        {
            _baseTitle = baseTitle ?? string.Empty;
            Children = children ?? throw new ArgumentNullException(nameof(children));
            Children.CollectionChanged += OnChildrenChanged;
        }

        public ObservableCollection<BandRowViewModel> Children { get; }

        public override string DisplayTitle => $"{_baseTitle} ({Children.Count})";

        private void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }
}
