namespace HYFEA.Core.Solvers;

/// <summary>
/// 线性求解器注册表 (P2.v0.2)。
/// 支持 Track A / Track B 求解器的注册、切换与默认选择。
/// </summary>
public sealed class SolverRegistry
{
    private readonly Dictionary<string, ILinearSystemSolver> _solvers = new();
    private ILinearSystemSolver? _default;

    /// <summary>
    /// 创建注册表并注册默认求解器。
    /// </summary>
    public SolverRegistry()
    {
        // Track A: CSparse.NET 稀疏直接求解器(默认)
        var csparseSolver = new CSparseSystemSolver();
        Register("csparse", csparseSolver);

        // Track B: 稠密 Gauss 消元(fallback)
        var denseSolver = new DenseSystemSolver();
        Register("dense_gauss", denseSolver);

        // 默认使用 CSparse(Track A)
        _default = csparseSolver;
    }

    /// <summary>
    /// 注册求解器。
    /// </summary>
    public void Register(string key, ILinearSystemSolver solver)
    {
        _solvers[key] = solver;
    }

    /// <summary>
    /// 获取指定求解器。
    /// </summary>
    public ILinearSystemSolver Get(string key)
    {
        if (!_solvers.TryGetValue(key, out var solver))
            throw new ArgumentException($"Solver '{key}' not registered. Available: {string.Join(", ", _solvers.Keys)}");
        return solver;
    }

    /// <summary>
    /// 获取默认求解器。
    /// </summary>
    public ILinearSystemSolver Default => _default ?? throw new InvalidOperationException("No default solver set.");

    /// <summary>
    /// 设置默认求解器。
    /// </summary>
    public void SetDefault(string key)
    {
        _default = Get(key);
    }

    /// <summary>
    /// 获取所有已注册的求解器键。
    /// </summary>
    public IReadOnlyCollection<string> AvailableSolvers => _solvers.Keys;
}
