using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace HyCAD.BlenderUI.Samples
{
    // =========================================================================
    //  纵断面 v2 面板原型 — 节点模型 + ViewModel（独立于 HyCADTool.Refactored）
    //
    //  设计目标：
    //  - 交互 / 层级 / 字段命名对齐 `Profile` / `ProfileVertex` / `ProfileEditorViewModel`，
    //    但本 sample 不引用 Refactored 与 AutoCAD；
    //  - Outliner 四根：路线 Alignment / 地面线 EG / 设计线 FG / 数据条 BandSet；
    //  - 属性面板按节点类型五路互斥切换；
    //  - Profile View 仅骨架：网格 + EG 黑虚线 + FG 蓝实线 + PVI 红点；
    //    派生字段 L / E / T 用最简公式展示，**不复刻 ProfileFgDesigner 的抛物线**；
    //  - 数据条 9 行可配置（默认勾 5 行），行顺序静态，CheckBox 控制显隐。
    //
    //  基础设施（V2ObservableObject / V2RelayCommand）复用
    //  `CrossSectionV2SampleModel.cs` 中已有的实现（同一命名空间），
    //  避免重复类型定义。
    // =========================================================================

    // -------------------------------------------------------------------------
    //  枚举：采样源、数据条行类型
    // -------------------------------------------------------------------------

    /// <summary>地面线 EG 的采样来源。原型仅列出 UI 下拉选项。</summary>
    public enum V2EgSampleSource
    {
        Dtm,           // 地形曲面（DTM / TIN）
        DiscretePoints,// 离散高程点（中桩实测）
        Manual,        // 手工输入
    }

    /// <summary>
    /// 数据条行类型。顺序即默认渲染顺序（从上到下），
    /// UI 上每行通过 <see cref="V2BandRowNode.IsEnabled"/> 决定是否可见。
    /// </summary>
    public enum V2BandRowKind
    {
        PavementRise,                // 路面抬高
        DesignElevation,             // 设计高程
        ExistingElevation,           // 自然高程
        DesignGradeAndVerticalCurve, // 设计纵坡 + 竖曲线
        ServiceRoadElevation,        // 辅道高程
        HorizontalCurve,             // 平曲线
        Intersection,                // 交叉口
        Superelevation,              // 超高
        StationLabel,                // 桩号
    }

    /// <summary>PVI 的竖曲线派生类型。由 g_in / g_out 符号决定。</summary>
    public enum V2PviCurveType
    {
        None,    // 端点或无竖曲线
        Sag,     // 凹曲线
        Crest,   // 凸曲线
        Tangent, // 折线（无竖曲线过渡，相邻坡度不同但 R=0）
    }

    // -------------------------------------------------------------------------
    //  大纲节点基类
    // -------------------------------------------------------------------------

    /// <summary>
    /// 大纲节点基类。命名与 rCs 的 <see cref="V2OutlineNodeBase"/> 对齐（仅加 Profile 前缀避免冲突），
    /// 暴露 TreeView 所需的 IsSelected / IsExpanded 双向绑定。
    /// </summary>
    public abstract class V2ProfileOutlineNodeBase : V2ObservableObject
    {
        public abstract string DisplayTitle { get; }

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

        public virtual IEnumerable<V2ProfileOutlineNodeBase> Children
            => Array.Empty<V2ProfileOutlineNodeBase>();

        internal void RaiseTitleSummary()
        {
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(Summary));
        }
    }

    // -------------------------------------------------------------------------
    //  路线 Alignment 节点
    // -------------------------------------------------------------------------

    /// <summary>
    /// 路线节点（仅作 UI 展示，原型值为假数据 L = 1698m）。
    /// 真实项目对应 <c>HyCADTool.Refactored.Domain.Models.Road.Alignment</c>。
    /// </summary>
    public sealed class V2ProfileAlignmentNode : V2ProfileOutlineNodeBase
    {
        private string _name = "主线 A";
        public string Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "主线" : value.Trim())) return;
                RaiseTitleSummary();
            }
        }

        private double _totalLength = 1698.0;
        public double TotalLength
        {
            get => _totalLength;
            set
            {
                double v = value <= 0 ? 0 : value;
                if (!SetProperty(ref _totalLength, v)) return;
                OnPropertyChanged(nameof(EndStation));
                RaiseTitleSummary();
            }
        }

        private double _startStation = 0;
        public double StartStation
        {
            get => _startStation;
            set
            {
                if (!SetProperty(ref _startStation, value)) return;
                OnPropertyChanged(nameof(EndStation));
                RaiseTitleSummary();
            }
        }

        public double EndStation => _startStation + _totalLength;

        public override string DisplayTitle => $"路线 · {_name}";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "L={0:F2} m", _totalLength);
    }

    // -------------------------------------------------------------------------
    //  地面线 EG
    // -------------------------------------------------------------------------

    /// <summary>
    /// EG 采样点（桩号 / 高程）。单点轻量行；不计入 Outliner Children，
    /// 只在属性面板 ListBox / 预览 Canvas 中使用。
    /// </summary>
    public sealed class V2EgSampleRow : V2ObservableObject
    {
        private double _station;
        public double Station
        {
            get => _station;
            set => SetProperty(ref _station, value);
        }

        private double _elevation;
        public double Elevation
        {
            get => _elevation;
            set => SetProperty(ref _elevation, value);
        }

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "K{0:F2} H={1:F3}", _station, _elevation);
    }

    /// <summary>地面线 EG 节点。叶节点（不展开子节点），属性面板内查看采样点列表。</summary>
    public sealed class V2ProfileExistingGroundNode : V2ProfileOutlineNodeBase
    {
        public V2ProfileExistingGroundNode()
        {
            Samples = new ObservableCollection<V2EgSampleRow>();
            Samples.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(SampleCount));
                RaiseTitleSummary();
            };
        }

        public ObservableCollection<V2EgSampleRow> Samples { get; }

        public int SampleCount => Samples.Count;

        private V2EgSampleSource _source = V2EgSampleSource.DiscretePoints;
        public V2EgSampleSource Source
        {
            get => _source;
            set
            {
                if (!SetProperty(ref _source, value)) return;
                RaiseTitleSummary();
            }
        }

        private double _sampleInterval = 50.0;
        public double SampleInterval
        {
            get => _sampleInterval;
            set
            {
                double v = value < 1 ? 1 : value;
                if (!SetProperty(ref _sampleInterval, v)) return;
                RaiseTitleSummary();
            }
        }

        public override string DisplayTitle => "地面线 EG";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0} 点 · {1:F0} m", SampleCount, _sampleInterval);
    }

    // -------------------------------------------------------------------------
    //  设计线 FG + PVI
    // -------------------------------------------------------------------------

    /// <summary>
    /// 设计线 FG 节点。子节点为若干 <see cref="V2PviNode"/>。
    /// </summary>
    public sealed class V2ProfileDesignNode : V2ProfileOutlineNodeBase
    {
        public V2ProfileDesignNode()
        {
            Pvis = new ObservableCollection<V2PviNode>();
            Pvis.CollectionChanged += OnPvisChanged;
        }

        private string _schemeName = "设计纵断面 1";
        public string SchemeName
        {
            get => _schemeName;
            set
            {
                if (!SetProperty(ref _schemeName, string.IsNullOrWhiteSpace(value) ? "设计纵断面" : value.Trim())) return;
                RaiseTitleSummary();
            }
        }

        private bool _isPrimary = true;
        public bool IsPrimary
        {
            get => _isPrimary;
            set => SetProperty(ref _isPrimary, value);
        }

        public ObservableCollection<V2PviNode> Pvis { get; }

        public int PviCount => Pvis.Count;

        public override IEnumerable<V2ProfileOutlineNodeBase> Children => Pvis;

        public override string DisplayTitle => $"设计线 · {_schemeName}";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0} 个 PVI", PviCount);

        /// <summary>
        /// 刷新所有 PVI 的派生字段（g_in / g_out / L / E / T / Type）。
        /// 在 PVI 字段改变、增删、排序后调用。
        /// </summary>
        public void RefreshDerived()
        {
            var sorted = Pvis.OrderBy(p => p.Station).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                // g_in：与左邻点连接的坡度（第一个点没有左邻，置 0）
                double gIn = 0;
                if (i > 0)
                {
                    var prev = sorted[i - 1];
                    double dx = p.Station - prev.Station;
                    if (Math.Abs(dx) > 1e-6) gIn = (p.Elevation - prev.Elevation) / dx;
                }
                // g_out：与右邻点连接的坡度（最后点没有右邻，置 0）
                double gOut = 0;
                if (i < sorted.Count - 1)
                {
                    var next = sorted[i + 1];
                    double dx = next.Station - p.Station;
                    if (Math.Abs(dx) > 1e-6) gOut = (next.Elevation - p.Elevation) / dx;
                }

                p.GradeInPct = gIn * 100;
                p.GradeOutPct = gOut * 100;

                double omega = gOut - gIn;
                p.OmegaPct = omega * 100;

                bool isEndpoint = (i == 0 || i == sorted.Count - 1);
                if (isEndpoint || p.CurveRadius <= 0 || Math.Abs(omega) < 1e-6)
                {
                    p.CurveLength = 0;
                    p.TangentLength = 0;
                    p.ExternalDistance = 0;
                    p.CurveType = isEndpoint || Math.Abs(omega) < 1e-6
                        ? V2PviCurveType.None
                        : V2PviCurveType.Tangent;
                }
                else
                {
                    // 最简展示公式（非 ProfileFgDesigner 严格结果）：
                    //   L = R · |ω|    T = L / 2    E = T² / (2R)
                    double L = p.CurveRadius * Math.Abs(omega);
                    double T = L / 2;
                    double E = (T * T) / (2 * p.CurveRadius);
                    p.CurveLength = L;
                    p.TangentLength = T;
                    p.ExternalDistance = E;
                    p.CurveType = omega > 0 ? V2PviCurveType.Sag : V2PviCurveType.Crest;
                }
                p.Index = i;
                p.RefreshDerivedDisplay();
            }
        }

        private void OnPvisChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(PviCount));
            RaiseTitleSummary();
        }
    }

    /// <summary>
    /// PVI 节点。语义对齐 <c>ProfileVertex { Station, Elevation, CurveRadius }</c>；
    /// 派生字段 <see cref="CurveLength"/> 等由 <see cref="V2ProfileDesignNode.RefreshDerived"/> 回写。
    /// </summary>
    public sealed class V2PviNode : V2ProfileOutlineNodeBase
    {
        public V2PviNode(double station, double elevation, double radius = 0)
        {
            _station = station;
            _elevation = elevation;
            _curveRadius = radius < 0 ? 0 : radius;
        }

        // --- 主字段 ---

        private double _station;
        public double Station
        {
            get => _station;
            set
            {
                if (!SetProperty(ref _station, value)) return;
                RaiseTitleSummary();
            }
        }

        private double _elevation;
        public double Elevation
        {
            get => _elevation;
            set
            {
                if (!SetProperty(ref _elevation, value)) return;
                RaiseTitleSummary();
            }
        }

        private double _curveRadius;
        public double CurveRadius
        {
            get => _curveRadius;
            set
            {
                double v = value < 0 ? 0 : value;
                if (!SetProperty(ref _curveRadius, v)) return;
                RaiseTitleSummary();
            }
        }

        // --- 派生字段（VM 回写，只读）---

        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private double _gradeInPct;
        public double GradeInPct
        {
            get => _gradeInPct;
            set => SetProperty(ref _gradeInPct, value);
        }

        private double _gradeOutPct;
        public double GradeOutPct
        {
            get => _gradeOutPct;
            set => SetProperty(ref _gradeOutPct, value);
        }

        private double _omegaPct;
        public double OmegaPct
        {
            get => _omegaPct;
            set => SetProperty(ref _omegaPct, value);
        }

        private double _curveLength;
        public double CurveLength
        {
            get => _curveLength;
            set => SetProperty(ref _curveLength, value);
        }

        private double _tangentLength;
        public double TangentLength
        {
            get => _tangentLength;
            set => SetProperty(ref _tangentLength, value);
        }

        private double _externalDistance;
        public double ExternalDistance
        {
            get => _externalDistance;
            set => SetProperty(ref _externalDistance, value);
        }

        private V2PviCurveType _curveType = V2PviCurveType.None;
        public V2PviCurveType CurveType
        {
            get => _curveType;
            set => SetProperty(ref _curveType, value);
        }

        internal void RefreshDerivedDisplay()
        {
            OnPropertyChanged(nameof(CurveTypeText));
            RaiseTitleSummary();
        }

        public string CurveTypeText
        {
            get
            {
                switch (_curveType)
                {
                    case V2PviCurveType.Sag: return "凹";
                    case V2PviCurveType.Crest: return "凸";
                    case V2PviCurveType.Tangent: return "折线";
                    default: return "—";
                }
            }
        }

        public override string DisplayTitle
            => string.Format(CultureInfo.InvariantCulture, "PVI#{0}  K{1:F2}", _index + 1, _station);

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture,
                _curveRadius > 0 ? "H={0:F2} · R={1:F0}" : "H={0:F2}",
                _elevation, _curveRadius);
    }

    // -------------------------------------------------------------------------
    //  数据条 BandSet
    // -------------------------------------------------------------------------

    /// <summary>单行数据条（桩号 / 设计高程 / 平曲线 / 超高…）。</summary>
    public sealed class V2BandRowNode : V2ProfileOutlineNodeBase
    {
        public V2BandRowNode(V2BandRowKind kind, bool enabled)
        {
            Kind = kind;
            _isEnabled = enabled;
        }

        public V2BandRowKind Kind { get; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (!SetProperty(ref _isEnabled, value)) return;
                RaiseTitleSummary();
            }
        }

        public override string DisplayTitle => BandRowLabels.LabelOf(Kind);

        public override string Summary => _isEnabled ? "启用" : "隐藏";
    }

    /// <summary>数据条根节点：容器，子节点即 9 行。</summary>
    public sealed class V2BandSetNode : V2ProfileOutlineNodeBase
    {
        public V2BandSetNode()
        {
            Bands = new ObservableCollection<V2BandRowNode>();
            Bands.CollectionChanged += (_, __) => RaiseTitleSummary();
        }

        public ObservableCollection<V2BandRowNode> Bands { get; }

        public override IEnumerable<V2ProfileOutlineNodeBase> Children => Bands;

        public override string DisplayTitle => "数据条";

        public override string Summary
        {
            get
            {
                int total = Bands.Count;
                int on = Bands.Count(b => b.IsEnabled);
                return $"{on}/{total}";
            }
        }

        public void AddRow(V2BandRowKind kind, bool enabled)
        {
            var row = new V2BandRowNode(kind, enabled);
            Bands.Add(row);
            row.PropertyChanged += OnBandPropChanged;
        }

        private void OnBandPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V2BandRowNode.IsEnabled))
            {
                RaiseTitleSummary();
            }
        }
    }

    /// <summary>数据条行标签（中文）。</summary>
    public static class BandRowLabels
    {
        public static string LabelOf(V2BandRowKind kind)
        {
            switch (kind)
            {
                case V2BandRowKind.PavementRise: return "路面抬高";
                case V2BandRowKind.DesignElevation: return "设计高程";
                case V2BandRowKind.ExistingElevation: return "自然高程";
                case V2BandRowKind.DesignGradeAndVerticalCurve: return "设计纵坡 / 竖曲线";
                case V2BandRowKind.ServiceRoadElevation: return "辅道高程";
                case V2BandRowKind.HorizontalCurve: return "平曲线";
                case V2BandRowKind.Intersection: return "交叉口";
                case V2BandRowKind.Superelevation: return "超高";
                case V2BandRowKind.StationLabel: return "桩号";
                default: return kind.ToString();
            }
        }
    }

    // =========================================================================
    //  ViewModel
    // =========================================================================

    /// <summary>
    /// 纵断面 v2 面板原型 ViewModel。
    /// </summary>
    public sealed class V2ProfileSampleViewModel : V2ObservableObject
    {
        public V2ProfileSampleViewModel()
        {
            Alignment = new V2ProfileAlignmentNode();
            Alignment.PropertyChanged += OnAlignmentChanged;

            ExistingGround = new V2ProfileExistingGroundNode();
            ExistingGround.Samples.CollectionChanged += (_, __) => RaisePreview();

            DesignProfile = new V2ProfileDesignNode();
            DesignProfile.Pvis.CollectionChanged += OnPvisChanged;

            BandSet = new V2BandSetNode();

            OutlineRoots = new ObservableCollection<V2ProfileOutlineNodeBase>
            {
                Alignment,
                ExistingGround,
                DesignProfile,
                BandSet,
            };

            AddPviCommand = new V2RelayCommand(AppendPvi);
            RemoveSelectedPviCommand = new V2RelayCommand(RemoveSelectedPvi, () => _selectedPvi != null && DesignProfile.Pvis.Count > 2);
            MovePviUpCommand = new V2RelayCommand(MoveSelectedPviUp, () => CanMovePvi(up: true));
            MovePviDownCommand = new V2RelayCommand(MoveSelectedPviDown, () => CanMovePvi(up: false));

            LoadDemoData();
            DesignProfile.RefreshDerived();

            SelectedNode = DesignProfile.Pvis.Count > 1 ? (object)DesignProfile.Pvis[1] : null;
        }

        // -------- 根节点 --------

        public V2ProfileAlignmentNode Alignment { get; }
        public V2ProfileExistingGroundNode ExistingGround { get; }
        public V2ProfileDesignNode DesignProfile { get; }
        public V2BandSetNode BandSet { get; }

        public ObservableCollection<V2ProfileOutlineNodeBase> OutlineRoots { get; }

        // -------- 全局设置 --------

        public static IReadOnlyList<int> AvailableScales { get; } = new[] { 200, 500, 1000, 2000 };

        public static IReadOnlyList<int> AvailableSpeeds { get; } = new[] { 20, 30, 40, 50, 60, 80 };

        public static IReadOnlyList<V2EgSampleSource> AvailableEgSources { get; }
            = (V2EgSampleSource[])Enum.GetValues(typeof(V2EgSampleSource));

        private int _scaleDenominator = 500;
        /// <summary>默认比例尺 1:500（纵断面图常见值）。</summary>
        public int ScaleDenominator
        {
            get => _scaleDenominator;
            set
            {
                if (!SetProperty(ref _scaleDenominator, value <= 0 ? 500 : value)) return;
                RaisePreview();
            }
        }

        private int _designSpeed = 40;
        public int DesignSpeed
        {
            get => _designSpeed;
            set => SetProperty(ref _designSpeed, value);
        }

        private string _title = "纵断面设计（原型）";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value ?? string.Empty);
        }

        // -------- 派生摘要 --------

        public int PviCount => DesignProfile.Pvis.Count;
        public double AlignmentLength => Alignment.TotalLength;

        public string FootprintSummary
            => string.Format(CultureInfo.InvariantCulture,
                "PVI {0}  ·  Aln L={1:F2} m",
                PviCount, AlignmentLength);

        private void RaisePreview()
        {
            OnPropertyChanged(nameof(PviCount));
            OnPropertyChanged(nameof(AlignmentLength));
            OnPropertyChanged(nameof(FootprintSummary));
            PreviewRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler PreviewRequested;

        private void OnAlignmentChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V2ProfileAlignmentNode.TotalLength)
                || e.PropertyName == nameof(V2ProfileAlignmentNode.StartStation))
            {
                RaisePreview();
            }
        }

        private void OnPvisChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (V2PviNode p in e.NewItems) p.PropertyChanged += OnPviPropChanged;
            if (e.OldItems != null)
                foreach (V2PviNode p in e.OldItems) p.PropertyChanged -= OnPviPropChanged;

            DesignProfile.RefreshDerived();
            RaisePreview();
            RemoveSelectedPviCommand.RaiseCanExecuteChanged();
            MovePviUpCommand.RaiseCanExecuteChanged();
            MovePviDownCommand.RaiseCanExecuteChanged();
        }

        private void OnPviPropChanged(object sender, PropertyChangedEventArgs e)
        {
            // 主字段变动（桩号 / 高程 / 半径）才触发派生重算与重绘，
            // 派生字段自身变动不再递归。
            if (e.PropertyName == nameof(V2PviNode.Station)
                || e.PropertyName == nameof(V2PviNode.Elevation)
                || e.PropertyName == nameof(V2PviNode.CurveRadius))
            {
                DesignProfile.RefreshDerived();
                RaisePreview();
            }
        }

        // -------- 选中分发（五路互斥）--------

        private object _selectedNode;
        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value)) return;
                _selectedNode = value;

                _selectedAlignment = value as V2ProfileAlignmentNode;
                _selectedEg = value as V2ProfileExistingGroundNode;
                _selectedFg = value as V2ProfileDesignNode;
                _selectedPvi = value as V2PviNode;
                _selectedBandSet = value as V2BandSetNode;
                _selectedBandRow = value as V2BandRowNode;

                OnPropertyChanged(nameof(SelectedNode));
                OnPropertyChanged(nameof(SelectedAlignment));
                OnPropertyChanged(nameof(SelectedEg));
                OnPropertyChanged(nameof(SelectedFg));
                OnPropertyChanged(nameof(SelectedPvi));
                OnPropertyChanged(nameof(SelectedBandSet));
                OnPropertyChanged(nameof(SelectedBandRow));
                OnPropertyChanged(nameof(IsAlignmentSelected));
                OnPropertyChanged(nameof(IsEgSelected));
                OnPropertyChanged(nameof(IsFgSelected));
                OnPropertyChanged(nameof(IsPviSelected));
                OnPropertyChanged(nameof(IsBandSetSelected));
                OnPropertyChanged(nameof(IsBandRowSelected));
                OnPropertyChanged(nameof(SelectionPath));

                RemoveSelectedPviCommand.RaiseCanExecuteChanged();
                MovePviUpCommand.RaiseCanExecuteChanged();
                MovePviDownCommand.RaiseCanExecuteChanged();

                // 选中变更需要重绘以高亮 PVI
                PreviewRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private V2ProfileAlignmentNode _selectedAlignment;
        public V2ProfileAlignmentNode SelectedAlignment => _selectedAlignment;

        private V2ProfileExistingGroundNode _selectedEg;
        public V2ProfileExistingGroundNode SelectedEg => _selectedEg;

        private V2ProfileDesignNode _selectedFg;
        public V2ProfileDesignNode SelectedFg => _selectedFg;

        private V2PviNode _selectedPvi;
        public V2PviNode SelectedPvi => _selectedPvi;

        private V2BandSetNode _selectedBandSet;
        public V2BandSetNode SelectedBandSet => _selectedBandSet;

        private V2BandRowNode _selectedBandRow;
        public V2BandRowNode SelectedBandRow => _selectedBandRow;

        public bool IsAlignmentSelected => _selectedAlignment != null;
        public bool IsEgSelected => _selectedEg != null;
        public bool IsFgSelected => _selectedFg != null;
        public bool IsPviSelected => _selectedPvi != null;
        public bool IsBandSetSelected => _selectedBandSet != null || _selectedBandRow != null;
        public bool IsBandRowSelected => _selectedBandRow != null;

        public string SelectionPath
        {
            get
            {
                if (_selectedPvi != null)
                    return $"设计线 / {_selectedPvi.DisplayTitle}";
                if (_selectedFg != null)
                    return $"设计线 · {_selectedFg.SchemeName}";
                if (_selectedEg != null)
                    return "地面线 EG";
                if (_selectedAlignment != null)
                    return $"路线 · {_selectedAlignment.Name}";
                if (_selectedBandRow != null)
                    return $"数据条 / {_selectedBandRow.DisplayTitle}";
                if (_selectedBandSet != null)
                    return "数据条";
                return "未选中";
            }
        }

        // -------- 命令：PVI 增删 / 上下移动 --------

        public V2RelayCommand AddPviCommand { get; }
        public V2RelayCommand RemoveSelectedPviCommand { get; }
        public V2RelayCommand MovePviUpCommand { get; }
        public V2RelayCommand MovePviDownCommand { get; }

        private void AppendPvi()
        {
            // 在末段中点插入一个新 PVI，便于立即可见。
            double s;
            double e;
            if (DesignProfile.Pvis.Count >= 2)
            {
                var last = DesignProfile.Pvis[DesignProfile.Pvis.Count - 1];
                var prev = DesignProfile.Pvis[DesignProfile.Pvis.Count - 2];
                s = (prev.Station + last.Station) / 2;
                e = (prev.Elevation + last.Elevation) / 2;
            }
            else if (DesignProfile.Pvis.Count == 1)
            {
                var only = DesignProfile.Pvis[0];
                s = only.Station + 100;
                e = only.Elevation;
            }
            else
            {
                s = 0;
                e = 100;
            }

            var node = new V2PviNode(s, e);
            // 按桩号升序插入
            int insertAt = DesignProfile.Pvis.Count;
            for (int i = 0; i < DesignProfile.Pvis.Count; i++)
            {
                if (DesignProfile.Pvis[i].Station > s) { insertAt = i; break; }
            }
            DesignProfile.Pvis.Insert(insertAt, node);
            SelectedNode = node;
        }

        private void RemoveSelectedPvi()
        {
            if (_selectedPvi == null) return;
            if (DesignProfile.Pvis.Count <= 2) return;
            DesignProfile.Pvis.Remove(_selectedPvi);
            SelectedNode = null;
        }

        private bool CanMovePvi(bool up)
        {
            if (_selectedPvi == null) return false;
            int idx = DesignProfile.Pvis.IndexOf(_selectedPvi);
            if (idx < 0) return false;
            return up ? idx > 0 : idx < DesignProfile.Pvis.Count - 1;
        }

        private void MoveSelectedPviUp()
        {
            if (_selectedPvi == null) return;
            int idx = DesignProfile.Pvis.IndexOf(_selectedPvi);
            if (idx > 0) DesignProfile.Pvis.Move(idx, idx - 1);
            MovePviUpCommand.RaiseCanExecuteChanged();
            MovePviDownCommand.RaiseCanExecuteChanged();
        }

        private void MoveSelectedPviDown()
        {
            if (_selectedPvi == null) return;
            int idx = DesignProfile.Pvis.IndexOf(_selectedPvi);
            if (idx >= 0 && idx < DesignProfile.Pvis.Count - 1) DesignProfile.Pvis.Move(idx, idx + 1);
            MovePviUpCommand.RaiseCanExecuteChanged();
            MovePviDownCommand.RaiseCanExecuteChanged();
        }

        // -------- Demo 数据 --------

        private void LoadDemoData()
        {
            // 路线：L = 1698 m（与用户截图 HongYe 示例一致）
            Alignment.Name = "主线 A";
            Alignment.TotalLength = 1698.0;

            // 设计线：4 个 PVI（用户图中的凹曲线场景）
            DesignProfile.Pvis.Add(new V2PviNode(0.000, 18.00));
            DesignProfile.Pvis.Add(new V2PviNode(422.579, 17.460, 5000));     // 凹曲线
            DesignProfile.Pvis.Add(new V2PviNode(1034.940, 18.00, 3000));     // 凸曲线
            DesignProfile.Pvis.Add(new V2PviNode(1698.000, 18.60));

            // 地面线：EG 每 50m 一个采样点，基线 18.0 ± 0.5 m 的轻微波动
            var rand = new Random(20260421);
            for (double s = 0; s <= Alignment.TotalLength + 0.01; s += 50)
            {
                double wave = 0.3 * Math.Sin(s / 180.0) + 0.15 * Math.Cos(s / 73.0);
                double noise = (rand.NextDouble() - 0.5) * 0.2;
                ExistingGround.Samples.Add(new V2EgSampleRow
                {
                    Station = s,
                    Elevation = 18.00 + wave + noise,
                });
            }

            // 数据条：9 行，默认勾 5 行（桩号 / 设计高 / 自然高 / 纵坡+竖曲 / 平曲线）
            BandSet.AddRow(V2BandRowKind.PavementRise, enabled: false);
            BandSet.AddRow(V2BandRowKind.DesignElevation, enabled: true);
            BandSet.AddRow(V2BandRowKind.ExistingElevation, enabled: true);
            BandSet.AddRow(V2BandRowKind.DesignGradeAndVerticalCurve, enabled: true);
            BandSet.AddRow(V2BandRowKind.ServiceRoadElevation, enabled: false);
            BandSet.AddRow(V2BandRowKind.HorizontalCurve, enabled: true);
            BandSet.AddRow(V2BandRowKind.Intersection, enabled: false);
            BandSet.AddRow(V2BandRowKind.Superelevation, enabled: false);
            BandSet.AddRow(V2BandRowKind.StationLabel, enabled: true);

            // 数据条任一行勾选变化 → 重绘
            foreach (var row in BandSet.Bands)
            {
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(V2BandRowNode.IsEnabled))
                    {
                        RaisePreview();
                    }
                };
            }
        }
    }
}
