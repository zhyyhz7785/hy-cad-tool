using System;
using HYFEA.Core.Analysis;
using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HYFEA.Hosting;

/// <summary>几何 → 组装 → 线性静力求解 → 可选结果呈现。</summary>
public sealed class BeamSolveSession
{
    private readonly SparseLinearStaticAnalysis _analysis = new();

    public FemResult Run(IFemHostGeometry geometry, BeamSolveRequest request, IFemResultSink? sink = null)
    {
        var solved = Compute(geometry, request);
        sink?.Present(solved.Result, solved.Problem);
        return solved.Result;
    }

    /// <summary>求解，不回调 UI。壳层把这一步放到后台线程，再回 UI 线程 Present。</summary>
    public BeamSolveOutput Compute(IFemHostGeometry geometry, BeamSolveRequest request)
    {
        if (geometry == null) throw new ArgumentNullException(nameof(geometry));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var axis = geometry.GetAxisNodes();
        var problem = BeamProblemFactory.Build(axis, request);
        var result = _analysis.Run(problem);
        return new BeamSolveOutput(result, problem);
    }
}

/// <summary>一次梁求解的结果和对应问题（结果网格需要两者）。</summary>
public sealed class BeamSolveOutput
{
    public BeamSolveOutput(FemResult result, FemProblem problem)
    {
        Result = result;
        Problem = problem;
    }

    public FemResult Result { get; }

    public FemProblem Problem { get; }
}
