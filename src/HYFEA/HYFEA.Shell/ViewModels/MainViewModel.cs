using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Core.Results;
using HYFEA.Hosting;
using HYFEA.Viz;

namespace HYFEA.Shell.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IFemResultSink
{
    private readonly BeamSolveSession _session = new();
    private readonly RelayCommand _exportVtuCommand;
    private FemProblem? _lastProblem;
    private FemResult? _lastResult;
    private string? _lastMeshJson;
    private double _youngsModulus = 2.06e5;
    private double _area = 8e4;
    private double _inertiaZ = 1.067e9;
    private double _q = 10;
    private double _length = 10000;
    private int _segments = 4;
    private bool _loadIsGlobalNegY = true;
    private bool _useSiUnits;
    private BeamSupportKind _endA = BeamSupportKind.Fixed;
    private BeamSupportKind _endB = BeamSupportKind.Free;
    private string _status = "就绪 — 改参数后点「求解」";

    public MainViewModel()
    {
        SolveCommand = new RelayCommand(ExecuteSolve);
        _exportVtuCommand = new RelayCommand(ExecuteExportVtu, () => _lastProblem != null && _lastResult is { Success: true });
        ExportVtuCommand = _exportVtuCommand;
    }

    public IReadOnlyList<BeamSupportKind> SupportOptions { get; } =
    [
        BeamSupportKind.Free,
        BeamSupportKind.Pin,
        BeamSupportKind.Fixed,
    ];

    public double YoungsModulus
    {
        get => _youngsModulus;
        set { if (Set(ref _youngsModulus, value)) { } }
    }

    public double Area
    {
        get => _area;
        set => Set(ref _area, value);
    }

    public double InertiaZ
    {
        get => _inertiaZ;
        set => Set(ref _inertiaZ, value);
    }

    public double Q
    {
        get => _q;
        set => Set(ref _q, value);
    }

    public double Length
    {
        get => _length;
        set => Set(ref _length, value);
    }

    public int Segments
    {
        get => _segments;
        set => Set(ref _segments, value);
    }

    public bool LoadIsGlobalNegY
    {
        get => _loadIsGlobalNegY;
        set => Set(ref _loadIsGlobalNegY, value);
    }

    public bool UseSiUnits
    {
        get => _useSiUnits;
        set => Set(ref _useSiUnits, value);
    }

    public BeamSupportKind EndA
    {
        get => _endA;
        set => Set(ref _endA, value);
    }

    public BeamSupportKind EndB
    {
        get => _endB;
        set => Set(ref _endB, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public void ReportStatus(string message) => Status = message;

    /// <summary>最近一次成功求解的 ResultMesh JSON（vtk.js 契约）。</summary>
    public string? LastMeshJson
    {
        get => _lastMeshJson;
        private set
        {
            if (Set(ref _lastMeshJson, value))
                MeshJsonChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? MeshJsonChanged;

    public ObservableCollection<NodalResultRow> NodalResults { get; } = new();

    public ObservableCollection<BeamForceRow> BeamForces { get; } = new();

    public ICommand SolveCommand { get; }

    public ICommand ExportVtuCommand { get; }

    private void ExecuteSolve()
    {
        try
        {
            int seg = Segments < 1 ? 4 : Math.Min(Segments, 10000);
            if (seg != Segments) Segments = seg;

            double len = Length > 0 ? Length : 10000;
            if (Math.Abs(len - Length) > 1e-15) Length = len;

            var geom = InMemoryAxisGeometry.Horizontal(len, seg);
            var request = new BeamSolveRequest
            {
                YoungsModulus = CoercePositive(YoungsModulus, 2.06e5),
                Area = CoercePositive(Area, 8e4),
                InertiaZ = CoercePositive(InertiaZ, 1.067e9),
                Q = CoercePositive(Q, 10),
                LoadDirection = LoadIsGlobalNegY ? BeamLoadDirection.GlobalNegY : BeamLoadDirection.LocalPerpendicular,
                EndA = EndA,
                EndB = EndB,
                Units = UseSiUnits ? UnitSystem.SI : UnitSystem.MmN,
            };

            var sw = Stopwatch.StartNew();
            var result = _session.Run(geom, request, this);
            sw.Stop();

            if (!result.Success)
            {
                _lastProblem = null;
                _lastResult = null;
                LastMeshJson = null;
                RaiseExportCommandsChanged();
                Status = "求解失败：" + (result.Error?.Message ?? "未知");
                return;
            }

            Status = $"求解成功 — {sw.ElapsedMilliseconds} ms — 节点 {NodalResults.Count}，单元 {BeamForces.Count}";
        }
        catch (Exception ex)
        {
            _lastProblem = null;
            _lastResult = null;
            LastMeshJson = null;
            RaiseExportCommandsChanged();
            Status = "异常：" + ex.Message;
        }
    }

    private void ExecuteExportVtu()
    {
        try
        {
            var path = WriteLastResultVtu();
            if (path == null) return;
            Status = "已导出 VTU：" + path;
        }
        catch (Exception ex)
        {
            Status = "导出失败：" + ex.Message;
        }
    }

    private string? WriteLastResultVtu()
    {
        if (_lastProblem == null || _lastResult is not { Success: true })
        {
            Status = "请先求解成功再导出";
            return null;
        }

        var mesh = BeamResultMeshBuilder.From(_lastProblem, _lastResult);
        var path = Path.Combine(Path.GetTempPath(), "hyfea-last.vtu");
        VtuWriter.Write(mesh, path);
        return path;
    }

    public void Present(FemResult result, FemProblem problem)
    {
        NodalResults.Clear();
        BeamForces.Clear();

        _lastProblem = problem;
        _lastResult = result;
        RaiseExportCommandsChanged();

        if (!result.Success || result.Displacements == null || result.Layout == null)
        {
            LastMeshJson = null;
            return;
        }

        try
        {
            var mesh = BeamResultMeshBuilder.From(problem, result);
            LastMeshJson = ResultMeshJson.ToJson(mesh);
            // 边车 / ParaView 旁路共用固定路径
            VtuWriter.Write(mesh, Path.Combine(Path.GetTempPath(), "hyfea-last.vtu"));
        }
        catch (Exception ex)
        {
            LastMeshJson = null;
            Status = "结果网格构建失败：" + ex.Message;
        }

        foreach (var node in problem.Nodes)
        {
            result.Displacements.TryGet(node.Id, DofType.UX, out var ux);
            result.Displacements.TryGet(node.Id, DofType.UY, out var uy);
            result.Displacements.TryGet(node.Id, DofType.RZ, out var rz);
            NodalResults.Add(new NodalResultRow
            {
                Node = node.Id.Value,
                UX = ux,
                UY = uy,
                RZ = rz,
            });
        }

        if (result.BeamEndForces != null)
        {
            foreach (var kv in result.BeamEndForces.AsReadOnly())
            {
                var v = kv.Value;
                BeamForces.Add(new BeamForceRow
                {
                    Element = kv.Key.Value,
                    N = v.N,
                    VA = v.VA,
                    MA = v.MA,
                    VB = v.VB,
                    MB = v.MB,
                });
            }
        }
    }

    private void RaiseExportCommandsChanged() =>
        _exportVtuCommand.RaiseCanExecuteChanged();

    private static double CoercePositive(double v, double fallback) =>
        double.IsNaN(v) || double.IsInfinity(v) || v <= 0 ? fallback : v;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
