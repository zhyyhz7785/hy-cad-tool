using HYFEA.Core.Elements;
using HYFEA.Core.Solvers;

namespace HYFEA.Core.Analysis;

/// <summary>
/// 一次求解共用的注册表（单元贡献 + 线性求解器）。
/// Assembler / DofLayout / 分析器从这里取实例，不再各自 <c>new</c>。
/// </summary>
public sealed class FemContext
{
    public FemContext(
        ElementContributionRegistry? elements = null,
        SolverRegistry? solvers = null)
    {
        Elements = elements ?? ElementContributionRegistry.CreateDefault();
        Solvers = solvers ?? new SolverRegistry();
    }

    public ElementContributionRegistry Elements { get; }

    public SolverRegistry Solvers { get; }

    public static FemContext CreateDefault() => new();
}
