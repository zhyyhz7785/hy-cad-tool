using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 构件识别：独立小轮廓判型 + 大轮廓割线扫描精确分区（板 / 底板）。
    /// 墙 / 梁 / 大体积为阶段 2，在分区结果上二次切分。
    /// </summary>
    public static class ComponentRecognizer
    {
        private const double MinSegmentLengthMm = 200.0;
        private const double AnchorYToleranceMm = 80.0;

        private const int PriorityBottomSlab = 1;
        private const int PrioritySlab = 2;
        private const int PriorityWall = 3;
        private const int PriorityBeam = 4;

        /// <summary>整批识别（独立轮廓判型 + 大轮廓割线分区）。</summary>
        public static List<ComponentRegion> Recognize(
            IReadOnlyList<ReinRegion> regions,
            ComponentParameters parameters)
        {
            var result = new List<ComponentRegion>();
            if (regions == null || regions.Count == 0 || parameters == null)
                return result;

            var globalBbox = ComputeGlobalBbox(regions);

            foreach (var region in regions)
            {
                if (region?.Outer == null || region.Outer.VertexCount < 3)
                    continue;

                var outerBbox = GetBoundingBox(region.Outer);

                if (TryClassifyStandalone(region, outerBbox, globalBbox, parameters, out var standalone))
                {
                    result.Add(standalone);
                    continue;
                }

                result.AddRange(RecognizeLargeRegion(region));
            }

            return result;
        }

        /// <summary>兼容旧调用：单区域识别。</summary>
        public static List<ComponentRegion> Recognize(ReinRegion region, ComponentParameters parameters)
        {
            if (region == null)
                return new List<ComponentRegion>();
            return Recognize(new[] { region }, parameters);
        }

        // ===================================================================
        //  独立小轮廓整体判型
        // ===================================================================

        private static bool TryClassifyStandalone(
            ReinRegion region,
            (double MinX, double MaxX, double MinY, double MaxY) outerBbox,
            (double MinX, double MaxX, double MinY, double MaxY) globalBbox,
            ComponentParameters parameters,
            out ComponentRegion regionOut)
        {
            regionOut = null;
            double w = outerBbox.MaxX - outerBbox.MinX;
            double h = outerBbox.MaxY - outerBbox.MinY;

            if (w <= parameters.BeamMaxWidthMm
                && h <= parameters.BeamMaxHeightMm
                && Math.Max(w, h) >= MinSegmentLengthMm)
            {
                regionOut = CreateRegion(region.Outer, ComponentType.Beam, Math.Min(w, h), PriorityBeam);
                return true;
            }

            if (w <= parameters.WallMaxThicknessMm
                && h >= 2.0 * w
                && h >= MinSegmentLengthMm)
            {
                regionOut = CreateRegion(region.Outer, ComponentType.Wall, w, PriorityWall);
                return true;
            }

            double flatLimit = Math.Max(parameters.SlabMaxThicknessMm, parameters.BottomSlabMaxThicknessMm);
            if (h <= flatLimit && w >= 2.0 * h && w >= MinSegmentLengthMm)
            {
                bool atBottom = outerBbox.MinY <= globalBbox.MinY + AnchorYToleranceMm;
                var type = atBottom ? ComponentType.BottomSlab : ComponentType.Slab;
                if (h <= (atBottom ? parameters.BottomSlabMaxThicknessMm : parameters.SlabMaxThicknessMm))
                {
                    regionOut = CreateRegion(region.Outer, type,
                        h, atBottom ? PriorityBottomSlab : PrioritySlab);
                    return true;
                }
            }

            return false;
        }

        // ===================================================================
        //  大轮廓：竖直割线扫描精确分区
        // ===================================================================

        private static List<ComponentRegion> RecognizeLargeRegion(ReinRegion region)
        {
            var partitions = RegionPartitioner.Partition(region);
            var result = new List<ComponentRegion>(partitions.Count);

            foreach (var part in partitions)
            {
                if (part?.Polygon == null || part.Polygon.VertexCount < 3)
                    continue;

                result.Add(new ComponentRegion
                {
                    Type = part.Type,
                    Polygon = new Polyline2D(part.Polygon.Vertices, isClosed: true),
                    ThicknessMm = part.ThicknessMm,
                    Priority = PriorityOf(part.Type)
                });
            }

            return result;
        }

        // ===================================================================
        //  公共工具
        // ===================================================================

        private static ComponentRegion CreateRegion(
            Polyline2D polygon,
            ComponentType type,
            double thickness,
            int priority)
        {
            return new ComponentRegion
            {
                Type = type,
                Polygon = polygon.Clone(),
                ThicknessMm = thickness,
                Priority = priority
            };
        }

        private static int PriorityOf(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.BottomSlab: return PriorityBottomSlab;
                case ComponentType.Slab: return PrioritySlab;
                case ComponentType.Wall: return PriorityWall;
                case ComponentType.Beam: return PriorityBeam;
                default: return PrioritySlab;
            }
        }

        private static (double MinX, double MaxX, double MinY, double MaxY) ComputeGlobalBbox(IReadOnlyList<ReinRegion> regions)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var region in regions)
            {
                if (region?.Outer == null)
                    continue;
                var b = GetBoundingBox(region.Outer);
                minX = Math.Min(minX, b.MinX);
                maxX = Math.Max(maxX, b.MaxX);
                minY = Math.Min(minY, b.MinY);
                maxY = Math.Max(maxY, b.MaxY);
            }

            if (minX == double.MaxValue)
                return (0, 0, 0, 0);

            return (minX, maxX, minY, maxY);
        }

        private static (double MinX, double MaxX, double MinY, double MaxY) GetBoundingBox(Polyline2D poly)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
            return (minX, maxX, minY, maxY);
        }

        public static ComponentType ClassifyPoint(Point2D point, IReadOnlyList<ComponentRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return ComponentType.Slab;

            foreach (var region in regions.OrderByDescending(r => r.Priority))
            {
                if (region.ContainsPoint(point))
                    return region.Type;
            }

            return ComponentType.Slab;
        }
    }
}
