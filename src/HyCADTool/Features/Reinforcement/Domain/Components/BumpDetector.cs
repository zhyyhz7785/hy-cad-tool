using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 检测底板/顶板局部凸起（高差 ≤ 阈值），用于主筋拉通。
    /// </summary>
    public static class BumpDetector
    {
        public static List<Polyline2D> DetectBumps(Polyline2D boundary, double maxBumpHeightMm)
        {
            var bumps = new List<Polyline2D>();
            if (boundary == null || boundary.VertexCount < 4)
                return bumps;

            var bbox = GetBoundingBox(boundary);
            double baselineY = bbox.MinY + (bbox.MaxY - bbox.MinY) * 0.25;

            for (int i = 0; i < boundary.VertexCount; i++)
            {
                var p = boundary.GetPointAt(i);
                if (p.Y - baselineY > 1.0 && p.Y - baselineY <= maxBumpHeightMm)
                {
                    int i0 = (i - 1 + boundary.VertexCount) % boundary.VertexCount;
                    int i1 = i;
                    int i2 = (i + 1) % boundary.VertexCount;
                    var bump = new Polyline2D(new[]
                    {
                        boundary.GetPointAt(i0),
                        boundary.GetPointAt(i1),
                        boundary.GetPointAt(i2)
                    });
                    bumps.Add(bump);
                }
            }

            return bumps;
        }

        /// <summary>
        /// 将局部凸起顶点投影回主面，使主筋不在凸起处折断。
        /// </summary>
        public static Polyline2D FlattenForMainRebar(Polyline2D boundary, double maxBumpHeightMm)
        {
            if (boundary == null)
                return boundary;

            var bbox = GetBoundingBox(boundary);
            double baselineY = bbox.MinY + (bbox.MaxY - bbox.MinY) * 0.25;
            var verts = new List<Point2D>();

            for (int i = 0; i < boundary.VertexCount; i++)
            {
                var p = boundary.GetPointAt(i);
                double delta = p.Y - baselineY;
                if (delta > 1.0 && delta <= maxBumpHeightMm)
                    p = new Point2D(p.X, baselineY);
                verts.Add(p);
            }

            var flat = new Polyline2D(verts, boundary.IsClosed);
            flat.RemoveDuplicateVertices();
            return flat;
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
