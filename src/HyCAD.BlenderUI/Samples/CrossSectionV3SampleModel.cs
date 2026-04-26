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
    public enum V3BandSide
    {
        Left = -1,
        Center = 0,
        Right = 1,
    }

    public enum V3BandKind
    {
        Pavement,
        NonMotorized,
        Sidewalk,
        GreenStrip,
        Separator,
    }

    public enum V3GeometrySourceKind
    {
        Manual,
        PickedFromCad,
        BoundToCad,
    }

    public enum V3LayerGroupKind
    {
        Surface,
        Base,
        Subbase,
    }

    public sealed class V3KindOption
    {
        public V3KindOption(V3BandKind kind, string label)
        {
            Kind = kind;
            Label = label ?? string.Empty;
        }

        public V3BandKind Kind { get; }

        public string Label { get; }
    }

    public sealed class V3SourceOption
    {
        public V3SourceOption(V3GeometrySourceKind kind, string label)
        {
            Kind = kind;
            Label = label ?? string.Empty;
        }

        public V3GeometrySourceKind Kind { get; }

        public string Label { get; }
    }

    public static class V3BandCatalog
    {
        public static string LabelOf(V3BandKind kind)
        {
            switch (kind)
            {
                case V3BandKind.Pavement: return "机动车道";
                case V3BandKind.NonMotorized: return "非机动车道";
                case V3BandKind.Sidewalk: return "人行道";
                case V3BandKind.GreenStrip: return "绿化带";
                case V3BandKind.Separator: return "分隔带";
                default: return "板块";
            }
        }

        public static string ShortLabelOf(V3BandKind kind)
        {
            switch (kind)
            {
                case V3BandKind.Pavement: return "机";
                case V3BandKind.NonMotorized: return "非";
                case V3BandKind.Sidewalk: return "人";
                case V3BandKind.GreenStrip: return "绿";
                case V3BandKind.Separator: return "隔";
                default: return "块";
            }
        }

        public static double DefaultWidthOf(V3BandKind kind)
        {
            switch (kind)
            {
                case V3BandKind.Pavement: return 3.5;
                case V3BandKind.NonMotorized: return 3.0;
                case V3BandKind.Sidewalk: return 4.0;
                case V3BandKind.GreenStrip: return 2.0;
                case V3BandKind.Separator: return 1.5;
                default: return 3.0;
            }
        }

        public static double DefaultSlopeOf(V3BandKind kind)
        {
            switch (kind)
            {
                case V3BandKind.Pavement: return -2.0;
                case V3BandKind.NonMotorized: return -1.5;
                case V3BandKind.Sidewalk: return 1.5;
                case V3BandKind.GreenStrip: return 0.0;
                case V3BandKind.Separator: return 0.0;
                default: return 0.0;
            }
        }

        public static int DefaultLaneCountOf(V3BandKind kind)
        {
            switch (kind)
            {
                case V3BandKind.Pavement: return 1;
                case V3BandKind.NonMotorized: return 1;
                default: return 0;
            }
        }

        public static bool SupportsStructure(V3BandKind kind)
        {
            return kind == V3BandKind.Pavement
                || kind == V3BandKind.NonMotorized
                || kind == V3BandKind.Sidewalk;
        }

        public static bool HasLaneCount(V3BandKind kind)
        {
            return kind == V3BandKind.Pavement
                || kind == V3BandKind.NonMotorized;
        }
    }

    public static class V3SourceCatalog
    {
        public static string LabelOf(V3GeometrySourceKind kind)
        {
            switch (kind)
            {
                case V3GeometrySourceKind.Manual: return "手动输入";
                case V3GeometrySourceKind.PickedFromCad: return "图上拾取";
                case V3GeometrySourceKind.BoundToCad: return "绑定图元";
                default: return "未知来源";
            }
        }
    }

    public static class V3StructureRules
    {
        public const int SurfaceMin = 2;
        public const int SurfaceMax = 3;
        public const int BaseMin = 1;
        public const int BaseMax = 5;
        public const int SubbaseMin = 1;
        public const int SubbaseMax = 2;

        public static string LabelOf(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface: return "面层";
                case V3LayerGroupKind.Base: return "基层";
                case V3LayerGroupKind.Subbase: return "垫层";
                default: return "结构层";
            }
        }

        public static int MinOf(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface: return SurfaceMin;
                case V3LayerGroupKind.Base: return BaseMin;
                case V3LayerGroupKind.Subbase: return SubbaseMin;
                default: return 0;
            }
        }

        public static int MaxOf(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface: return SurfaceMax;
                case V3LayerGroupKind.Base: return BaseMax;
                case V3LayerGroupKind.Subbase: return SubbaseMax;
                default: return 0;
            }
        }

        public static double DefaultThicknessOf(V3LayerGroupKind kind, int index)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface:
                    return index == 0 ? 0.04 : index == 1 ? 0.06 : 0.05;
                case V3LayerGroupKind.Base:
                    return index < 2 ? 0.18 : 0.16;
                case V3LayerGroupKind.Subbase:
                    return index == 0 ? 0.15 : 0.12;
                default:
                    return 0.10;
            }
        }

        public static string DefaultMaterialOf(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface: return "沥青混凝土";
                case V3LayerGroupKind.Base: return "水泥稳定碎石";
                case V3LayerGroupKind.Subbase: return "级配碎石";
                default: return string.Empty;
            }
        }

        public static string DefaultPatternOf(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface: return "AC";
                case V3LayerGroupKind.Base: return "JC";
                case V3LayerGroupKind.Subbase: return "SS";
                default: return string.Empty;
            }
        }
    }

    public static class V3NumberParser
    {
        public static bool TryParseLooseDouble(string text, out double value)
        {
            text = (text ?? string.Empty).Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            text = text.Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    public abstract class V3ObservableObject : INotifyPropertyChanged
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

    public sealed class V3RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public V3RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public abstract class V3OutlineNodeBase : V3ObservableObject
    {
        private bool _isSelected;
        private bool _isExpanded = true;

        public abstract string DisplayTitle { get; }

        public virtual string Summary => string.Empty;

        public virtual IEnumerable<V3OutlineNodeBase> Children => Array.Empty<V3OutlineNodeBase>();

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        internal void RaiseNodePresentation()
        {
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(Summary));
        }
    }

    public sealed class V3MedianNode : V3OutlineNodeBase
    {
        private bool _isActive = true;
        private double _widthM = 2.5;

        public override string DisplayTitle => "中央隔离带";

        public override string Summary => _isActive
            ? string.Format(CultureInfo.InvariantCulture, "{0:F2} m", _widthM)
            : "禁用";

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!SetProperty(ref _isActive, value)) return;
                RaiseNodePresentation();
                OnPropertyChanged(nameof(WidthText));
            }
        }

        public double WidthM
        {
            get => _widthM;
            set
            {
                double sanitized = value < 0 ? 0 : value;
                if (!SetProperty(ref _widthM, sanitized)) return;
                RaiseNodePresentation();
                OnPropertyChanged(nameof(WidthText));
            }
        }

        public string WidthText
        {
            get => _widthM.ToString("0.##", CultureInfo.InvariantCulture);
            set
            {
                if (!V3NumberParser.TryParseLooseDouble(value, out var parsed))
                {
                    OnPropertyChanged();
                    return;
                }

                WidthM = parsed;
            }
        }
    }

    public sealed class V3SideNode : V3OutlineNodeBase
    {
        private readonly string _baseTitle;
        private readonly List<V3BandNode> _subscriptions = new List<V3BandNode>();

        public V3SideNode(string baseTitle, V3BandSide side)
        {
            _baseTitle = baseTitle ?? string.Empty;
            Side = side;
            Bands = new ObservableCollection<V3BandNode>();
            Bands.CollectionChanged += OnBandsChanged;
        }

        public V3BandSide Side { get; }

        public string SideLabel => Side == V3BandSide.Left ? "左侧" : "右侧";

        public string DirectionLabel => Side == V3BandSide.Left ? "e = (-1, 0)" : "e = (1, 0)";

        public ObservableCollection<V3BandNode> Bands { get; }

        public int BandCount => Bands.Count;

        public double ActiveWidth => Bands.Where(x => x.IsActive).Sum(x => x.WidthM);

        public override string DisplayTitle => _baseTitle;

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0} 块 / {1:F2} m", BandCount, ActiveWidth);

        public override IEnumerable<V3OutlineNodeBase> Children => Bands;

        private void OnBandsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshBandState();
            RaiseNodePresentation();
            OnPropertyChanged(nameof(BandCount));
            OnPropertyChanged(nameof(ActiveWidth));
        }

        private void RefreshBandState()
        {
            foreach (var band in _subscriptions)
                band.PropertyChanged -= OnBandChanged;
            _subscriptions.Clear();

            for (int i = 0; i < Bands.Count; i++)
            {
                var band = Bands[i];
                band.OwnerSide = this;
                band.Side = Side;
                band.OrderIndex = i + 1;
                band.PropertyChanged += OnBandChanged;
                _subscriptions.Add(band);
            }
        }

        private void OnBandChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V3BandNode.WidthM)
                || e.PropertyName == nameof(V3BandNode.IsActive)
                || e.PropertyName == nameof(V3BandNode.Kind))
            {
                RaiseNodePresentation();
                OnPropertyChanged(nameof(ActiveWidth));
            }
        }
    }

    public sealed class V3BandNode : V3OutlineNodeBase
    {
        private readonly List<V3LayerGroupNode> _subscriptions = new List<V3LayerGroupNode>();
        private V3BandSide _side;
        private int _orderIndex;
        private bool _isActive = true;
        private V3BandKind _kind;
        private string _name;
        private double _widthM;
        private double _slopePct;
        private int _laneCount;
        private V3GeometrySourceKind _geometrySource;
        private string _cadObjectHint = string.Empty;

        public V3BandNode(V3BandSide side, V3BandKind kind, string name = null)
        {
            _side = side;
            _kind = kind;
            _name = string.IsNullOrWhiteSpace(name) ? V3BandCatalog.LabelOf(kind) : name.Trim();
            _widthM = V3BandCatalog.DefaultWidthOf(kind);
            _slopePct = V3BandCatalog.DefaultSlopeOf(kind);
            _laneCount = V3BandCatalog.DefaultLaneCountOf(kind);
            _geometrySource = V3GeometrySourceKind.Manual;

            LayerGroups = new ObservableCollection<V3LayerGroupNode>();
            LayerGroups.CollectionChanged += OnGroupsChanged;
            EnsureGroupsForKind();
        }

        public V3SideNode OwnerSide { get; internal set; }

        public override IEnumerable<V3OutlineNodeBase> Children => LayerGroups;

        public ObservableCollection<V3LayerGroupNode> LayerGroups { get; }

        public V3BandSide Side
        {
            get => _side;
            internal set
            {
                if (!SetProperty(ref _side, value)) return;
                RaisePresentation();
            }
        }

        public int OrderIndex
        {
            get => _orderIndex;
            internal set
            {
                if (!SetProperty(ref _orderIndex, value)) return;
                RaisePresentation();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!SetProperty(ref _isActive, value)) return;
                RaisePresentation();
            }
        }

        public V3BandKind Kind
        {
            get => _kind;
            set
            {
                if (!SetProperty(ref _kind, value)) return;

                if (!HasLaneCount) _laneCount = 0;
                else if (_laneCount <= 0) _laneCount = V3BandCatalog.DefaultLaneCountOf(value);

                if (_widthM <= 0) _widthM = V3BandCatalog.DefaultWidthOf(value);
                EnsureGroupsForKind();
                RaisePresentation();
                OnPropertyChanged(nameof(HasLaneCount));
                OnPropertyChanged(nameof(HasStructureGroups));
                OnPropertyChanged(nameof(KindLabel));
                OnPropertyChanged(nameof(KindShortLabel));
                OnPropertyChanged(nameof(StructureSummary));
                OnPropertyChanged(nameof(TotalStructureThicknessM));
                OnPropertyChanged(nameof(SurfaceGroup));
                OnPropertyChanged(nameof(BaseGroup));
                OnPropertyChanged(nameof(SubbaseGroup));
            }
        }

        public string KindLabel => V3BandCatalog.LabelOf(_kind);

        public string KindShortLabel => V3BandCatalog.ShortLabelOf(_kind);

        public string Name
        {
            get => _name;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? V3BandCatalog.LabelOf(_kind) : value.Trim();
                if (!SetProperty(ref _name, normalized)) return;
                RaisePresentation();
            }
        }

        public double WidthM
        {
            get => _widthM;
            set
            {
                double sanitized = value <= 0 ? 0.01 : value;
                if (!SetProperty(ref _widthM, sanitized)) return;
                RaisePresentation();
                OnPropertyChanged(nameof(WidthText));
            }
        }

        public string WidthText
        {
            get => _widthM.ToString("0.##", CultureInfo.InvariantCulture);
            set
            {
                if (!V3NumberParser.TryParseLooseDouble(value, out var parsed))
                {
                    OnPropertyChanged();
                    return;
                }

                WidthM = parsed;
            }
        }

        public double SlopePct
        {
            get => _slopePct;
            set
            {
                double sanitized = Math.Max(-20, Math.Min(20, value));
                if (!SetProperty(ref _slopePct, sanitized)) return;
                RaisePresentation();
            }
        }

        public int LaneCount
        {
            get => _laneCount;
            set
            {
                int sanitized = value < 0 ? 0 : value;
                if (!SetProperty(ref _laneCount, sanitized)) return;
                RaisePresentation();
            }
        }

        public bool HasLaneCount => V3BandCatalog.HasLaneCount(_kind);

        public V3GeometrySourceKind GeometrySource
        {
            get => _geometrySource;
            set
            {
                if (!SetProperty(ref _geometrySource, value)) return;
                RaisePresentation();
                OnPropertyChanged(nameof(GeometrySourceLabel));
            }
        }

        public string GeometrySourceLabel => V3SourceCatalog.LabelOf(_geometrySource);

        public string CadObjectHint
        {
            get => _cadObjectHint;
            set => SetProperty(ref _cadObjectHint, value ?? string.Empty);
        }

        public bool HasStructureGroups => V3BandCatalog.SupportsStructure(_kind);

        public V3LayerGroupNode SurfaceGroup => LayerGroups.FirstOrDefault(x => x.GroupKind == V3LayerGroupKind.Surface);

        public V3LayerGroupNode BaseGroup => LayerGroups.FirstOrDefault(x => x.GroupKind == V3LayerGroupKind.Base);

        public V3LayerGroupNode SubbaseGroup => LayerGroups.FirstOrDefault(x => x.GroupKind == V3LayerGroupKind.Subbase);

        public double TotalStructureThicknessM => LayerGroups.Sum(x => x.TotalThicknessM);

        public string StructureSummary
        {
            get
            {
                if (!HasStructureGroups) return "无第二层 / 第三层";
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "面 {0} / 基 {1} / 垫 {2} · {3:F3} m",
                    SurfaceGroup?.Layers.Count ?? 0,
                    BaseGroup?.Layers.Count ?? 0,
                    SubbaseGroup?.Layers.Count ?? 0,
                    TotalStructureThicknessM);
            }
        }

        public string DetailSummary
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · 坡 {1:F1}% · {2} · {3}",
                    KindLabel,
                    SlopePct,
                    GeometrySourceLabel,
                    StructureSummary);
            }
        }

        public override string DisplayTitle
        {
            get
            {
                if (_orderIndex <= 0) return _name;
                return string.Format(CultureInfo.InvariantCulture, "{0}. {1}", _orderIndex, _name);
            }
        }

        public override string Summary => _isActive
            ? string.Format(CultureInfo.InvariantCulture, "{0:F2} m", _widthM)
            : "禁用";

        public V3LayerNode AddLayer(V3LayerGroupKind kind)
        {
            var group = LayerGroups.FirstOrDefault(x => x.GroupKind == kind);
            return group?.AddDefaultLayer();
        }

        private void EnsureGroupsForKind()
        {
            if (!HasStructureGroups)
            {
                if (LayerGroups.Count == 0) return;
                LayerGroups.Clear();
                return;
            }

            if (LayerGroups.Count > 0) return;

            LayerGroups.Add(new V3LayerGroupNode(this, V3LayerGroupKind.Surface));
            LayerGroups.Add(new V3LayerGroupNode(this, V3LayerGroupKind.Base));
            LayerGroups.Add(new V3LayerGroupNode(this, V3LayerGroupKind.Subbase));
        }

        private void OnGroupsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshGroupSubscriptions();
            NotifyStructureChanged();
        }

        private void RefreshGroupSubscriptions()
        {
            foreach (var group in _subscriptions)
                group.PropertyChanged -= OnGroupChanged;
            _subscriptions.Clear();

            foreach (var group in LayerGroups)
            {
                group.ParentBand = this;
                group.PropertyChanged += OnGroupChanged;
                _subscriptions.Add(group);
            }
        }

        private void OnGroupChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyStructureChanged();
        }

        internal void NotifyStructureChanged()
        {
            OnPropertyChanged(nameof(HasStructureGroups));
            OnPropertyChanged(nameof(SurfaceGroup));
            OnPropertyChanged(nameof(BaseGroup));
            OnPropertyChanged(nameof(SubbaseGroup));
            OnPropertyChanged(nameof(StructureSummary));
            OnPropertyChanged(nameof(TotalStructureThicknessM));
            RaisePresentation();
        }

        private void RaisePresentation()
        {
            RaiseNodePresentation();
            OnPropertyChanged(nameof(DetailSummary));
            OnPropertyChanged(nameof(WidthText));
            OnPropertyChanged(nameof(GeometrySourceLabel));
            OnPropertyChanged(nameof(KindLabel));
            OnPropertyChanged(nameof(KindShortLabel));
            OnPropertyChanged(nameof(HasLaneCount));
            OnPropertyChanged(nameof(StructureSummary));
            OnPropertyChanged(nameof(TotalStructureThicknessM));
        }
    }

    public sealed class V3LayerGroupNode : V3OutlineNodeBase
    {
        private readonly List<V3LayerNode> _subscriptions = new List<V3LayerNode>();

        public V3LayerGroupNode(V3BandNode parentBand, V3LayerGroupKind groupKind)
        {
            ParentBand = parentBand;
            GroupKind = groupKind;
            Layers = new ObservableCollection<V3LayerNode>();
            Layers.CollectionChanged += OnLayersChanged;

            EnsureMinimumLayers();
        }

        public V3BandNode ParentBand { get; internal set; }

        public V3LayerGroupKind GroupKind { get; }

        public ObservableCollection<V3LayerNode> Layers { get; }

        public override IEnumerable<V3OutlineNodeBase> Children => Layers;

        public int MinCount => V3StructureRules.MinOf(GroupKind);

        public int MaxCount => V3StructureRules.MaxOf(GroupKind);

        public int LayerCount => Layers.Count;

        public double TotalThicknessM => Layers.Sum(x => x.ThicknessM);

        public bool CanAdd => LayerCount < MaxCount;

        public override string DisplayTitle => V3StructureRules.LabelOf(GroupKind) + "组";

        public override string Summary
            => string.Format(CultureInfo.InvariantCulture, "{0}/{1}~{2} · {3:F3} m", LayerCount, MinCount, MaxCount, TotalThicknessM);

        public string RuleSummary => string.Format(CultureInfo.InvariantCulture, "允许 {0} ~ {1} 层", MinCount, MaxCount);

        public V3LayerNode AddDefaultLayer()
        {
            if (!CanAdd) return null;

            var layer = new V3LayerNode
            {
                ParentGroup = this,
                Name = V3StructureRules.LabelOf(GroupKind) + (Layers.Count + 1),
                ThicknessM = V3StructureRules.DefaultThicknessOf(GroupKind, Layers.Count),
                Material = V3StructureRules.DefaultMaterialOf(GroupKind),
                Pattern = V3StructureRules.DefaultPatternOf(GroupKind),
            };

            Layers.Add(layer);
            return layer;
        }

        public bool CanRemove(V3LayerNode layer)
        {
            if (layer == null) return false;
            return Layers.Contains(layer) && LayerCount > MinCount;
        }

        public bool RemoveLayer(V3LayerNode layer)
        {
            if (!CanRemove(layer)) return false;
            return Layers.Remove(layer);
        }

        private void EnsureMinimumLayers()
        {
            while (Layers.Count < MinCount)
                AddDefaultLayer();
        }

        private void OnLayersChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshLayerSubscriptions();
            RaisePresentation();
        }

        private void RefreshLayerSubscriptions()
        {
            foreach (var layer in _subscriptions)
                layer.PropertyChanged -= OnLayerChanged;
            _subscriptions.Clear();

            foreach (var layer in Layers)
            {
                layer.ParentGroup = this;
                layer.PropertyChanged += OnLayerChanged;
                _subscriptions.Add(layer);
            }
        }

        private void OnLayerChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePresentation();
        }

        private void RaisePresentation()
        {
            RaiseNodePresentation();
            OnPropertyChanged(nameof(LayerCount));
            OnPropertyChanged(nameof(TotalThicknessM));
            OnPropertyChanged(nameof(CanAdd));
            OnPropertyChanged(nameof(RuleSummary));
            ParentBand?.NotifyStructureChanged();
        }
    }

    public sealed class V3LayerNode : V3OutlineNodeBase
    {
        private string _name = "结构层";
        private double _thicknessM = 0.10;
        private string _material = string.Empty;
        private string _pattern = string.Empty;
        private string _note = string.Empty;

        public V3LayerGroupNode ParentGroup { get; internal set; }

        public string GroupLabel => ParentGroup == null ? string.Empty : V3StructureRules.LabelOf(ParentGroup.GroupKind);

        public string Name
        {
            get => _name;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "结构层" : value.Trim();
                if (!SetProperty(ref _name, normalized)) return;
                RaiseNodePresentation();
            }
        }

        public double ThicknessM
        {
            get => _thicknessM;
            set
            {
                double sanitized = value < 0.001 ? 0.001 : value;
                if (!SetProperty(ref _thicknessM, sanitized)) return;
                RaiseNodePresentation();
            }
        }

        public string Material
        {
            get => _material;
            set => SetProperty(ref _material, value ?? string.Empty);
        }

        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value ?? string.Empty);
        }

        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value ?? string.Empty);
        }

        public override string DisplayTitle => _name;

        public override string Summary => string.Format(CultureInfo.InvariantCulture, "{0:F3} m", _thicknessM);
    }

    public sealed class V3CrossSectionSampleViewModel : V3ObservableObject
    {
        private readonly List<V3BandNode> _bandSubscriptions = new List<V3BandNode>();
        private object _selectedNode;
        private string _title = "rCs 横断面 v3 - 建模工作台原型";
        private int _scaleDenominator = 150;
        private int _designSpeed = 40;
        private bool _useAutoCadDimension = true;
        private bool _useElevationLeader = true;
        private bool _preferModelSpacePreview = true;
        private V3MedianNode _selectedMedian;
        private V3SideNode _selectedSide;
        private V3BandNode _selectedBand;
        private V3LayerGroupNode _selectedGroup;
        private V3LayerNode _selectedLayer;
        private bool _isSyncingSelection;

        public V3CrossSectionSampleViewModel()
        {
            Median = new V3MedianNode();
            LeftSide = new V3SideNode("左侧板块容器", V3BandSide.Left);
            RightSide = new V3SideNode("右侧板块容器", V3BandSide.Right);

            OutlineRoots = new ReadOnlyCollection<V3OutlineNodeBase>(
                new List<V3OutlineNodeBase> { Median, LeftSide, RightSide });

            Median.PropertyChanged += OnMedianChanged;
            LeftSide.Bands.CollectionChanged += OnBandsChanged;
            RightSide.Bands.CollectionChanged += OnBandsChanged;

            AddLeftBandCommand = new V3RelayCommand(() => AddBand(V3BandSide.Left, V3BandKind.Pavement));
            AddRightBandCommand = new V3RelayCommand(() => AddBand(V3BandSide.Right, V3BandKind.Pavement));

            AddPavementBandCommand = new V3RelayCommand(() => AddBandToActiveSide(V3BandKind.Pavement));
            AddNonMotorizedBandCommand = new V3RelayCommand(() => AddBandToActiveSide(V3BandKind.NonMotorized));
            AddSidewalkBandCommand = new V3RelayCommand(() => AddBandToActiveSide(V3BandKind.Sidewalk));
            AddGreenStripBandCommand = new V3RelayCommand(() => AddBandToActiveSide(V3BandKind.GreenStrip));
            AddSeparatorBandCommand = new V3RelayCommand(() => AddBandToActiveSide(V3BandKind.Separator));

            RemoveSelectedCommand = new V3RelayCommand(RemoveSelectedNode, () => SelectedBand != null || SelectedLayer != null);
            MoveUpCommand = new V3RelayCommand(MoveSelectedUp, () => CanMoveSelected(true));
            MoveDownCommand = new V3RelayCommand(MoveSelectedDown, () => CanMoveSelected(false));

            AddSurfaceLayerCommand = new V3RelayCommand(() => AddLayer(V3LayerGroupKind.Surface), () => CanAddLayer(V3LayerGroupKind.Surface));
            AddBaseLayerCommand = new V3RelayCommand(() => AddLayer(V3LayerGroupKind.Base), () => CanAddLayer(V3LayerGroupKind.Base));
            AddSubbaseLayerCommand = new V3RelayCommand(() => AddLayer(V3LayerGroupKind.Subbase), () => CanAddLayer(V3LayerGroupKind.Subbase));
            AddLayerToSelectedGroupCommand = new V3RelayCommand(AddLayerToSelectedGroup, () => ActiveGroup != null && ActiveGroup.CanAdd);
            RemoveSelectedLayerCommand = new V3RelayCommand(RemoveSelectedLayer, () => SelectedLayer != null && SelectedLayer.ParentGroup != null && SelectedLayer.ParentGroup.CanRemove(SelectedLayer));

            LoadDemoLayout();
            RefreshBandSubscriptions();
            RaiseFootprint();
        }

        public static IReadOnlyList<int> AvailableScales { get; } = new[] { 50, 100, 150, 200, 250 };

        public static IReadOnlyList<int> AvailableSpeeds { get; } = new[] { 20, 30, 40, 50, 60, 80 };

        public static IReadOnlyList<V3KindOption> AvailableKinds { get; } = new[]
        {
            new V3KindOption(V3BandKind.Pavement, "机动车道"),
            new V3KindOption(V3BandKind.NonMotorized, "非机动车道"),
            new V3KindOption(V3BandKind.Sidewalk, "人行道"),
            new V3KindOption(V3BandKind.GreenStrip, "绿化带"),
            new V3KindOption(V3BandKind.Separator, "分隔带"),
        };

        public static IReadOnlyList<V3SourceOption> AvailableSourceOptions { get; } = new[]
        {
            new V3SourceOption(V3GeometrySourceKind.Manual, "手动输入"),
            new V3SourceOption(V3GeometrySourceKind.PickedFromCad, "图上拾取"),
            new V3SourceOption(V3GeometrySourceKind.BoundToCad, "绑定图元"),
        };

        public event EventHandler PreviewRequested;

        public V3MedianNode Median { get; }

        public V3SideNode LeftSide { get; }

        public V3SideNode RightSide { get; }

        public IReadOnlyList<V3OutlineNodeBase> OutlineRoots { get; }

        public string Title
        {
            get => _title;
            set
            {
                if (!SetProperty(ref _title, value ?? string.Empty)) return;
                RaisePreview();
            }
        }

        public int ScaleDenominator
        {
            get => _scaleDenominator;
            set
            {
                int normalized = value <= 0 ? 150 : value;
                if (!SetProperty(ref _scaleDenominator, normalized)) return;
                RaisePreview();
            }
        }

        public int DesignSpeed
        {
            get => _designSpeed;
            set
            {
                if (!SetProperty(ref _designSpeed, value)) return;
                RaisePreview();
            }
        }

        public bool UseAutoCadDimension
        {
            get => _useAutoCadDimension;
            set => SetProperty(ref _useAutoCadDimension, value);
        }

        public bool UseElevationLeader
        {
            get => _useElevationLeader;
            set => SetProperty(ref _useElevationLeader, value);
        }

        public bool PreferModelSpacePreview
        {
            get => _preferModelSpacePreview;
            set => SetProperty(ref _preferModelSpacePreview, value);
        }

        public double LeftHalfWidth => LeftSide.Bands.Where(x => x.IsActive).Sum(x => x.WidthM);

        public double RightHalfWidth => RightSide.Bands.Where(x => x.IsActive).Sum(x => x.WidthM);

        public double MedianWidth => Median.IsActive ? Median.WidthM : 0;

        public double TotalWidth => LeftHalfWidth + MedianWidth + RightHalfWidth;

        public string FootprintSummary
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "左 {0:F2} m / 中 {1:F2} m / 右 {2:F2} m  ·  总路幅 {3:F2} m",
                    LeftHalfWidth,
                    MedianWidth,
                    RightHalfWidth,
                    TotalWidth);
            }
        }

        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value)) return;

                _selectedNode = value;
                _selectedMedian = null;
                _selectedSide = null;
                _selectedBand = null;
                _selectedGroup = null;
                _selectedLayer = null;

                if (value is V3MedianNode median)
                {
                    _selectedMedian = median;
                }
                else if (value is V3SideNode side)
                {
                    _selectedSide = side;
                }
                else if (value is V3BandNode band)
                {
                    _selectedBand = band;
                }
                else if (value is V3LayerGroupNode group)
                {
                    _selectedGroup = group;
                    _selectedBand = group.ParentBand;
                }
                else if (value is V3LayerNode layer)
                {
                    _selectedLayer = layer;
                    _selectedGroup = layer.ParentGroup;
                    _selectedBand = layer.ParentGroup?.ParentBand;
                }

                SyncTreeSelection();
                RaiseSelectionState();
                RaiseCommandsChanged();
                RaisePreview();
            }
        }

        public V3MedianNode SelectedMedian => _selectedMedian;

        public V3SideNode SelectedSide => _selectedSide;

        public V3BandNode SelectedBand => _selectedBand;

        public V3LayerGroupNode ActiveGroup => _selectedGroup;

        public V3BandNode ActiveBand => _selectedBand;

        public V3LayerNode SelectedLayer => _selectedLayer;

        public bool IsMedianSelected => _selectedMedian != null;

        public bool IsSideSelected => _selectedSide != null;

        public bool IsBandSelected => _selectedNode is V3BandNode;

        public bool IsGroupSelected => _selectedNode is V3LayerGroupNode;

        public bool IsLayerSelected => _selectedNode is V3LayerNode;

        public bool HasActiveBand => ActiveBand != null;

        public bool HasActiveGroup => ActiveGroup != null;

        public bool HasActiveLayer => SelectedLayer != null;

        public string SelectionPath
        {
            get
            {
                if (SelectedLayer != null)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} / {1} / {2} / {3}",
                        SideLabel(SelectedBand?.Side ?? V3BandSide.Center),
                        SelectedBand?.DisplayTitle ?? string.Empty,
                        ActiveGroup?.DisplayTitle ?? string.Empty,
                        SelectedLayer.DisplayTitle);
                }

                if (ActiveGroup != null)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} / {1} / {2}",
                        SideLabel(SelectedBand?.Side ?? V3BandSide.Center),
                        SelectedBand?.DisplayTitle ?? string.Empty,
                        ActiveGroup.DisplayTitle);
                }

                if (SelectedBand != null)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} / {1}",
                        SideLabel(SelectedBand.Side),
                        SelectedBand.DisplayTitle);
                }

                if (SelectedSide != null)
                    return string.Format(CultureInfo.InvariantCulture, "{0}（从中心向边界）", SelectedSide.SideLabel);

                if (SelectedMedian != null)
                    return "中央隔离带";

                return "未选中";
            }
        }

        public V3RelayCommand AddLeftBandCommand { get; }

        public V3RelayCommand AddRightBandCommand { get; }

        public V3RelayCommand AddPavementBandCommand { get; }

        public V3RelayCommand AddNonMotorizedBandCommand { get; }

        public V3RelayCommand AddSidewalkBandCommand { get; }

        public V3RelayCommand AddGreenStripBandCommand { get; }

        public V3RelayCommand AddSeparatorBandCommand { get; }

        public V3RelayCommand RemoveSelectedCommand { get; }

        public V3RelayCommand MoveUpCommand { get; }

        public V3RelayCommand MoveDownCommand { get; }

        public V3RelayCommand AddSurfaceLayerCommand { get; }

        public V3RelayCommand AddBaseLayerCommand { get; }

        public V3RelayCommand AddSubbaseLayerCommand { get; }

        public V3RelayCommand AddLayerToSelectedGroupCommand { get; }

        public V3RelayCommand RemoveSelectedLayerCommand { get; }

        private void AddBandToActiveSide(V3BandKind kind)
        {
            var side = ResolveActiveSideForInsertion();
            AddBand(side, kind);
        }

        private V3BandSide ResolveActiveSideForInsertion()
        {
            if (SelectedSide != null) return SelectedSide.Side;
            if (SelectedBand != null) return SelectedBand.Side;
            return V3BandSide.Left;
        }

        private void AddBand(V3BandSide side, V3BandKind kind)
        {
            var owner = side == V3BandSide.Right ? RightSide : LeftSide;
            var band = new V3BandNode(side, kind, CreateBandName(owner, kind));
            owner.Bands.Add(band);
            SelectedNode = band;
        }

        private string CreateBandName(V3SideNode owner, V3BandKind kind)
        {
            int index = owner.Bands.Count(x => x.Kind == kind) + 1;
            return V3BandCatalog.LabelOf(kind) + index;
        }

        private void RemoveSelectedNode()
        {
            if (SelectedLayer != null)
            {
                RemoveSelectedLayer();
                return;
            }

            if (SelectedBand == null || SelectedBand.OwnerSide == null) return;
            var side = SelectedBand.OwnerSide;
            side.Bands.Remove(SelectedBand);
            SelectedNode = side;
        }

        private bool CanMoveSelected(bool moveTowardCenter)
        {
            if (SelectedBand == null || SelectedBand.OwnerSide == null) return false;
            var bands = SelectedBand.OwnerSide.Bands;
            int index = bands.IndexOf(SelectedBand);
            if (index < 0) return false;
            return moveTowardCenter ? index > 0 : index < bands.Count - 1;
        }

        private void MoveSelectedUp()
        {
            if (!CanMoveSelected(true)) return;
            var bands = SelectedBand.OwnerSide.Bands;
            int index = bands.IndexOf(SelectedBand);
            bands.Move(index, index - 1);
            RaiseCommandsChanged();
        }

        private void MoveSelectedDown()
        {
            if (!CanMoveSelected(false)) return;
            var bands = SelectedBand.OwnerSide.Bands;
            int index = bands.IndexOf(SelectedBand);
            bands.Move(index, index + 1);
            RaiseCommandsChanged();
        }

        private bool CanAddLayer(V3LayerGroupKind kind)
        {
            var band = ActiveBand;
            if (band == null || !band.HasStructureGroups) return false;
            var group = band.LayerGroups.FirstOrDefault(x => x.GroupKind == kind);
            return group != null && group.CanAdd;
        }

        private void AddLayer(V3LayerGroupKind kind)
        {
            var band = ActiveBand;
            if (band == null) return;
            var layer = band.AddLayer(kind);
            if (layer != null) SelectedNode = layer;
            RaiseCommandsChanged();
        }

        private void AddLayerToSelectedGroup()
        {
            if (ActiveGroup == null) return;
            var layer = ActiveGroup.AddDefaultLayer();
            if (layer != null) SelectedNode = layer;
            RaiseCommandsChanged();
        }

        private void RemoveSelectedLayer()
        {
            if (SelectedLayer == null || SelectedLayer.ParentGroup == null) return;
            var parentGroup = SelectedLayer.ParentGroup;
            if (!parentGroup.RemoveLayer(SelectedLayer)) return;

            SelectedNode = parentGroup;
            RaiseCommandsChanged();
        }

        private void OnMedianChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(V3MedianNode.IsActive)
                || e.PropertyName == nameof(V3MedianNode.WidthM))
            {
                RaiseFootprint();
            }
        }

        private void OnBandsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshBandSubscriptions();
            RaiseFootprint();
            RaiseCommandsChanged();
        }

        private void RefreshBandSubscriptions()
        {
            foreach (var band in _bandSubscriptions)
                band.PropertyChanged -= OnBandChanged;
            _bandSubscriptions.Clear();

            foreach (var band in LeftSide.Bands)
            {
                band.PropertyChanged += OnBandChanged;
                _bandSubscriptions.Add(band);
            }

            foreach (var band in RightSide.Bands)
            {
                band.PropertyChanged += OnBandChanged;
                _bandSubscriptions.Add(band);
            }
        }

        private void OnBandChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not V3BandNode band) return;

            if (e.PropertyName == nameof(V3BandNode.Kind)
                && SelectedBand == band
                && !band.HasStructureGroups
                && (SelectedLayer != null || ActiveGroup != null))
            {
                SelectedNode = band;
                return;
            }

            if (e.PropertyName == nameof(V3BandNode.WidthM)
                || e.PropertyName == nameof(V3BandNode.IsActive)
                || e.PropertyName == nameof(V3BandNode.Kind)
                || e.PropertyName == nameof(V3BandNode.SlopePct)
                || e.PropertyName == nameof(V3BandNode.GeometrySource)
                || e.PropertyName == nameof(V3BandNode.StructureSummary)
                || e.PropertyName == nameof(V3BandNode.TotalStructureThicknessM)
                || e.PropertyName == nameof(V3BandNode.Name))
            {
                RaiseFootprint();
                RaiseCommandsChanged();
            }
        }

        private void RaiseFootprint()
        {
            OnPropertyChanged(nameof(LeftHalfWidth));
            OnPropertyChanged(nameof(RightHalfWidth));
            OnPropertyChanged(nameof(MedianWidth));
            OnPropertyChanged(nameof(TotalWidth));
            OnPropertyChanged(nameof(FootprintSummary));
            OnPropertyChanged(nameof(SelectionPath));
            RaisePreview();
        }

        private void RaisePreview()
            => PreviewRequested?.Invoke(this, EventArgs.Empty);

        private void RaiseSelectionState()
        {
            OnPropertyChanged(nameof(SelectedNode));
            OnPropertyChanged(nameof(SelectedMedian));
            OnPropertyChanged(nameof(SelectedSide));
            OnPropertyChanged(nameof(SelectedBand));
            OnPropertyChanged(nameof(ActiveBand));
            OnPropertyChanged(nameof(ActiveGroup));
            OnPropertyChanged(nameof(SelectedLayer));
            OnPropertyChanged(nameof(IsMedianSelected));
            OnPropertyChanged(nameof(IsSideSelected));
            OnPropertyChanged(nameof(IsBandSelected));
            OnPropertyChanged(nameof(IsGroupSelected));
            OnPropertyChanged(nameof(IsLayerSelected));
            OnPropertyChanged(nameof(HasActiveBand));
            OnPropertyChanged(nameof(HasActiveGroup));
            OnPropertyChanged(nameof(HasActiveLayer));
            OnPropertyChanged(nameof(SelectionPath));
        }

        private void RaiseCommandsChanged()
        {
            RemoveSelectedCommand.RaiseCanExecuteChanged();
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
            AddSurfaceLayerCommand.RaiseCanExecuteChanged();
            AddBaseLayerCommand.RaiseCanExecuteChanged();
            AddSubbaseLayerCommand.RaiseCanExecuteChanged();
            AddLayerToSelectedGroupCommand.RaiseCanExecuteChanged();
            RemoveSelectedLayerCommand.RaiseCanExecuteChanged();
        }

        private void SyncTreeSelection()
        {
            if (_isSyncingSelection) return;
            _isSyncingSelection = true;
            try
            {
                foreach (var node in EnumerateNodes())
                    node.IsSelected = ReferenceEquals(node, _selectedNode);
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private IEnumerable<V3OutlineNodeBase> EnumerateNodes()
        {
            foreach (var root in OutlineRoots)
            {
                foreach (var item in EnumerateNode(root))
                    yield return item;
            }
        }

        private IEnumerable<V3OutlineNodeBase> EnumerateNode(V3OutlineNodeBase node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var nested in EnumerateNode(child))
                    yield return nested;
            }
        }

        private void LoadDemoLayout()
        {
            Median.IsActive = true;
            Median.WidthM = 2.5;

            var left1 = new V3BandNode(V3BandSide.Left, V3BandKind.Pavement, "机动车道1")
            {
                WidthM = 3.75,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.BoundToCad,
                CadObjectHint = "来源对象：PL-101（控制边线）",
            };
            left1.AddLayer(V3LayerGroupKind.Surface);
            left1.AddLayer(V3LayerGroupKind.Base);

            var left2 = new V3BandNode(V3BandSide.Left, V3BandKind.Pavement, "机动车道2")
            {
                WidthM = 3.50,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.BoundToCad,
                CadObjectHint = "来源对象：PL-102（中心车道）",
            };

            var left3 = new V3BandNode(V3BandSide.Left, V3BandKind.Pavement, "机动车道3")
            {
                WidthM = 3.50,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.PickedFromCad,
                CadObjectHint = "预留：从图上拾取边界宽度",
            };

            var left4 = new V3BandNode(V3BandSide.Left, V3BandKind.Separator, "机非分隔带")
            {
                WidthM = 1.50,
                SlopePct = 0.0,
            };

            var left5 = new V3BandNode(V3BandSide.Left, V3BandKind.NonMotorized, "非机动车道")
            {
                WidthM = 3.00,
                SlopePct = -1.5,
                GeometrySource = V3GeometrySourceKind.Manual,
            };

            var left6 = new V3BandNode(V3BandSide.Left, V3BandKind.Sidewalk, "人行道")
            {
                WidthM = 4.00,
                SlopePct = 1.5,
                GeometrySource = V3GeometrySourceKind.Manual,
            };

            var left7 = new V3BandNode(V3BandSide.Left, V3BandKind.GreenStrip, "绿化带")
            {
                WidthM = 2.00,
                SlopePct = 0.0,
            };

            LeftSide.Bands.Add(left1);
            LeftSide.Bands.Add(left2);
            LeftSide.Bands.Add(left3);
            LeftSide.Bands.Add(left4);
            LeftSide.Bands.Add(left5);
            LeftSide.Bands.Add(left6);
            LeftSide.Bands.Add(left7);

            var right1 = new V3BandNode(V3BandSide.Right, V3BandKind.Pavement, "机动车道1")
            {
                WidthM = 3.75,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.BoundToCad,
                CadObjectHint = "来源对象：PL-201（控制边线）",
            };

            var right2 = new V3BandNode(V3BandSide.Right, V3BandKind.Pavement, "机动车道2")
            {
                WidthM = 3.50,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.BoundToCad,
                CadObjectHint = "来源对象：PL-202（中心车道）",
            };

            var right3 = new V3BandNode(V3BandSide.Right, V3BandKind.Pavement, "机动车道3")
            {
                WidthM = 3.50,
                SlopePct = -2.0,
                GeometrySource = V3GeometrySourceKind.PickedFromCad,
                CadObjectHint = "预留：从图上拾取边界宽度",
            };

            var right4 = new V3BandNode(V3BandSide.Right, V3BandKind.Separator, "机非分隔带")
            {
                WidthM = 1.50,
                SlopePct = 0.0,
            };

            var right5 = new V3BandNode(V3BandSide.Right, V3BandKind.NonMotorized, "非机动车道")
            {
                WidthM = 3.00,
                SlopePct = -1.5,
                GeometrySource = V3GeometrySourceKind.Manual,
            };

            var right6 = new V3BandNode(V3BandSide.Right, V3BandKind.Sidewalk, "人行道")
            {
                WidthM = 4.00,
                SlopePct = 1.5,
                GeometrySource = V3GeometrySourceKind.Manual,
            };

            var right7 = new V3BandNode(V3BandSide.Right, V3BandKind.GreenStrip, "绿化带")
            {
                WidthM = 2.00,
                SlopePct = 0.0,
            };

            RightSide.Bands.Add(right1);
            RightSide.Bands.Add(right2);
            RightSide.Bands.Add(right3);
            RightSide.Bands.Add(right4);
            RightSide.Bands.Add(right5);
            RightSide.Bands.Add(right6);
            RightSide.Bands.Add(right7);

            SelectedNode = left1;
        }

        private static string SideLabel(V3BandSide side)
        {
            switch (side)
            {
                case V3BandSide.Left: return "左侧";
                case V3BandSide.Right: return "右侧";
                default: return "中央";
            }
        }
    }
}
