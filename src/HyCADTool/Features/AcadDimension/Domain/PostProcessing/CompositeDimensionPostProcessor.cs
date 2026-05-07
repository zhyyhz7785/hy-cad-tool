using System;
using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Domain.PostProcessing
{
    /// <summary>
    /// 后处理链——按声明顺序串联多个 IDimensionPostProcessor。
    /// 当前默认链路：NearbyParallelMerge（合并同尺寸近距）→ EqualExtensionLength（统一延伸线长度）。
    /// 顺序原因：先合并减少标注数量，再对剩余标注做视觉等长——避免对将被合并丢弃的标注做无效计算。
    /// </summary>
    public sealed class CompositeDimensionPostProcessor : IDimensionPostProcessor
    {
        private readonly IReadOnlyList<IDimensionPostProcessor> _stages;

        public CompositeDimensionPostProcessor(params IDimensionPostProcessor[] stages)
        {
            _stages = stages ?? Array.Empty<IDimensionPostProcessor>();
        }

        public IReadOnlyList<DerivedDimension> Process(
            IReadOnlyList<DerivedDimension> input, NewDdsConfig config)
        {
            var current = input;
            for (int i = 0; i < _stages.Count; i++)
            {
                if (_stages[i] == null) continue;
                current = _stages[i].Process(current, config) ?? current;
            }
            return current ?? new List<DerivedDimension>();
        }
    }
}
