using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.PostProcessing
{
    /// <summary>
    /// 近距/重叠平行标注合并（v3）—— 对应 06 §7 #6 / 07 计划 Phase 4 修 #6 的语义增强。
    ///
    /// 触发场景：
    ///   1. 用户图1：底部两个深度=1700 的凹槽，凹槽顶 Y / 底 Y 完全相同
    ///      → 两条 InsideLR 标注 ExtensionLine **测同一 Y 区段**，视觉重复 → 应合并
    ///   2. 同 measure 同 Source 但不同区段（如不同高度的两个凹槽都恰好 1700 深）
    ///      → 测的是不同特征 → 不合并
    ///   3. 同 Source 测同区段但 measure 微差（罕见，几何噪声）→ 合并保留长者
    ///
    /// 合并判据（按优先级）：
    ///   主判据：**测量轴上的区段端点近乎重合**（|ΔLo|+|ΔHi| &lt; 2mm）
    ///           **且** 垂直于测量轴的代表坐标近乎重合（竖向标注比较平均 X，横向比较平均 Y）
    ///           → 才是「同一条边上的重复尺寸」；忽略后者会把 **两处相距很远、高度同为 1700 的竖墙**
    ///           误判成一段几何而合并掉一条标注。
    ///   辅判据：dimLine 在「偏移轴」上可分辩地接近，且 measure 接近 → 合并（视觉冲突兜底）。
    ///
    /// 分组策略：仅在 (Rotation, Source) 同组内合并，跨 Source（如 OutsideLeft vs
    /// OutsideTotalLeft）天然不合，避免误合分段尺寸与总尺寸。
    ///
    /// 冲突时保留：measure 大者优先（信息量更大）；measure 相同时保留前者（dimLine 坐标小者）。
    /// </summary>
    public sealed class NearbyParallelMergePostProcessor : IDimensionPostProcessor
    {
        private const double RotationBucket = 1e-3;
        private const double MeasureTolerance = 1e-3;
        private const double SegmentMatchThreshold = 2.0;

        /// <summary>
        /// OutsideUp/Down 整串共用 bbox ± offset → DimLine.Y 完全一致；OutsideLeft/Right 同理 DimLine.X。
        /// 此时辅判据「dimLine 间距 &lt; mergeDistance」恒成立，若再配合 measure 接近会把 **相距很远但数值碰巧相同**
        /// （两处均为 1700）的尺寸错误合并 → 尺寸链出现空缺（用户日志 OU 11→10）。
        /// 重合在此阈值内视为「同一偏移基准链」：**只允许主判据（区段端点重合）合并**，禁止数值兜底合并。
        /// </summary>
        private const double ParallelChainCoincidenceEpsilon = 1.0;

        public IReadOnlyList<DerivedDimension> Process(
            IReadOnlyList<DerivedDimension> input, NewDdsConfig config)
        {
            if (input == null || input.Count == 0)
                return input ?? (IReadOnlyList<DerivedDimension>)new List<DerivedDimension>();

            var output = new List<DerivedDimension>(input.Count);
            var grouped = input.GroupBy(d => new GroupKey(
                Math.Round(d.Rotation / RotationBucket) * RotationBucket,
                d.Source));

            foreach (var group in grouped)
            {
                var merged = MergeRedundant(group.ToList(), config.DimDistanceTolerance);
                output.AddRange(merged);
            }
            return output;
        }

        private static List<DerivedDimension> MergeRedundant(
            List<DerivedDimension> sameRotSource, double mergeDistance)
        {
            int n = sameRotSource.Count;
            if (n <= 1) return sameRotSource;

            bool isHorizontal = Math.Abs(sameRotSource[0].Rotation) < RotationBucket;
            // 同一偏移链上 DimLine 的主坐标相同 → 必须按测量轴（水平→seg.minX，竖直→seg.minY）排序，
            // 否则 pairwise 顺序任意，易误判。
            var sorted = isHorizontal
                ? sameRotSource
                    .OrderBy(d => d.DimensionLinePoint.Y)
                    .ThenBy(d => GetMeasureSegment(d, true).Lo)
                    .ThenBy(d => GetMeasureSegment(d, true).Hi)
                    .ToList()
                : sameRotSource
                    .OrderBy(d => d.DimensionLinePoint.X)
                    .ThenBy(d => GetMeasureSegment(d, false).Lo)
                    .ThenBy(d => GetMeasureSegment(d, false).Hi)
                    .ToList();

            var keep = new bool[n];
            for (int i = 0; i < n; i++) keep[i] = true;

            for (int i = 0; i < n - 1; i++)
            {
                if (!keep[i]) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (!keep[j]) continue;
                    if (!ShouldMerge(sorted[i], sorted[j], isHorizontal, mergeDistance)) continue;

                    double iMeasure = ComputeMeasure(sorted[i], isHorizontal);
                    double jMeasure = ComputeMeasure(sorted[j], isHorizontal);
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

        private static bool ShouldMerge(DerivedDimension a, DerivedDimension b,
            bool isHorizontal, double mergeDistance)
        {
            // 主判据：测量轴区段重合 **且** 垂直方向代表坐标重合 → 同一边上的重复派生才合并。
            var aSeg = GetMeasureSegment(a, isHorizontal);
            var bSeg = GetMeasureSegment(b, isHorizontal);
            double segDelta = Math.Abs(aSeg.Lo - bSeg.Lo) + Math.Abs(aSeg.Hi - bSeg.Hi);
            double witnessSep = WitnessAxisSeparation(a, b, isHorizontal);
            if (segDelta < SegmentMatchThreshold && witnessSep < SegmentMatchThreshold)
                return true;

            // 辅判据：区段不重合，但 dimLine 在「偏移轴」上可分辩地接近，且 measure 接近 → 合并。
            if (mergeDistance <= 0.0) return false;
            double aKey = isHorizontal ? a.DimensionLinePoint.Y : a.DimensionLinePoint.X;
            double bKey = isHorizontal ? b.DimensionLinePoint.Y : b.DimensionLinePoint.X;
            if (Math.Abs(aKey - bKey) < ParallelChainCoincidenceEpsilon)
                return false;

            if (Math.Abs(aKey - bKey) >= mergeDistance) return false;
            double aMeasure = aSeg.Hi - aSeg.Lo;
            double bMeasure = bSeg.Hi - bSeg.Lo;
            return Math.Abs(aMeasure - bMeasure) < Math.Max(mergeDistance * 0.05, 1.0);
        }

        private static (double Lo, double Hi) GetMeasureSegment(DerivedDimension d, bool isHorizontal)
        {
            double v1 = isHorizontal ? d.ExtensionLine1Point.X : d.ExtensionLine1Point.Y;
            double v2 = isHorizontal ? d.ExtensionLine2Point.X : d.ExtensionLine2Point.Y;
            return v1 <= v2 ? (v1, v2) : (v2, v1);
        }

        private static double ComputeMeasure(DerivedDimension d, bool isHorizontal)
        {
            var seg = GetMeasureSegment(d, isHorizontal);
            return seg.Hi - seg.Lo;
        }

        /// <summary>
        /// 延伸线在「垂直于测量轴」方向上的代表间距：竖向尺寸（量 ΔY）取两端点平均 X；横向尺寸取平均 Y。
        /// 用于区分「同高度不同位置的竖墙」与「同一条竖边上的重复标注」。
        /// </summary>
        private static double WitnessAxisSeparation(DerivedDimension a, DerivedDimension b, bool isHorizontal)
        {
            if (isHorizontal)
            {
                double ay = (a.ExtensionLine1Point.Y + a.ExtensionLine2Point.Y) * 0.5;
                double by = (b.ExtensionLine1Point.Y + b.ExtensionLine2Point.Y) * 0.5;
                return Math.Abs(ay - by);
            }
            double ax = (a.ExtensionLine1Point.X + a.ExtensionLine2Point.X) * 0.5;
            double bx = (b.ExtensionLine1Point.X + b.ExtensionLine2Point.X) * 0.5;
            return Math.Abs(ax - bx);
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
