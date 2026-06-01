using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.PostProcessing
{
    /// <summary>
    /// ExtensionLine 等长后处理器（语义对齐老 dds DimVsEqualLength）。
    ///
    /// 视觉问题：DerivedDimension 的 XLine1/2Point 来自 polyline 顶点。
    /// 多次扫描中"最上 / 最下 / 最左 / 最右"边段的端点 Y/X 不一致——
    /// 直接渲染会让某些 ExtensionLine 横穿 polyline 主体（极不专业），
    /// 这正是上一版 NewDDS 图里"polyline 内部出现尺寸数字"的原因。
    ///
    /// 解决：按 Source 分组（同组共享 dimLine），找该组所有 ExtensionLine
    /// 中**最短**的那条，把所有 ExtensionLine 都对齐到该长度——视觉上等长悬空，
    /// 不再延伸到 polyline 顶点，与老 dds 工程惯例一致。
    /// </summary>
    public sealed class EqualExtensionLengthPostProcessor : IDimensionPostProcessor
    {
        private const double Tolerance = 1e-6;

        public IReadOnlyList<DerivedDimension> Process(
            IReadOnlyList<DerivedDimension> input, NewDdsConfig config)
        {
            if (input == null || input.Count == 0)
                return input ?? (IReadOnlyList<DerivedDimension>)new List<DerivedDimension>();

            var output = new List<DerivedDimension>(input.Count);
            var groups = input.GroupBy(d => d.Source);
            foreach (var group in groups)
            {
                var groupList = group.ToList();
                double minLen = ComputeMinExtensionLength(groupList);
                foreach (var d in groupList)
                    output.Add(EqualizeExtension(d, minLen));
            }
            return output;
        }

        private static double ComputeMinExtensionLength(List<DerivedDimension> dims)
        {
            double min = double.MaxValue;
            foreach (var d in dims)
            {
                double l1 = ExtensionLength(d.ExtensionLine1Point, d.DimensionLinePoint, d.Rotation);
                double l2 = ExtensionLength(d.ExtensionLine2Point, d.DimensionLinePoint, d.Rotation);
                double shorter = Math.Min(l1, l2);
                if (shorter < min) min = shorter;
            }
            return min == double.MaxValue ? 0.0 : min;
        }

        private static double ExtensionLength(Point2D ext, Point2D dimLine, double rotation)
        {
            bool isHorizontal = Math.Abs(rotation) < Tolerance;
            return isHorizontal
                ? Math.Abs(ext.Y - dimLine.Y)
                : Math.Abs(ext.X - dimLine.X);
        }

        private static DerivedDimension EqualizeExtension(DerivedDimension d, double targetLen)
        {
            bool isHorizontal = Math.Abs(d.Rotation) < Tolerance;
            var dl = d.DimensionLinePoint;
            Point2D newExt1, newExt2;

            if (isHorizontal)
            {
                int sign1 = d.ExtensionLine1Point.Y >= dl.Y ? 1 : -1;
                int sign2 = d.ExtensionLine2Point.Y >= dl.Y ? 1 : -1;
                newExt1 = new Point2D(d.ExtensionLine1Point.X, dl.Y + sign1 * targetLen);
                newExt2 = new Point2D(d.ExtensionLine2Point.X, dl.Y + sign2 * targetLen);
            }
            else
            {
                int sign1 = d.ExtensionLine1Point.X >= dl.X ? 1 : -1;
                int sign2 = d.ExtensionLine2Point.X >= dl.X ? 1 : -1;
                newExt1 = new Point2D(dl.X + sign1 * targetLen, d.ExtensionLine1Point.Y);
                newExt2 = new Point2D(dl.X + sign2 * targetLen, d.ExtensionLine2Point.Y);
            }

            return new DerivedDimension
            {
                ExtensionLine1Point = newExt1,
                ExtensionLine2Point = newExt2,
                DimensionLinePoint = dl,
                Rotation = d.Rotation,
                Source = d.Source
            };
        }
    }
}
