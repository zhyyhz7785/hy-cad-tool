using System;
using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Derivation
{
    /// <summary>
    /// 内部尺寸派生器（图2 dds 中底部凸起左右宽度 / 上下高度等局部尺寸）。
    ///
    /// 算法骨架来自老 dds GenerateInsideLeftRight / GenerateInsideUpDown，但 Domain 化、纯几何，
    /// 修 06 §7 #4：FillArray 对齐到 maxCols 引入幻边 → 这里直接用 jagged 的
    /// `row[1..row.Count-2]` 作为"中间列"，零幻边。
    ///
    /// 算法本质：
    ///   水平 SweepLine 列簇里，每行的中间边段（除去最左 / 最右两条外侧边）= polyline
    ///   内部凸起 / 凹陷的局部边段。每条边段独立生成一条尺寸：
    ///     竖直边段（StartY≠EndY，StartX≈EndX）→ ForLeft/Right 标 Y 高度（凸起的高度）
    ///     水平边段（StartY≈EndY，StartX≠EndX）→ ForUp/Down 标 X 宽度（凸起的宽度）
    ///   方向（toLeft/Right or toUp/Down）由"与左右邻接边段的间距大小"决定——
    ///   标到间距更大的一侧避免文字挤压（与老 dds GenerateInsideLeftRight 同策略）。
    /// </summary>
    public sealed class InsideDimensionDeriver : IDimensionDeriver
    {
        private const double ZeroMeasureThreshold = 1.0;
        private const double DedupePrecision = 0.5;

        public IReadOnlyList<DerivedDimension> Derive(BoundaryFeatures features, NewDdsConfig config)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var dims = new List<DerivedDimension>();
            DeriveInsideLeftRight(features.HorizontalSecantColumns, config, dims);
            DeriveInsideUpDown(features.VerticalSecantColumns, config, dims);
            return DeduplicateByEndpoints(dims);
        }

        // === 内部 Left/Right（量竖直边段的 Y 高度）====================================
        private static void DeriveInsideLeftRight(
            IReadOnlyList<IReadOnlyList<Line2D>> horizontalCols,
            NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (horizontalCols == null) return;
            foreach (var row in horizontalCols)
            {
                if (row == null || row.Count < 3) continue;
                for (int i = 1; i < row.Count - 1; i++)
                {
                    var edge = row[i];
                    double measure = Math.Abs(edge.StartPoint.Y - edge.EndPoint.Y);
                    if (measure < ZeroMeasureThreshold) continue;

                    double edgeMinX = Math.Min(edge.StartPoint.X, edge.EndPoint.X);
                    double edgeMaxX = Math.Max(edge.StartPoint.X, edge.EndPoint.X);

                    var leftEdge = row[i - 1];
                    var rightEdge = row[i + 1];
                    double leftEdgeMaxX = Math.Max(leftEdge.StartPoint.X, leftEdge.EndPoint.X);
                    double rightEdgeMinX = Math.Min(rightEdge.StartPoint.X, rightEdge.EndPoint.X);

                    double leftGap = edgeMinX - leftEdgeMaxX;
                    double rightGap = rightEdgeMinX - edgeMaxX;
                    bool toLeft = leftGap > rightGap;

                    NormalizeByY(edge.StartPoint, edge.EndPoint, out var lo, out var hi);
                    double dimLineX = toLeft
                        ? edgeMinX - config.DimensionDistanceInside
                        : edgeMaxX + config.DimensionDistanceInside;

                    dims.Add(new DerivedDimension
                    {
                        ExtensionLine1Point = lo,
                        ExtensionLine2Point = hi,
                        DimensionLinePoint = new Point2D(dimLineX, lo.Y),
                        Rotation = Math.PI / 2.0,
                        Source = DimensionSource.InsideLeftRight
                    });
                }
            }
        }

        // === 内部 Up/Down（量水平边段的 X 宽度）======================================
        private static void DeriveInsideUpDown(
            IReadOnlyList<IReadOnlyList<Line2D>> verticalCols,
            NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (verticalCols == null) return;
            foreach (var row in verticalCols)
            {
                if (row == null || row.Count < 3) continue;
                for (int i = 1; i < row.Count - 1; i++)
                {
                    var edge = row[i];
                    double measure = Math.Abs(edge.StartPoint.X - edge.EndPoint.X);
                    if (measure < ZeroMeasureThreshold) continue;

                    double edgeMinY = Math.Min(edge.StartPoint.Y, edge.EndPoint.Y);
                    double edgeMaxY = Math.Max(edge.StartPoint.Y, edge.EndPoint.Y);

                    var downEdge = row[i - 1];
                    var upEdge = row[i + 1];
                    double downEdgeMaxY = Math.Max(downEdge.StartPoint.Y, downEdge.EndPoint.Y);
                    double upEdgeMinY = Math.Min(upEdge.StartPoint.Y, upEdge.EndPoint.Y);

                    double downGap = edgeMinY - downEdgeMaxY;
                    double upGap = upEdgeMinY - edgeMaxY;
                    bool toDown = downGap > upGap;

                    NormalizeByX(edge.StartPoint, edge.EndPoint, out var left, out var right);
                    double dimLineY = toDown
                        ? edgeMinY - config.DimensionDistanceInside
                        : edgeMaxY + config.DimensionDistanceInside;

                    dims.Add(new DerivedDimension
                    {
                        ExtensionLine1Point = left,
                        ExtensionLine2Point = right,
                        DimensionLinePoint = new Point2D(left.X, dimLineY),
                        Rotation = 0.0,
                        Source = DimensionSource.InsideUpDown
                    });
                }
            }
        }

        // === 工具 =====================================================================
        private static void NormalizeByY(Point2D a, Point2D b, out Point2D lo, out Point2D hi)
        {
            if (a.Y <= b.Y) { lo = a; hi = b; } else { lo = b; hi = a; }
        }

        private static void NormalizeByX(Point2D a, Point2D b, out Point2D left, out Point2D right)
        {
            if (a.X <= b.X) { left = a; right = b; } else { left = b; right = a; }
        }

        // 端点对去重（同 polyline 边在多扫描线被多次取到 → 单条标注即可）。
        private static List<DerivedDimension> DeduplicateByEndpoints(List<DerivedDimension> dims)
        {
            var seen = new Dictionary<string, DerivedDimension>(dims.Count);
            foreach (var d in dims)
            {
                string key = MakeEndpointKey(d.ExtensionLine1Point, d.ExtensionLine2Point, d.Source);
                if (!seen.ContainsKey(key)) seen[key] = d;
            }
            var result = new List<DerivedDimension>(seen.Count);
            foreach (var v in seen.Values) result.Add(v);
            return result;
        }

        private static string MakeEndpointKey(Point2D a, Point2D b, DimensionSource source)
        {
            // 方向无关化：按 (X,Y) 字典序选小端为 first
            bool aFirst = a.X < b.X || (Math.Abs(a.X - b.X) < DedupePrecision && a.Y <= b.Y);
            var first = aFirst ? a : b;
            var second = aFirst ? b : a;
            long ax = (long)Math.Round(first.X / DedupePrecision);
            long ay = (long)Math.Round(first.Y / DedupePrecision);
            long bx = (long)Math.Round(second.X / DedupePrecision);
            long by = (long)Math.Round(second.Y / DedupePrecision);
            return $"{(int)source}|{ax}_{ay}_{bx}_{by}";
        }
    }
}
