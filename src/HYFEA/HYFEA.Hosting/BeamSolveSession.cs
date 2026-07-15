using System;
using HYFEA.Core.Analysis;
using HYFEA.Core.Results;

namespace HYFEA.Hosting;

/// <summary>几何 → 组装 → 线性静力求解 → 可选结果呈现。</summary>
public sealed class BeamSolveSession
{
    private readonly LinearStaticAnalysis _analysis = new();

    public FemResult Run(IFemHostGeometry geometry, BeamSolveRequest request, IFemResultSink? sink = null)
    {
        if (geometry == null) throw new ArgumentNullException(nameof(geometry));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var axis = geometry.GetAxisNodes();
        var problem = BeamProblemFactory.Build(axis, request);
        var result = _analysis.Run(problem);
        sink?.Present(result, problem);
        return result;
    }
}
