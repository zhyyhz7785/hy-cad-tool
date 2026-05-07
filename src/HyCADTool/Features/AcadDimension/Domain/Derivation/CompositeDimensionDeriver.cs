using System;
using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Domain.Derivation
{
    /// <summary>
    /// 组合多个 IDimensionDeriver——按顺序调用并合并结果。
    /// 当前用法：Outside（4 方向 + 总尺寸）→ Inside（凸起局部尺寸）。
    /// 设计要点：单接口下保持 Service 链路单调，又能把 Deriver 拆成可独立测试的小单元。
    /// </summary>
    public sealed class CompositeDimensionDeriver : IDimensionDeriver
    {
        private readonly IReadOnlyList<IDimensionDeriver> _derivers;

        public CompositeDimensionDeriver(params IDimensionDeriver[] derivers)
        {
            _derivers = derivers ?? Array.Empty<IDimensionDeriver>();
        }

        public IReadOnlyList<DerivedDimension> Derive(BoundaryFeatures features, NewDdsConfig config)
        {
            var all = new List<DerivedDimension>();
            for (int i = 0; i < _derivers.Count; i++)
            {
                var sub = _derivers[i]?.Derive(features, config);
                if (sub == null) continue;
                for (int k = 0; k < sub.Count; k++) all.Add(sub[k]);
            }
            return all;
        }
    }
}
