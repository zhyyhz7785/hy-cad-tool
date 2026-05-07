using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Domain.PostProcessing
{
    /// <summary>
    /// 近距平行标注合并（升级版）—— 对应 06 §7 #6 / 07 计划 Phase 4 修 #6。
    ///
    /// 触发场景：
    ///   1. 同尺寸近距（用户图1底部凸起左/右两条 1500 同 measure，dimLine 间距 ~1260mm）
    ///   2. 不同尺寸近距（罕见但存在：同 Source 内 measure 微差但 dimLine 几乎重叠）
    ///
    /// 算法（升级要点）：
    ///   1. 按 (Rotation, Source) 分组——同 Source 内才合并，**避免 OutsideLeft 与 OutsideTotalLeft 误合并**
    ///      （它们的 dimLine 距离 = DimensionDistanceWithDim，但语义完全不同：分段尺寸 vs 总尺寸）；
    ///   2. 同组内按 dimLine 在垂直方向上的坐标排序；
    ///   3. 遍历所有相邻对，dimLine 距离 &lt; mergeDistance 时按 **measure 大者优先** 决定丢弃哪条
    ///      （工程惯例：信息量更大的长尺寸优先；measure 相同时保留前者）。
    ///
    /// 与老 dds DeleteNearbyParallelDim 的差异：
    ///   - 老 dds：按 (Rotation, Measurement) 分组（measure 必须相同），删前者；
    ///   - NewDDS：按 (Rotation, Source) 分组（不要求 measure 相同），保留长者。
    ///   - 后者更稳健：能处理"同 Source 测量值微差但 dimLine 几乎重合"的退化场景。
    ///
    /// 配置：
    ///   - 合并阈值 = `config.DimDistanceTolerance`（来自 SettingsPanelViewModel.DimDistanceTolerance × Scale）；
    ///   - 用户调面板"距离容差"控合并强度。
    /// </summary>
    public sealed class NearbyParallelMergePostProcessor : IDimensionPostProcessor
    {
        private const double RotationBucket = 1e-3;
        private const double MeasureTolerance = 1e-3;

        public IReadOnlyList<DerivedDimension> Process(
            IReadOnlyList<DerivedDimension> input, NewDdsConfig config)
        {
            if (input == null || input.Count == 0)
                return input ?? (IReadOnlyList<DerivedDimension>)new List<DerivedDimension>();
            if (config.DimDistanceTolerance <= 0.0) return input;

            var output = new List<DerivedDimension>(input.Count);
            var grouped = input.GroupBy(d => new GroupKey(
                Math.Round(d.Rotation / RotationBucket) * RotationBucket,
                d.Source));

            foreach (var group in grouped)
            {
                var merged = MergeKeepLongest(group.ToList(), config.DimDistanceTolerance);
                output.AddRange(merged);
            }
            return output;
        }

        private static List<DerivedDimension> MergeKeepLongest(
            List<DerivedDimension> sameRotSource, double mergeDistance)
        {
            int n = sameRotSource.Count;
            if (n <= 1) return sameRotSource;

            bool isHorizontal = Math.Abs(sameRotSource[0].Rotation) < RotationBucket;
            var sorted = isHorizontal
                ? sameRotSource.OrderBy(d => d.DimensionLinePoint.Y).ToList()
                : sameRotSource.OrderBy(d => d.DimensionLinePoint.X).ToList();

            var keep = new bool[n];
            for (int i = 0; i < n; i++) keep[i] = true;

            for (int i = 0; i < n - 1; i++)
            {
                if (!keep[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!keep[j]) continue;
                    double iKey = isHorizontal ? sorted[i].DimensionLinePoint.Y : sorted[i].DimensionLinePoint.X;
                    double jKey = isHorizontal ? sorted[j].DimensionLinePoint.Y : sorted[j].DimensionLinePoint.X;
                    if (Math.Abs(iKey - jKey) >= mergeDistance) break;

                    double iMeasure = ComputeMeasure(sorted[i]);
                    double jMeasure = ComputeMeasure(sorted[j]);
                    if (iMeasure > jMeasure + MeasureTolerance)
                    {
                        keep[j] = false;
                    }
                    else if (jMeasure > iMeasure + MeasureTolerance)
                    {
                        keep[i] = false;
                        break;
                    }
                    else
                    {
                        keep[j] = false;
                    }
                }
            }

            var result = new List<DerivedDimension>(n);
            for (int i = 0; i < n; i++)
                if (keep[i]) result.Add(sorted[i]);
            return result;
        }

        private static double ComputeMeasure(DerivedDimension d)
        {
            bool isHorizontal = Math.Abs(d.Rotation) < RotationBucket;
            return isHorizontal
                ? Math.Abs(d.ExtensionLine1Point.X - d.ExtensionLine2Point.X)
                : Math.Abs(d.ExtensionLine1Point.Y - d.ExtensionLine2Point.Y);
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public readonly double Rotation;
            public readonly DimensionSource Source;
            public GroupKey(double rotation, DimensionSource source) { Rotation = rotation; Source = source; }
            public bool Equals(GroupKey other) => Rotation.Equals(other.Rotation) && Source == other.Source;
            public override bool Equals(object obj) => obj is GroupKey gk && Equals(gk);
            public override int GetHashCode() => unchecked(Rotation.GetHashCode() * 397 ^ (int)Source);
        }
    }
}
