using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Tests
{
    /// <summary>
    /// Phase 7 基线（首冻于 2026-05-08）。
    /// 数值来自当时 ndds 链路（Phase 6 完整 + Phase 5 绑定 + Phase 4 后处理）下的纯 Domain 输出；
    /// 后续重构改 Domain 行为，<c>nddsT</c>/<c>N5</c> 应立即列出 DIFF。
    /// 调整 fixture 顶点 / 配置 / 算法预期后，需要同步更新这里。
    /// </summary>
    public static class NewDdsGoldenBaseline
    {
        public static IReadOnlyDictionary<string, NewDdsGoldenSnapshot> Map { get; }
            = new Dictionary<string, NewDdsGoldenSnapshot>
            {
                ["01-rect"] = new NewDdsGoldenSnapshot
                {
                    Fixture = "01-rect",
                    VertexCount = 4,
                    HorizontalColumns = 1, HorizontalEdges = 2,
                    VerticalColumns = 1, VerticalEdges = 2,
                    RawDimensions = 8, CleanDimensions = 8,
                    SourceDistribution = new SortedDictionary<DimensionSource, int>
                    {
                        { DimensionSource.OutsideLeft, 1 },
                        { DimensionSource.OutsideRight, 1 },
                        { DimensionSource.OutsideUp, 1 },
                        { DimensionSource.OutsideDown, 1 },
                        { DimensionSource.OutsideTotalLeft, 1 },
                        { DimensionSource.OutsideTotalRight, 1 },
                        { DimensionSource.OutsideTotalUp, 1 },
                        { DimensionSource.OutsideTotalDown, 1 }
                    },
                    Coverage = 1.00, NoOverlap = 1.00, WithinBounds = 1.00,
                    Consistency = 1.00, DirectionalCoverage = 1.00, Overall = 1.00
                },

                ["02-l-shape"] = new NewDdsGoldenSnapshot
                {
                    Fixture = "02-l-shape",
                    VertexCount = 6,
                    HorizontalColumns = 2, HorizontalEdges = 4,
                    VerticalColumns = 2, VerticalEdges = 4,
                    RawDimensions = 10, CleanDimensions = 10,
                    SourceDistribution = new SortedDictionary<DimensionSource, int>
                    {
                        { DimensionSource.OutsideLeft, 1 },
                        { DimensionSource.OutsideRight, 2 },
                        { DimensionSource.OutsideUp, 2 },
                        { DimensionSource.OutsideDown, 1 },
                        { DimensionSource.OutsideTotalLeft, 1 },
                        { DimensionSource.OutsideTotalRight, 1 },
                        { DimensionSource.OutsideTotalUp, 1 },
                        { DimensionSource.OutsideTotalDown, 1 }
                    },
                    Coverage = 0.83, NoOverlap = 1.00, WithinBounds = 1.00,
                    Consistency = 1.00, DirectionalCoverage = 1.00, Overall = 0.97
                },

                ["03-t-shape"] = new NewDdsGoldenSnapshot
                {
                    Fixture = "03-t-shape",
                    VertexCount = 8,
                    HorizontalColumns = 2, HorizontalEdges = 4,
                    VerticalColumns = 3, VerticalEdges = 6,
                    RawDimensions = 12, CleanDimensions = 12,
                    SourceDistribution = new SortedDictionary<DimensionSource, int>
                    {
                        { DimensionSource.OutsideLeft, 2 },
                        { DimensionSource.OutsideRight, 2 },
                        { DimensionSource.OutsideUp, 3 },
                        { DimensionSource.OutsideDown, 1 },
                        { DimensionSource.OutsideTotalLeft, 1 },
                        { DimensionSource.OutsideTotalRight, 1 },
                        { DimensionSource.OutsideTotalUp, 1 },
                        { DimensionSource.OutsideTotalDown, 1 }
                    },
                    Coverage = 0.75, NoOverlap = 1.00, WithinBounds = 1.00,
                    Consistency = 1.00, DirectionalCoverage = 1.00, Overall = 0.95
                },

                ["04-double-notch"] = new NewDdsGoldenSnapshot
                {
                    Fixture = "04-double-notch",
                    VertexCount = 13,
                    HorizontalColumns = 2, HorizontalEdges = 8,
                    VerticalColumns = 5, VerticalEdges = 18,
                    RawDimensions = 18, CleanDimensions = 16,
                    SourceDistribution = new SortedDictionary<DimensionSource, int>
                    {
                        { DimensionSource.OutsideLeft, 2 },
                        { DimensionSource.OutsideRight, 1 },
                        { DimensionSource.OutsideUp, 1 },
                        { DimensionSource.OutsideDown, 1 },
                        { DimensionSource.OutsideTotalLeft, 1 },
                        { DimensionSource.OutsideTotalRight, 1 },
                        { DimensionSource.OutsideTotalUp, 1 },
                        { DimensionSource.OutsideTotalDown, 1 },
                        { DimensionSource.InsideLeftRight, 2 },
                        { DimensionSource.InsideUpDown, 5 }
                    },
                    Coverage = 1.00, NoOverlap = 0.88, WithinBounds = 1.00,
                    Consistency = 1.00, DirectionalCoverage = 1.00, Overall = 0.97
                },

                ["05-asymmetric"] = new NewDdsGoldenSnapshot
                {
                    Fixture = "05-asymmetric",
                    VertexCount = 8,
                    HorizontalColumns = 3, HorizontalEdges = 6,
                    VerticalColumns = 3, VerticalEdges = 6,
                    RawDimensions = 12, CleanDimensions = 12,
                    SourceDistribution = new SortedDictionary<DimensionSource, int>
                    {
                        { DimensionSource.OutsideLeft, 1 },
                        { DimensionSource.OutsideRight, 3 },
                        { DimensionSource.OutsideUp, 3 },
                        { DimensionSource.OutsideDown, 1 },
                        { DimensionSource.OutsideTotalLeft, 1 },
                        { DimensionSource.OutsideTotalRight, 1 },
                        { DimensionSource.OutsideTotalUp, 1 },
                        { DimensionSource.OutsideTotalDown, 1 }
                    },
                    Coverage = 0.62, NoOverlap = 1.00, WithinBounds = 1.00,
                    Consistency = 1.00, DirectionalCoverage = 1.00, Overall = 0.93
                }
            };
    }
}
