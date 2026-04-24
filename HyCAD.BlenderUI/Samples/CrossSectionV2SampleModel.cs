using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Samples
{
    // =========================================================================
    //  rCs 面板 v2 原型 — 节点模型 + ViewModel（独立于 HyCADTool.Refactored）
    //
    //  设计目标：
    //  - 交互/层级/字段命名对齐 `CrossSectionBand` / `CrossSectionDesignerViewModel`
    //    / `CrossSectionDrawViewModel`，但不引用 Refactored 与 AutoCAD；
    //  - 左大纲采用动态树（中央隔离带 / 左侧 / 右侧 + 条带 + 结构层）；
    //  - 右属性面板按节点类型切换内容；
    //  - 中央预览仅做轻量 Canvas 示意，不走 AutoCAD 模型空间。
    //
    //  后续平移时可直接把节点/属性布局对到 Refactored 的 Outliner 与 PropertyEditor。
    // =========================================================================

    /// <summary>条带侧别；语义对齐 <c>HyCADTool.Refactored.Domain.ValueObjects.Road.BandSide</c>。</summary>
    public enum V2BandSide
    {
        Left = -1,
        Center = 0,
        Right = 1,
    }

    /// <summary>
    /// 板块类型；语义对齐 <c>TemplateComponentKind</c>。原型仅列出 UI 需要的子集。
    /// </summary>
    public enum V2BandKind
    {
        Pavement,      // 机动车道
        NonMotorized,  // 非机动车道
        Sidewalk,      // 人行道
        GreenStrip,    // 绿化带
        Separator,     // 分隔带（机非 / 侧分）
        Kerb,          // 路牙 / 缘石
        MedianStrip,   // 中央分隔带（条带内不常用，列出供切换）
    }

    /// <summary>结构层种类；语义对齐 <c>StructureLayerKind</c>。</summary>
    public enum V2LayerKind
    {
        Surface, // 面层
        Base,    // 基层
        Subbase, // 垫层
    }

    /// <summary>横坡坡型；语义对齐 <c>RoadSlopeType</c>。</summary>
    public enum V2SlopeType
    {
        Single,  // 单坡
        Double,  // 双坡
    }

    /// <summary>路拱形式；语义对齐 <c>RoadCrownProfile</c>。</summary>
    public enum V2CrownProfile
    {
        Linear,     // 直线型
        Parabolic,  // 抛物线
        Folded,     // 折线
    }

    // =========================================================================
    //  结构层规则表：面层 2~3、基层 1~5、垫层 1~2；绿化带等不承载结构层
    // =========================================================================

    /// <summary>
    /// 结构层数量约束。原型内部单点决定"新增是否可用 / 删除是否可用"。
    /// 绿化带 / 分隔带 / 路牙 不承载结构层，UI 侧整个"结构层编辑"分组折叠。
    /// </summary>
    public static class V2StructureRules
    {
        public const int SurfaceMin = 2;
        public const int SurfaceMax = 3;
        public const int BaseMin = 1;
        public const int BaseMax = 5;
        public const int SubbaseMin = 1;
        public const int SubbaseMax = 2;

        public static bool IsStructureBearing(V2BandKind kind)
        {
            return kind == V2BandKind.Pavement
                || kind == V2BandKind.NonMotorized
                || kind == V2BandKind.Sidewalk;
        }

        public static int MinOf(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return SurfaceMin;
                case V2LayerKind.Base: return BaseMin;
                case V2LayerKind.Subbase: return SubbaseMin;
                default: return 0;
            }
        }

        public static int MaxOf(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return SurfaceMax;
                case V2LayerKind.Base: return BaseMax;
                case V2LayerKind.Subbase: return SubbaseMax;
                default: return 0;
            }
        }

        public static string LabelOf(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return "面层";
                case V2LayerKind.Base: return "基层";
                case V2LayerKind.Subbase: return "垫层";
                default: return "结构层";
            }
        }

        public static double DefaultThicknessCm(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return 4;
                case V2LayerKind.Base: return 20;
                case V2LayerKind.Subbase: return 15;
                default: return 10;
            }
        }

        public static string DefaultMaterial(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return "细粒式沥青混凝土 AC-13";
                case V2LayerKind.Base: return "水泥稳定碎石";
                case V2LayerKind.Subbase: return "级配碎石";
                default: return string.Empty;
            }
        }
    }

    // =========================================================================
    //  INPC 基类与 RelayCommand（BlenderUI 没有自带的实现）
    // =========================================================================

    /// <summary>
    /// 轻量 INPC 基类。sample 内部专用，避免引入 Refactored / CommunityToolkit 依赖。
    /// </summary>
    public abstract class V2ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>最小化 RelayCommand；CanExecute 变化由调用方手动 Raise。</summary>
    public sealed class V2RelayCommand : ICommand
    {
        private readonly Action _exec;
        private readonly Func<bool> _can;

        public V2RelayCommand(Action exec, Func<bool> can = null)
        {
            _exec = exec ?? throw new ArgumentNullException(nameof(exec));
            _can = can;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _can?.Invoke() ?? true;

        public void Execute(object parameter) => _exec();

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // =========================================================================
    //  大纲节点模型
    // =========================================================================

    /// <summary>大纲节点基类。暴露 TreeView 所需的 IsSelected / IsExpanded 双向绑定。</summary>
    public abstract class V2OutlineNodeBase : V2ObservableObject
    {
        /// <summary>节点头部显示标题。</summary>
        public abstract string DisplayTitle { get; }

        /// <summary>节点右端摘要（宽度 / 层数等只读信息）。</summary>
        public virtual string Summary => string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>用于 HierarchicalDataTemplate 的子节点集合。基类默认空集。</summary>
        public virtual IEnumerable<V2OutlineNodeBase> Children => Array.Empty<V2OutlineNodeBase>();

        /// <summary>显式刷新标题 / 摘要（外部属性联动时调用）。</summary>
        internal void RaiseTitleSummary()
        {
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(Summary));
        }
    }

    /// <summary>中央隔离带节点。</summary>
    public sealed class V2MedianNode : V2OutlineNodeBase
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!SetProperty(ref _isActive, value)) return;
                RaiseTitleSummary();
            }
        }

        private double _width = 2.0;
        public double Width
        {
            get => _width;
            set
            {
                double v = value < 0 ? 0 : value;
                if (!SetProperty(ref _width, v)) return;
                RaiseTitleSummary();
            }
        }

        public override string DisplayTitle => "中央隔离带";

        public override string Summary
            => IsActive
                ? string.Format(CultureInfo.InvariantCulture, "{0:F2} m", Width)
                : "禁用";
    }

    /// <summary>左 / 右侧根节点。</summary>
    public sealed class V2SideNode : V2OutlineNodeBase
    {
        private readonly string _baseTitle;

        public V2SideNode(string baseTitle, V2BandSide side)
        {
            _baseTitle = baseTitle ?? string.Empty;
            Side = side;
            Bands = new ObservableCollection<V2BandNode>();
            Bands.CollectionChanged += OnBandsChanged;
        }

        public V2BandSide Side { get; }

        public ObservableCollection<V2BandNode> Bands { get; }

        public override IEnumerable<V2OutlineNodeBase> Children => Bands;

        public override string DisplayTitle => $"{_baseTitle} ({Bands.Count})";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0:F2} m",
                Bands.Where(b => b.IsActive).Sum(b => b.Width));

        private void OnBandsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseTitleSummary();

            // 订阅新条带的启用 / 宽度变化以便刷新摘要。
            if (e.NewItems != null)
                foreach (V2BandNode band in e.NewItems) band.PropertyChanged += OnBandPropChanged;
            if (e.OldItems != null)
                foreach (V2BandNode band in e.OldItems) band.PropertyChanged -= OnBandPropChanged;
        }

        private void OnBandPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V2BandNode.Width)
                || e.PropertyName == nameof(V2BandNode.IsActive))
            {
                RaiseTitleSummary();
            }
        }
    }

    /// <summary>条带（板块）节点。承载结构层子节点集合。</summary>
    public sealed class V2BandNode : V2OutlineNodeBase
    {
        public V2BandNode(V2BandSide side, V2BandKind kind, string name, double width)
        {
            _side = side;
            _kind = kind;
            _name = string.IsNullOrWhiteSpace(name) ? "条带" : name.Trim();
            _width = width <= 0 ? 3.5 : width;
            _crossSlopePct = 1.5;
            _slopeType = V2SlopeType.Single;
            _crownProfile = V2CrownProfile.Linear;
            _isActive = true;
            _laneCount = kind == V2BandKind.Pavement ? 1 : 0;

            StructureLayers = new ObservableCollection<V2LayerNode>();
            StructureLayers.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(StructureSummary));
                RaiseTitleSummary();
            };

            if (V2StructureRules.IsStructureBearing(kind))
            {
                InjectDefaultLayers(kind);
            }
        }

        // -------- 基础属性 --------

        private V2BandSide _side;
        public V2BandSide Side
        {
            get => _side;
            set => SetProperty(ref _side, value);
        }

        private V2BandKind _kind;
        public V2BandKind Kind
        {
            get => _kind;
            set
            {
                if (!SetProperty(ref _kind, value)) return;

                OnPropertyChanged(nameof(HasStructureLayers));
                OnPropertyChanged(nameof(StructureSummary));

                if (V2StructureRules.IsStructureBearing(value))
                {
                    if (StructureLayers.Count == 0)
                        InjectDefaultLayers(value);
                }
                else
                {
                    StructureLayers.Clear();
                }
                RaiseTitleSummary();
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "条带" : value.Trim())) return;
                RaiseTitleSummary();
            }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set
            {
                double v = value <= 0 ? 0.01 : value;
                if (!SetProperty(ref _width, v)) return;
                RaiseTitleSummary();
            }
        }

        private double _crossSlopePct;
        public double CrossSlopePct
        {
            get => _crossSlopePct;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double clamped = Math.Max(-20, Math.Min(20, value));
                SetProperty(ref _crossSlopePct, clamped);
            }
        }

        private V2SlopeType _slopeType;
        public V2SlopeType SlopeType
        {
            get => _slopeType;
            set => SetProperty(ref _slopeType, value);
        }

        private V2CrownProfile _crownProfile;
        public V2CrownProfile CrownProfile
        {
            get => _crownProfile;
            set => SetProperty(ref _crownProfile, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!SetProperty(ref _isActive, value)) return;
                RaiseTitleSummary();
            }
        }

        private int _laneCount;
        public int LaneCount
        {
            get => _laneCount;
            set => SetProperty(ref _laneCount, value < 0 ? 0 : value);
        }

        // -------- 结构层 --------

        public ObservableCollection<V2LayerNode> StructureLayers { get; }

        public override IEnumerable<V2OutlineNodeBase> Children => StructureLayers;

        public bool HasStructureLayers => V2StructureRules.IsStructureBearing(_kind);

        public string StructureSummary
        {
            get
            {
                if (!HasStructureLayers) return "无结构层";
                int s = StructureLayers.Count(l => l.LayerKind == V2LayerKind.Surface);
                int b = StructureLayers.Count(l => l.LayerKind == V2LayerKind.Base);
                int sb = StructureLayers.Count(l => l.LayerKind == V2LayerKind.Subbase);
                return $"面 {s} / 基 {b} / 垫 {sb}";
            }
        }

        public override string DisplayTitle => _name;

        public override string Summary
            => _isActive
                ? string.Format(CultureInfo.InvariantCulture, "{0:F2} m", _width)
                : "禁用";

        private void InjectDefaultLayers(V2BandKind kind)
        {
            // 注入符合规则下限的默认层：面层 2 / 基层 1 / 垫层 1。
            AddLayerInternal(V2LayerKind.Surface, "上面层");
            AddLayerInternal(V2LayerKind.Surface, "下面层");
            AddLayerInternal(V2LayerKind.Base, "基层");
            AddLayerInternal(V2LayerKind.Subbase, "垫层");
        }

        private void AddLayerInternal(V2LayerKind kind, string name)
        {
            StructureLayers.Add(new V2LayerNode
            {
                LayerKind = kind,
                Name = name,
                ThicknessCm = V2StructureRules.DefaultThicknessCm(kind),
                Material = V2StructureRules.DefaultMaterial(kind),
            });
        }
    }

    /// <summary>结构层节点。</summary>
    public sealed class V2LayerNode : V2OutlineNodeBase
    {
        private V2LayerKind _layerKind;
        public V2LayerKind LayerKind
        {
            get => _layerKind;
            set
            {
                if (!SetProperty(ref _layerKind, value)) return;
                RaiseTitleSummary();
            }
        }

        private string _name = "结构层";
        public string Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "结构层" : value.Trim())) return;
                RaiseTitleSummary();
            }
        }

        private double _thicknessCm = 10;
        public double ThicknessCm
        {
            get => _thicknessCm;
            set
            {
                double v = value < 0 ? 0 : value;
                if (!SetProperty(ref _thicknessCm, v)) return;
                RaiseTitleSummary();
            }
        }

        private string _material = string.Empty;
        public string Material
        {
            get => _material;
            set => SetProperty(ref _material, value ?? string.Empty);
        }

        public override string DisplayTitle
            => $"{V2StructureRules.LabelOf(_layerKind)} · {_name}";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0:F1} cm", _thicknessCm);
    }

    // =========================================================================
    //  ViewModel
    // =========================================================================

    /// <summary>
    /// rCs v2 原型 ViewModel。承载三个根节点 + 全局设置 + 增删命令 + 选中分发。
    /// </summary>
    public sealed class V2CrossSectionSampleViewModel : V2ObservableObject
    {
        public V2CrossSectionSampleViewModel()
        {
            Median = new V2MedianNode { IsActive = true, Width = 2.0 };
            Median.PropertyChanged += OnMedianChanged;

            LeftSide = new V2SideNode("左侧", V2BandSide.Left);
            RightSide = new V2SideNode("右侧", V2BandSide.Right);
            LeftSide.Bands.CollectionChanged += OnSideBandsChanged;
            RightSide.Bands.CollectionChanged += OnSideBandsChanged;

            OutlineRoots = new ObservableCollection<V2OutlineNodeBase>
            {
                Median,
                LeftSide,
                RightSide,
            };

            AddLeftBandCommand = new V2RelayCommand(() => AddBandDefault(V2BandSide.Left));
            AddRightBandCommand = new V2RelayCommand(() => AddBandDefault(V2BandSide.Right));
            RemoveSelectedCommand = new V2RelayCommand(RemoveSelectedNode, () => SelectedBand != null);
            MoveUpCommand = new V2RelayCommand(MoveSelectedUp, () => CanMoveSelected(up: true));
            MoveDownCommand = new V2RelayCommand(MoveSelectedDown, () => CanMoveSelected(up: false));

            AddSurfaceLayerCommand = new V2RelayCommand(
                () => AppendLayerToSelected(V2LayerKind.Surface),
                () => CanAddLayer(V2LayerKind.Surface));
            AddBaseLayerCommand = new V2RelayCommand(
                () => AppendLayerToSelected(V2LayerKind.Base),
                () => CanAddLayer(V2LayerKind.Base));
            AddSubbaseLayerCommand = new V2RelayCommand(
                () => AppendLayerToSelected(V2LayerKind.Subbase),
                () => CanAddLayer(V2LayerKind.Subbase));
            RemoveSelectedLayerCommand = new V2RelayCommand(
                RemoveSelectedLayer,
                () => CanRemoveSelectedLayer());

            LoadDemoLayout();
        }

        // -------- 根节点 --------

        public V2MedianNode Median { get; }
        public V2SideNode LeftSide { get; }
        public V2SideNode RightSide { get; }

        public ObservableCollection<V2OutlineNodeBase> OutlineRoots { get; }

        // -------- 全局设置 --------

        public static IReadOnlyList<int> AvailableScales { get; } = new[] { 50, 100, 150, 200 };

        public static IReadOnlyList<int> AvailableSpeeds { get; } = new[] { 20, 30, 40, 50, 60, 80 };

        public static IReadOnlyList<V2BandKind> AvailableKinds { get; }
            = (V2BandKind[])Enum.GetValues(typeof(V2BandKind));

        public static IReadOnlyList<V2LayerKind> AvailableLayerKinds { get; }
            = (V2LayerKind[])Enum.GetValues(typeof(V2LayerKind));

        public static IReadOnlyList<V2SlopeType> AvailableSlopeTypes { get; }
            = (V2SlopeType[])Enum.GetValues(typeof(V2SlopeType));

        public static IReadOnlyList<V2CrownProfile> AvailableCrownProfiles { get; }
            = (V2CrownProfile[])Enum.GetValues(typeof(V2CrownProfile));

        private int _scaleDenominator = 150;
        /// <summary>默认比例尺 1:150（用户需求）。</summary>
        public int ScaleDenominator
        {
            get => _scaleDenominator;
            set => SetProperty(ref _scaleDenominator, value <= 0 ? 150 : value);
        }

        private int _designSpeed = 40;
        public int DesignSpeed
        {
            get => _designSpeed;
            set => SetProperty(ref _designSpeed, value);
        }

        private string _title = "城市次干路标准断面（原型）";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value ?? string.Empty);
        }

        // -------- 派生摘要 --------

        public double LeftHalfWidth => LeftSide.Bands.Where(b => b.IsActive).Sum(b => b.Width);
        public double RightHalfWidth => RightSide.Bands.Where(b => b.IsActive).Sum(b => b.Width);
        public double MedianWidth => Median.IsActive ? Median.Width : 0;
        public double TotalWidth => LeftHalfWidth + MedianWidth + RightHalfWidth;

        public string FootprintSummary
            => string.Format(CultureInfo.InvariantCulture,
                "左 {0:F2} m / 中 {1:F2} m / 右 {2:F2} m  ·  路幅 {3:F2} m",
                LeftHalfWidth, MedianWidth, RightHalfWidth, TotalWidth);

        private void RaiseFootprint()
        {
            OnPropertyChanged(nameof(LeftHalfWidth));
            OnPropertyChanged(nameof(RightHalfWidth));
            OnPropertyChanged(nameof(MedianWidth));
            OnPropertyChanged(nameof(TotalWidth));
            OnPropertyChanged(nameof(FootprintSummary));
            PreviewRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>每次布局或属性变更触发；View 订阅后重绘预览。</summary>
        public event EventHandler PreviewRequested;

        private void OnMedianChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V2MedianNode.IsActive)
                || e.PropertyName == nameof(V2MedianNode.Width))
            {
                RaiseFootprint();
            }
        }

        private void OnSideBandsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 给每一条新加入的 band 挂 PropertyChanged，让宽/启用变化回冒到 footprint。
            if (e.NewItems != null)
                foreach (V2BandNode band in e.NewItems)
                    band.PropertyChanged += OnBandMetricsChanged;
            if (e.OldItems != null)
                foreach (V2BandNode band in e.OldItems)
                    band.PropertyChanged -= OnBandMetricsChanged;

            RaiseFootprint();
        }

        private void OnBandMetricsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V2BandNode.Width)
                || e.PropertyName == nameof(V2BandNode.IsActive))
            {
                RaiseFootprint();
            }
        }

        // -------- 选中分发 --------

        private object _selectedNode;
        /// <summary>TreeView 当前选中节点（Median / Side / Band / Layer）。</summary>
        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value)) return;
                _selectedNode = value;

                _selectedMedian = value as V2MedianNode;
                _selectedSide = value as V2SideNode;
                _selectedBand = value as V2BandNode;
                _selectedLayer = value as V2LayerNode;

                // 选到 Layer 时，同时反查它所在 Band，让结构层编辑区可以显示父 Band 的名称。
                if (_selectedLayer != null) _selectedBand = FindOwnerBand(_selectedLayer);

                OnPropertyChanged(nameof(SelectedNode));
                OnPropertyChanged(nameof(SelectedMedian));
                OnPropertyChanged(nameof(SelectedSide));
                OnPropertyChanged(nameof(SelectedBand));
                OnPropertyChanged(nameof(SelectedLayer));

                OnPropertyChanged(nameof(IsMedianSelected));
                OnPropertyChanged(nameof(IsSideSelected));
                OnPropertyChanged(nameof(IsBandSelected));
                OnPropertyChanged(nameof(IsLayerSelected));
                OnPropertyChanged(nameof(SelectionPath));
                OnPropertyChanged(nameof(ActiveBandForStructure));

                AddLeftBandCommand.RaiseCanExecuteChanged();
                AddRightBandCommand.RaiseCanExecuteChanged();
                RemoveSelectedCommand.RaiseCanExecuteChanged();
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
                AddSurfaceLayerCommand.RaiseCanExecuteChanged();
                AddBaseLayerCommand.RaiseCanExecuteChanged();
                AddSubbaseLayerCommand.RaiseCanExecuteChanged();
                RemoveSelectedLayerCommand.RaiseCanExecuteChanged();
            }
        }

        private V2MedianNode _selectedMedian;
        public V2MedianNode SelectedMedian => _selectedMedian;

        private V2SideNode _selectedSide;
        public V2SideNode SelectedSide => _selectedSide;

        private V2BandNode _selectedBand;
        public V2BandNode SelectedBand => _selectedBand;

        private V2LayerNode _selectedLayer;
        public V2LayerNode SelectedLayer => _selectedLayer;

        public bool IsMedianSelected => _selectedMedian != null;
        public bool IsSideSelected => _selectedSide != null;
        public bool IsBandSelected => _selectedBand != null && _selectedLayer == null;
        public bool IsLayerSelected => _selectedLayer != null;

        /// <summary>
        /// 结构层编辑区的 DataContext 源：
        /// 选中 Band 时 = Band 自己；选中 Layer 时 = Layer 所属 Band。
        /// </summary>
        public V2BandNode ActiveBandForStructure
        {
            get
            {
                if (_selectedLayer != null) return FindOwnerBand(_selectedLayer);
                if (_selectedBand != null) return _selectedBand;
                return null;
            }
        }

        public string SelectionPath
        {
            get
            {
                if (_selectedLayer != null)
                {
                    var owner = FindOwnerBand(_selectedLayer);
                    var sidePart = owner == null ? "" : $"{SideLabel(owner.Side)} / ";
                    var ownerName = owner == null ? "" : $"{owner.Name} / ";
                    return $"{sidePart}{ownerName}{_selectedLayer.DisplayTitle}";
                }
                if (_selectedBand != null)
                    return $"{SideLabel(_selectedBand.Side)} / {_selectedBand.Name}";
                if (_selectedSide != null)
                    return $"{SideLabel(_selectedSide.Side)}（整侧）";
                if (_selectedMedian != null)
                    return "中央隔离带";
                return "未选中";
            }
        }

        private static string SideLabel(V2BandSide side)
            => side == V2BandSide.Left ? "左侧" : side == V2BandSide.Right ? "右侧" : "中央";

        private V2BandNode FindOwnerBand(V2LayerNode layer)
        {
            foreach (var band in LeftSide.Bands)
                if (band.StructureLayers.Contains(layer)) return band;
            foreach (var band in RightSide.Bands)
                if (band.StructureLayers.Contains(layer)) return band;
            return null;
        }

        // -------- 条带增删 --------

        public V2RelayCommand AddLeftBandCommand { get; }
        public V2RelayCommand AddRightBandCommand { get; }
        public V2RelayCommand RemoveSelectedCommand { get; }
        public V2RelayCommand MoveUpCommand { get; }
        public V2RelayCommand MoveDownCommand { get; }

        private void AddBandDefault(V2BandSide side)
        {
            var bands = side == V2BandSide.Left ? LeftSide.Bands : RightSide.Bands;
            var band = new V2BandNode(side, V2BandKind.Pavement, $"机动车道{bands.Count + 1}", 3.5);
            bands.Add(band);
            SelectedNode = band;
        }

        private void RemoveSelectedNode()
        {
            if (_selectedBand == null) return;
            var bands = _selectedBand.Side == V2BandSide.Left ? LeftSide.Bands : RightSide.Bands;
            bands.Remove(_selectedBand);
            SelectedNode = null;
        }

        private bool CanMoveSelected(bool up)
        {
            if (_selectedBand == null) return false;
            var bands = _selectedBand.Side == V2BandSide.Left ? LeftSide.Bands : RightSide.Bands;
            int idx = bands.IndexOf(_selectedBand);
            if (idx < 0) return false;
            return up ? idx > 0 : idx < bands.Count - 1;
        }

        private void MoveSelectedUp()
        {
            if (_selectedBand == null) return;
            var bands = _selectedBand.Side == V2BandSide.Left ? LeftSide.Bands : RightSide.Bands;
            int idx = bands.IndexOf(_selectedBand);
            if (idx > 0) bands.Move(idx, idx - 1);
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
        }

        private void MoveSelectedDown()
        {
            if (_selectedBand == null) return;
            var bands = _selectedBand.Side == V2BandSide.Left ? LeftSide.Bands : RightSide.Bands;
            int idx = bands.IndexOf(_selectedBand);
            if (idx >= 0 && idx < bands.Count - 1) bands.Move(idx, idx + 1);
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
        }

        // -------- 结构层增删 --------

        public V2RelayCommand AddSurfaceLayerCommand { get; }
        public V2RelayCommand AddBaseLayerCommand { get; }
        public V2RelayCommand AddSubbaseLayerCommand { get; }
        public V2RelayCommand RemoveSelectedLayerCommand { get; }

        private bool CanAddLayer(V2LayerKind kind)
        {
            var band = ActiveBandForStructure;
            if (band == null || !band.HasStructureLayers) return false;
            int current = band.StructureLayers.Count(l => l.LayerKind == kind);
            return current < V2StructureRules.MaxOf(kind);
        }

        private void AppendLayerToSelected(V2LayerKind kind)
        {
            var band = ActiveBandForStructure;
            if (band == null || !band.HasStructureLayers) return;
            int current = band.StructureLayers.Count(l => l.LayerKind == kind);
            if (current >= V2StructureRules.MaxOf(kind)) return;

            var layer = new V2LayerNode
            {
                LayerKind = kind,
                Name = V2StructureRules.LabelOf(kind) + (current + 1),
                ThicknessCm = V2StructureRules.DefaultThicknessCm(kind),
                Material = V2StructureRules.DefaultMaterial(kind),
            };
            // 尽量保持 面→基→垫 的顺序：插在同类最后一项之后，或按顺序落位。
            int insertAt = FindInsertIndex(band.StructureLayers, kind);
            band.StructureLayers.Insert(insertAt, layer);

            SelectedNode = layer;
            AddSurfaceLayerCommand.RaiseCanExecuteChanged();
            AddBaseLayerCommand.RaiseCanExecuteChanged();
            AddSubbaseLayerCommand.RaiseCanExecuteChanged();
            RemoveSelectedLayerCommand.RaiseCanExecuteChanged();
        }

        private static int FindInsertIndex(ObservableCollection<V2LayerNode> layers, V2LayerKind kind)
        {
            // 顺序权重：Surface=0, Base=1, Subbase=2
            int targetWeight = (int)kind;
            int insertAt = layers.Count;
            for (int i = 0; i < layers.Count; i++)
            {
                int w = (int)layers[i].LayerKind;
                if (w > targetWeight)
                {
                    insertAt = i;
                    break;
                }
            }
            return insertAt;
        }

        private bool CanRemoveSelectedLayer()
        {
            if (_selectedLayer == null) return false;
            var band = FindOwnerBand(_selectedLayer);
            if (band == null) return false;
            int current = band.StructureLayers.Count(l => l.LayerKind == _selectedLayer.LayerKind);
            return current > V2StructureRules.MinOf(_selectedLayer.LayerKind);
        }

        private void RemoveSelectedLayer()
        {
            if (_selectedLayer == null) return;
            var band = FindOwnerBand(_selectedLayer);
            if (band == null) return;
            int current = band.StructureLayers.Count(l => l.LayerKind == _selectedLayer.LayerKind);
            if (current <= V2StructureRules.MinOf(_selectedLayer.LayerKind)) return;

            band.StructureLayers.Remove(_selectedLayer);
            SelectedNode = band;

            AddSurfaceLayerCommand.RaiseCanExecuteChanged();
            AddBaseLayerCommand.RaiseCanExecuteChanged();
            AddSubbaseLayerCommand.RaiseCanExecuteChanged();
            RemoveSelectedLayerCommand.RaiseCanExecuteChanged();
        }

        // -------- Demo 布局（原型演示用） --------

        private void LoadDemoLayout()
        {
            // 左侧：机动车道 × 2 + 分隔带 + 非机动车道 + 绿化带 + 人行道
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.Pavement, "机动车道1", 3.5));
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.Pavement, "机动车道2", 3.5));
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.Separator, "机非分隔带", 1.5));
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.NonMotorized, "非机动车道", 2.5));
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.GreenStrip, "绿化带", 1.5));
            LeftSide.Bands.Add(new V2BandNode(V2BandSide.Left, V2BandKind.Sidewalk, "人行道", 3.0));

            // 右侧对称
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.Pavement, "机动车道1", 3.5));
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.Pavement, "机动车道2", 3.5));
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.Separator, "机非分隔带", 1.5));
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.NonMotorized, "非机动车道", 2.5));
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.GreenStrip, "绿化带", 1.5));
            RightSide.Bands.Add(new V2BandNode(V2BandSide.Right, V2BandKind.Sidewalk, "人行道", 3.0));

            SelectedNode = LeftSide.Bands.Count > 0 ? (object)LeftSide.Bands[0] : null;
        }
    }
}
