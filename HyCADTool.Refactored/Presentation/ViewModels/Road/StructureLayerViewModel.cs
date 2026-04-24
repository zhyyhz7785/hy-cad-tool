using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 结构层定义窗口（hyRoadStructureLayer，M7.3）的 ViewModel。
    ///
    /// <para><b>对应图 7</b></para>
    /// 左列 TreeView（方案列表，每个方案可展开显示结构层序列），
    /// 右列属性：基本信息（名称 / 描述） + 层参数（类型 / 厚度 / 加宽 / 坡度 / 填料 / 填充符号）。
    /// </summary>
    public sealed class StructureLayerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<StructureLayerSchemeNode> Schemes { get; } = new ObservableCollection<StructureLayerSchemeNode>();

        public static IReadOnlyList<StructureLayerKind> AvailableKinds { get; } = new[]
        {
            StructureLayerKind.Surface,
            StructureLayerKind.Base,
            StructureLayerKind.Subbase,
            StructureLayerKind.Custom,
        };

        public string HeaderTitle => "结构层定义";

        // =========================== 选中节点 ===========================

        private object _selectedNode;
        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value)) return;
                _selectedNode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedScheme));
                OnPropertyChanged(nameof(SelectedLayer));
                OnPropertyChanged(nameof(HasSelectedLayer));
                OnPropertyChanged(nameof(HasSelectedScheme));
            }
        }

        public StructureLayerSchemeNode SelectedScheme
        {
            get
            {
                if (_selectedNode is StructureLayerSchemeNode s) return s;
                if (_selectedNode is StructureLayerNode l) return Schemes.FirstOrDefault(sc => sc.Layers.Contains(l));
                return null;
            }
        }

        public StructureLayerNode SelectedLayer => _selectedNode as StructureLayerNode;

        public bool HasSelectedScheme => SelectedScheme != null;
        public bool HasSelectedLayer => SelectedLayer != null;

        // =========================== 命令 ===========================

        public ICommand AddSchemeCommand { get; }
        public ICommand DeleteSchemeCommand { get; }
        public ICommand AddLayerCommand { get; }
        public ICommand DeleteLayerCommand { get; }
        public ICommand MoveLayerUpCommand { get; }
        public ICommand MoveLayerDownCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler<List<StructureLayerScheme>> Confirmed;
        public event EventHandler Cancelled;
        public event EventHandler<bool?> CloseRequested;

        public StructureLayerViewModel(IEnumerable<StructureLayerScheme> initial = null)
        {
            if (initial != null)
            {
                foreach (var s in initial) Schemes.Add(StructureLayerSchemeNode.From(s));
            }

            AddSchemeCommand = new RelayCommand(ExecuteAddScheme);
            DeleteSchemeCommand = new RelayCommand(ExecuteDeleteScheme, () => SelectedScheme != null && !SelectedScheme.IsBuiltIn);
            AddLayerCommand = new RelayCommand(ExecuteAddLayer, () => SelectedScheme != null);
            DeleteLayerCommand = new RelayCommand(ExecuteDeleteLayer, () => SelectedLayer != null);
            MoveLayerUpCommand = new RelayCommand(ExecuteMoveUp, () => SelectedLayer != null);
            MoveLayerDownCommand = new RelayCommand(ExecuteMoveDown, () => SelectedLayer != null);

            ConfirmCommand = new RelayCommand(() =>
            {
                Confirmed?.Invoke(this, Schemes.Select(n => n.ToModel()).ToList());
                CloseRequested?.Invoke(this, true);
            });
            CancelCommand = new RelayCommand(() =>
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, false);
            });
        }

        // =========================== Command Handlers ===========================

        private void ExecuteAddScheme()
        {
            var s = new StructureLayerSchemeNode
            {
                Name = $"新方案{Schemes.Count + 1}",
                Description = string.Empty,
                IsBuiltIn = false,
            };
            Schemes.Add(s);
            SelectedNode = s;
        }

        private void ExecuteDeleteScheme()
        {
            var s = SelectedScheme;
            if (s == null || s.IsBuiltIn) return;
            Schemes.Remove(s);
            SelectedNode = null;
        }

        private void ExecuteAddLayer()
        {
            var s = SelectedScheme;
            if (s == null) return;
            var l = new StructureLayerNode
            {
                Name = $"新层{s.Layers.Count + 1}",
                LayerKind = StructureLayerKind.Custom,
                ThicknessCm = 10,
            };
            s.Layers.Add(l);
            SelectedNode = l;
        }

        private void ExecuteDeleteLayer()
        {
            var l = SelectedLayer;
            if (l == null) return;
            var s = Schemes.FirstOrDefault(sc => sc.Layers.Contains(l));
            if (s == null) return;
            s.Layers.Remove(l);
            SelectedNode = s;
        }

        private void ExecuteMoveUp()
        {
            var l = SelectedLayer;
            if (l == null) return;
            var s = Schemes.FirstOrDefault(sc => sc.Layers.Contains(l));
            if (s == null) return;
            int i = s.Layers.IndexOf(l);
            if (i <= 0) return;
            s.Layers.Move(i, i - 1);
        }

        private void ExecuteMoveDown()
        {
            var l = SelectedLayer;
            if (l == null) return;
            var s = Schemes.FirstOrDefault(sc => sc.Layers.Contains(l));
            if (s == null) return;
            int i = s.Layers.IndexOf(l);
            if (i < 0 || i >= s.Layers.Count - 1) return;
            s.Layers.Move(i, i + 1);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>方案节点（TreeView 一级）。</summary>
    public sealed class StructureLayerSchemeNode : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set { _name = value ?? string.Empty; OnPropertyChanged(); } }

        private string _description = string.Empty;
        public string Description { get => _description; set { _description = value ?? string.Empty; OnPropertyChanged(); } }

        public bool IsBuiltIn { get; set; }

        public ObservableCollection<StructureLayerNode> Layers { get; } = new ObservableCollection<StructureLayerNode>();

        public static StructureLayerSchemeNode From(StructureLayerScheme m)
        {
            var n = new StructureLayerSchemeNode
            {
                Id = m.Id,
                Name = m.Name ?? string.Empty,
                Description = m.Description ?? string.Empty,
                IsBuiltIn = m.IsBuiltIn,
            };
            foreach (var l in m.Layers ?? new List<StructureLayer>())
                n.Layers.Add(StructureLayerNode.From(l));
            return n;
        }

        public StructureLayerScheme ToModel()
        {
            var m = new StructureLayerScheme
            {
                Id = Id,
                Name = Name,
                Description = Description,
                IsBuiltIn = IsBuiltIn,
            };
            foreach (var n in Layers) m.Layers.Add(n.ToModel());
            return m;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public override string ToString() => Name;
    }

    /// <summary>结构层节点（TreeView 二级）。</summary>
    public sealed class StructureLayerNode : INotifyPropertyChanged
    {
        private readonly LayerFillSettings _planFill;
        private readonly LayerFillSettings _sectionFill;

        public StructureLayerNode()
            : this(LayerFillSettings.Empty(), LayerFillSettings.Empty())
        {
        }

        private StructureLayerNode(LayerFillSettings planFill, LayerFillSettings sectionFill)
        {
            _planFill = planFill ?? LayerFillSettings.Empty();
            _sectionFill = sectionFill ?? LayerFillSettings.Empty();
            PlanFill = new LayerFillViewModel(_planFill);
            SectionFill = new LayerFillViewModel(_sectionFill);
            AddFillMaterialPresetCommand = new RelayCommand(AddCurrentFillMaterialPreset);
            RemoveFillMaterialPresetCommand = new RelayCommand(RemoveCurrentFillMaterialPreset);
        }

        /// <summary>道路平面图（俯视）填充。</summary>
        public LayerFillViewModel PlanFill { get; }

        /// <summary>横断面坡面图（侧视）填充。</summary>
        public LayerFillViewModel SectionFill { get; }

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>将当前填料加入本层类别的候选列表，并立即写回 hy-settings。</summary>
        public ICommand AddFillMaterialPresetCommand { get; }

        /// <summary>将当前填料从本层类别的候选列表移除，并立即写回 hy-settings。</summary>
        public ICommand RemoveFillMaterialPresetCommand { get; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                var v = value ?? string.Empty;
                if (_name == v) return;
                _name = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LayerHeaderDisplay));
            }
        }

        /// <summary>Expander 标题：与填料同步，未填时退回名称。</summary>
        public string LayerHeaderDisplay
        {
            get
            {
                var m = _fillMaterial?.Trim();
                if (!string.IsNullOrEmpty(m)) return m;
                return string.IsNullOrEmpty(_name) ? "（未命名）" : _name;
            }
        }

        private string _description = string.Empty;
        public string Description { get => _description; set { _description = value ?? string.Empty; OnPropertyChanged(); } }

        private StructureLayerKind _layerKind = StructureLayerKind.Custom;
        public StructureLayerKind LayerKind
        {
            get => _layerKind;
            set
            {
                _layerKind = value;
                OnPropertyChanged();
            }
        }

        private double _thicknessCm;
        public double ThicknessCm { get => _thicknessCm; set { _thicknessCm = value; OnPropertyChanged(); } }

        private double _leftWidenCm;
        public double LeftWidenCm { get => _leftWidenCm; set { _leftWidenCm = value; OnPropertyChanged(); } }

        private double _rightWidenCm;
        public double RightWidenCm { get => _rightWidenCm; set { _rightWidenCm = value; OnPropertyChanged(); } }

        private double _leftSlope;
        public double LeftSlope { get => _leftSlope; set { _leftSlope = value; OnPropertyChanged(); } }

        private double _rightSlope;
        public double RightSlope { get => _rightSlope; set { _rightSlope = value; OnPropertyChanged(); } }

        private string _fillMaterial = string.Empty;
        public string FillMaterial
        {
            get => _fillMaterial;
            set
            {
                var v = value ?? string.Empty;
                if (_fillMaterial == v) return;
                _fillMaterial = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LayerHeaderDisplay));
                TryApplySuggestedSectionHatch();
            }
        }

        /// <summary>与 <see cref="StructureLayer.PatternName"/> 同步：读写到 <see cref="SectionFill"/> 的坡面图图案名。</summary>
        public string PatternName
        {
            get => SectionFill.PatternName;
            set
            {
                var v = value ?? string.Empty;
                if (SectionFill.PatternName == v) return;
                SectionFill.PatternName = v;
                if (!string.IsNullOrWhiteSpace(v))
                    SectionFill.PatternEnabled = true;
            }
        }

        private void TryApplySuggestedSectionHatch()
        {
            if (!string.IsNullOrWhiteSpace(SectionFill.PatternName)) return;
            var s = RoadMaterialFillPresets.SuggestHatchFor(LayerKind, FillMaterial);
            SectionFill.PatternName = s;
            SectionFill.PatternEnabled = true;
        }

        public static StructureLayerNode From(StructureLayer m)
        {
            if (m == null) return new StructureLayerNode();
            var plan = (m.PlanFill ?? LayerFillSettings.Empty()).Clone();
            var sec = (m.SectionFill ?? LayerFillSettings.Empty()).Clone();
            LayerFillSettings.MigrateFromLegacyPatternName(sec, m.PatternName);
            var n = new StructureLayerNode(plan, sec)
            {
                Id = m.Id,
                Name = m.Name ?? string.Empty,
                Description = m.Description ?? string.Empty,
                LayerKind = m.LayerKind,
                ThicknessCm = m.ThicknessCm,
                LeftWidenCm = m.LeftWidenCm,
                RightWidenCm = m.RightWidenCm,
                LeftSlope = m.LeftSlope,
                RightSlope = m.RightSlope,
                FillMaterial = m.FillMaterial ?? string.Empty,
            };
            if (string.IsNullOrWhiteSpace(n.SectionFill.PatternName) && !string.IsNullOrWhiteSpace(n.FillMaterial))
                n.TryApplySuggestedSectionHatch();
            return n;
        }

        public StructureLayer ToModel()
        {
            return new StructureLayer
            {
                Id = Id,
                Name = Name,
                Description = Description,
                LayerKind = LayerKind,
                ThicknessCm = ThicknessCm,
                LeftWidenCm = LeftWidenCm,
                RightWidenCm = RightWidenCm,
                LeftSlope = LeftSlope,
                RightSlope = RightSlope,
                FillMaterial = FillMaterial,
                PatternName = SectionFill?.PatternName ?? string.Empty,
                PlanFill = _planFill?.Clone() ?? LayerFillSettings.Empty(),
                SectionFill = _sectionFill?.Clone() ?? LayerFillSettings.Empty(),
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private void AddCurrentFillMaterialPreset()
        {
            if (!RoadMaterialFillPresets.TryAdd(LayerKind, FillMaterial)) return;
            PersistFillMaterialPresetChanges();
        }

        private void RemoveCurrentFillMaterialPreset()
        {
            if (!RoadMaterialFillPresets.TryRemove(LayerKind, FillMaterial)) return;
            PersistFillMaterialPresetChanges();
        }

        private static void PersistFillMaterialPresetChanges()
        {
            var settings = SettingsPanelViewModel.Current;
            if (settings == null) return;
            settings.SetRoadMaterialFillSettings(RoadMaterialFillPresets.ExportUserSettings(), saveImmediately: true);
        }

        public override string ToString() => $"{Name}({LayerKind}) h={ThicknessCm}cm";
    }
}
