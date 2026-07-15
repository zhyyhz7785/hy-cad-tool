using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HYFEA.Hosting;

/// <summary>宿主消费一次求解结果（表、CAD 回写等）。</summary>
public interface IFemResultSink
{
    void Present(FemResult result, FemProblem problem);
}
