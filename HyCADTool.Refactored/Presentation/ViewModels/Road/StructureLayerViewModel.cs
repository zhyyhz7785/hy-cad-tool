using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;

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
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set { _name = value ?? string.Empty; OnPropertyChanged(); } }

        private string _description = string.Empty;
        public string Description { get => _description; set { _description = value ?? string.Empty; OnPropertyChanged(); } }

        private StructureLayerKind _layerKind = StructureLayerKind.Custom;
        public StructureLayerKind LayerKind { get => _layerKind; set { _layerKind = value; OnPropertyChanged(); } }

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
        public string FillMaterial { get => _fillMaterial; set { _fillMaterial = value ?? string.Empty; OnPropertyChanged(); } }

        private string _patternName = string.Empty;
        public string PatternName { get => _patternName; set { _patternName = value ?? string.Empty; OnPropertyChanged(); } }

        public static StructureLayerNode From(StructureLayer m)
        {
            return new StructureLayerNode
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
                PatternName = m.PatternName ?? string.Empty,
            };
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
                PatternName = PatternName,
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public override string ToString() => $"{Name}({LayerKind}) h={ThicknessCm}cm";
    }
}
