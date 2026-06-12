using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 大体积混凝土内部水平构造筋（高度范围内均匀分层）。
    /// </summary>
    public static class MassConcreteRebarGenerator
    {
        public static Polyline2D[] GenerateHorizontalBars(
            ComponentRegion region,
            ComponentParameters parameters,
            ReinParameters reinParameters)
        {
            if (region?.Polygon == null || region.Type != ComponentType.MassConcrete)
                return Array.Empty<Polyline2D>();

            double scale = reinParameters.Scale;
            double protection = reinParameters.ProtectionThickness * scale;
            double hookLength = reinParameters.HookLength * scale;
            double maxSpacing = parameters.MassConstructRebarMaxSpacing;

            var bbox = GetBoundingBox(region.Polygon);
            double height = bbox.MaxY - bbox.MinY;
            if (height <= maxSpacing + protection * 2)
                return Array.Empty<Polyline2D>();

            int layerCount = Math.Max(1, (int)Math.Ceiling(height / maxSpacing) - 1);
            var bars = new List<Polyline2D>();

            for (int i = 1; i <= layerCount; i++)
            {
                double y = bbox.MinY + protection + (height - 2 * protection) * i / (layerCount + 1);
                double xStart = bbox.MinX + protection;
                double xEnd = bbox.MaxX - protection;

                var bar = new Polyline2D();
                bar.AddVertex(new Point2D(xStart, y));
                bar.AddVertex(new Point2D(xEnd, y));

                AddHooks(bar, hookLength);
                bars.Add(bar);
            }

            return bars.ToArray();
        }

        private static void AddHooks(Polyline2D bar, double hookLength)
        {
            if (bar.VertexCount < 2 || hookLength <= 0)
                return;

            var seg = bar.GetSegmentAt(0);
            if (!seg.Direction.TryNormalize(out Vector2D dir))
                return;

            Vector2D hookDir = dir.Rotate(Math.PI * 3.0 / 4.0);
            Point2D startHook = bar.GetPointAt(0).Add(hookDir * hookLength);
            bar.AddVertexAt(0, startHook);

            var endSeg = bar.GetSegmentAt(bar.VertexCount - 2);
            if (!endSeg.Direction.TryNormalize(out Vector2D endDir))
                return;
            Vector2D endHookDir = endDir.Rotate(Math.PI * 3.0 / 4.0);
            Point2D endHook = bar.GetPointAt(bar.VertexCount - 1).Add(endHookDir * hookLength);
            bar.AddVertex(endHook);
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
    }
}
