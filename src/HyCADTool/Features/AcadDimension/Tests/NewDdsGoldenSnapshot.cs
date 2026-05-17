using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Tests
{
    /// <summary>
    /// Phase 7 金标快照：把一次纯 Domain 运行的可观测量压扁成可序列化结构，便于打印/对比。
    /// </summary>
    public sealed class NewDdsGoldenSnapshot
    {
        public string Fixture { get; set; }
        public int VertexCount { get; set; }
        public int HorizontalColumns { get; set; }
        public int HorizontalEdges { get; set; }
        public int VerticalColumns { get; set; }
        public int VerticalEdges { get; set; }
        public int RawDimensions { get; set; }
        public int CleanDimensions { get; set; }
        public IDictionary<DimensionSource, int> SourceDistribution { get; set; }
            = new SortedDictionary<DimensionSource, int>();
        public double Coverage { get; set; }
        public double NoOverlap { get; set; }
        public double WithinBounds { get; set; }
        public double Consistency { get; set; }
        public double DirectionalCoverage { get; set; }
        public double Overall { get; set; }

        public string ToShortLine()
        {
            string srcDist = string.Join(",",
                SourceDistribution.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}={kv.Value}"));
            return
                $"{Fixture}: V={VertexCount} H={HorizontalColumns}/{HorizontalEdges} V={VerticalColumns}/{VerticalEdges} " +
                $"raw={RawDimensions} clean={CleanDimensions} | {srcDist} | " +
                $"Q[Cov={Coverage:F2} NoOv={NoOverlap:F2} InB={WithinBounds:F2} Con={Consistency:F2} Dir={DirectionalCoverage:F2}] " +
                $"Overall={Overall:F2}";
        }
    }

    /// <summary>对比两个 Snapshot 的最小工具——按字段比对，差异列表写入 issues。</summary>
    public static class NewDdsGoldenComparer
    {
        public static IList<string> Compare(NewDdsGoldenSnapshot expected, NewDdsGoldenSnapshot actual)
        {
            var diffs = new List<string>();
            if (expected == null)
            {
                diffs.Add("BASELINE NOT SET（首次跑：把 actual 作为基线粘进 NewDdsGoldenBaseline）");
                return diffs;
            }
            void Cmp(string name, object a, object b)
            {
                if (!Equals(a, b)) diffs.Add($"{name} 差异：expected={a} actual={b}");
            }
            Cmp(nameof(expected.VertexCount), expected.VertexCount, actual.VertexCount);
            Cmp(nameof(expected.HorizontalColumns), expected.HorizontalColumns, actual.HorizontalColumns);
            Cmp(nameof(expected.HorizontalEdges), expected.HorizontalEdges, actual.HorizontalEdges);
            Cmp(nameof(expected.VerticalColumns), expected.VerticalColumns, actual.VerticalColumns);
            Cmp(nameof(expected.VerticalEdges), expected.VerticalEdges, actual.VerticalEdges);
            Cmp(nameof(expected.RawDimensions), expected.RawDimensions, actual.RawDimensions);
            Cmp(nameof(expected.CleanDimensions), expected.CleanDimensions, actual.CleanDimensions);
            foreach (var s in (DimensionSource[])System.Enum.GetValues(typeof(DimensionSource)))
            {
                int e = expected.SourceDistribution.TryGetValue(s, out int ev) ? ev : 0;
                int a = actual.SourceDistribution.TryGetValue(s, out int av) ? av : 0;
                if (e != a) diffs.Add($"Source[{s}] 差异：expected={e} actual={a}");
            }
            void CmpQ(string name, double e, double a)
            {
                if (System.Math.Abs(e - a) > 0.005)
                    diffs.Add($"Q.{name} 差异：expected={e:F2} actual={a:F2}");
            }
            CmpQ(nameof(expected.Coverage), expected.Coverage, actual.Coverage);
            CmpQ(nameof(expected.NoOverlap), expected.NoOverlap, actual.NoOverlap);
            CmpQ(nameof(expected.WithinBounds), expected.WithinBounds, actual.WithinBounds);
            CmpQ(nameof(expected.Consistency), expected.Consistency, actual.Consistency);
            CmpQ(nameof(expected.DirectionalCoverage), expected.DirectionalCoverage, actual.DirectionalCoverage);
            CmpQ(nameof(expected.Overall), expected.Overall, actual.Overall);
            return diffs;
        }
    }
}
