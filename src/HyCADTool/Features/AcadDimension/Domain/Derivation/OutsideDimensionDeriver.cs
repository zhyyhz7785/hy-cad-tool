using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Derivation
{
    /// <summary>
    /// 外部 4 方向 + 总尺寸派生器（范式 S1a 解析式直接派生）。
    ///
    /// 算法骨架来自老 dds GenerateOutsideLeftRight / GenerateOutsideUpDown，但 Domain 化、纯几何，
    /// 修 06 §7 三处问题：
    ///   #4 FillArray 引入幻边 → 直接用 jagged 列表，取每行 First / Last，不补齐；
    ///   #7 无总尺寸开关 → config.GenerateOutsideTotalDimension 控制；
    ///  #10 散落于 Service → 全部进 Deriver，状态由参数传递。
    ///
    /// 算法本质：
    ///   水平扫描列簇里，每行都有"最左 / 最右"边段；
    ///   把所有"最左边段"的端点收集 + 去重 + 按 Y 升序 → 即左外侧标注链特征点；
    ///   相邻特征点配成一条 Outside-Left 标注；首尾连成一条 OutsideTotal-Left 标注（若开启）。
    ///   左/右/上/下 四方向同构，仅排序键与偏移方向不同。
    /// </summary>
    public sealed class OutsideDimensionDeriver : IDimensionDeriver
    {
        private const double Tolerance = 1e-6;

        public IReadOnlyList<DerivedDimension> Derive(BoundaryFeatures features, NewDdsConfig config)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var dims = new List<DerivedDimension>();
            var bounds = features.Bounds;

            DeriveLeft(features.HorizontalSecantColumns, bounds, config, dims);
            DeriveRight(features.HorizontalSecantColumns, bounds, config, dims);
            DeriveDown(features.VerticalSecantColumns, bounds, config, dims);
            DeriveUp(features.VerticalSecantColumns, bounds, config, dims);

            return dims;
        }

        // === 左侧 ====================================================================
        private static void DeriveLeft(
            IReadOnlyList<IReadOnlyList<Line2D>> horizontalCols,
            BoundingBox bounds, NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (horizontalCols == null || horizontalCols.Count == 0) return;
            var leftEdges = horizontalCols
                .Where(row => row != null && row.Count > 0)
                .Select(row => row[0])
                .ToList();
            if (leftEdges.Count == 0) return;

            var points = ExtractDistinctPoints(leftEdges)
                .OrderBy(p => p.Y).ThenBy(p => p.X)
                .ToList();
            if (points.Count < 2) return;

            double dimLineX = bounds.MinPoint.X - config.DimensionDistanceOutside;
            double totalDimLineX = dimLineX - config.DimensionDistanceWithDim;

            for (int i = 0; i < points.Count - 1; i++)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[i],
                    ExtensionLine2Point = points[i + 1],
                    DimensionLinePoint = new Point2D(dimLineX, points[i].Y),
                    Rotation = Math.PI / 2.0,
                    Source = DimensionSource.OutsideLeft
                });
            }

            if (config.GenerateOutsideTotalDimension)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[0],
                    ExtensionLine2Point = points[points.Count - 1],
                    DimensionLinePoint = new Point2D(totalDimLineX, points[0].Y),
                    Rotation = Math.PI / 2.0,
                    Source = DimensionSource.OutsideTotalLeft
                });
            }
        }

        // === 右侧 ====================================================================
        private static void DeriveRight(
            IReadOnlyList<IReadOnlyList<Line2D>> horizontalCols,
            BoundingBox bounds, NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (horizontalCols == null || horizontalCols.Count == 0) return;
            var rightEdges = horizontalCols
                .Where(row => row != null && row.Count > 0)
                .Select(row => row[row.Count - 1])
                .ToList();
            if (rightEdges.Count == 0) return;

            var points = ExtractDistinctPoints(rightEdges)
                .OrderBy(p => p.Y).ThenByDescending(p => p.X)
                .ToList();
            if (points.Count < 2) return;

            double dimLineX = bounds.MaxPoint.X + config.DimensionDistanceOutside;
            double totalDimLineX = dimLineX + config.DimensionDistanceWithDim;

            for (int i = 0; i < points.Count - 1; i++)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[i],
                    ExtensionLine2Point = points[i + 1],
                    DimensionLinePoint = new Point2D(dimLineX, points[i].Y),
                    Rotation = Math.PI / 2.0,
                    Source = DimensionSource.OutsideRight
                });
            }

            if (config.GenerateOutsideTotalDimension)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[0],
                    ExtensionLine2Point = points[points.Count - 1],
                    DimensionLinePoint = new Point2D(totalDimLineX, points[0].Y),
                    Rotation = Math.PI / 2.0,
                    Source = DimensionSource.OutsideTotalRight
                });
            }
        }

        // === 下侧 ====================================================================
        private static void DeriveDown(
            IReadOnlyList<IReadOnlyList<Line2D>> verticalCols,
            BoundingBox bounds, NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (verticalCols == null || verticalCols.Count == 0) return;
            var downEdges = verticalCols
                .Where(row => row != null && row.Count > 0)
                .Select(row => row[0])
                .ToList();
            if (downEdges.Count == 0) return;

            var points = ExtractDistinctPoints(downEdges)
                .OrderBy(p => p.X).ThenBy(p => p.Y)
                .ToList();
            if (points.Count < 2) return;

            double dimLineY = bounds.MinPoint.Y - config.DimensionDistanceOutside;
            double totalDimLineY = dimLineY - config.DimensionDistanceWithDim;

            for (int i = 0; i < points.Count - 1; i++)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[i],
                    ExtensionLine2Point = points[i + 1],
                    DimensionLinePoint = new Point2D(points[i].X, dimLineY),
                    Rotation = 0.0,
                    Source = DimensionSource.OutsideDown
                });
            }

            if (config.GenerateOutsideTotalDimension)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[0],
                    ExtensionLine2Point = points[points.Count - 1],
                    DimensionLinePoint = new Point2D(points[0].X, totalDimLineY),
                    Rotation = 0.0,
                    Source = DimensionSource.OutsideTotalDown
                });
            }
        }

        // === 上侧 ====================================================================
        private static void DeriveUp(
            IReadOnlyList<IReadOnlyList<Line2D>> verticalCols,
            BoundingBox bounds, NewDdsConfig config, List<DerivedDimension> dims)
        {
            if (verticalCols == null || verticalCols.Count == 0) return;
            var upEdges = verticalCols
                .Where(row => row != null && row.Count > 0)
                .Select(row => row[row.Count - 1])
                .ToList();
            if (upEdges.Count == 0) return;

            var points = ExtractDistinctPoints(upEdges)
                .OrderBy(p => p.X).ThenByDescending(p => p.Y)
                .ToList();
            if (points.Count < 2) return;

            double dimLineY = bounds.MaxPoint.Y + config.DimensionDistanceOutside;
            double totalDimLineY = dimLineY + config.DimensionDistanceWithDim;

            for (int i = 0; i < points.Count - 1; i++)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[i],
                    ExtensionLine2Point = points[i + 1],
                    DimensionLinePoint = new Point2D(points[i].X, dimLineY),
                    Rotation = 0.0,
                    Source = DimensionSource.OutsideUp
                });
            }

            if (config.GenerateOutsideTotalDimension)
            {
                dims.Add(new DerivedDimension
                {
                    ExtensionLine1Point = points[0],
                    ExtensionLine2Point = points[points.Count - 1],
                    DimensionLinePoint = new Point2D(points[0].X, totalDimLineY),
                    Rotation = 0.0,
                    Source = DimensionSource.OutsideTotalUp
                });
            }
        }

        // === 端点收集 + 去重 =========================================================
        private static IEnumerable<Point2D> ExtractDistinctPoints(IEnumerable<Line2D> edges)
        {
            var seen = new List<Point2D>();
            foreach (var e in edges)
            {
                AddIfNew(seen, e.StartPoint);
                AddIfNew(seen, e.EndPoint);
            }
            return seen;
        }

        private static void AddIfNew(List<Point2D> list, Point2D p)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Math.Abs(list[i].X - p.X) < Tolerance &&
                    Math.Abs(list[i].Y - p.Y) < Tolerance) return;
            }
            list.Add(p);
        }
    }
}
