using HyCAD.Geometry;
using System;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 识别出的构件区域（闭合多边形 + 类型 + 厚度）。
    /// </summary>
    public sealed class ComponentRegion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ComponentType Type { get; set; }
        public Polyline2D Polygon { get; set; }
        public double ThicknessMm { get; set; }
        public int Priority { get; set; }

        public Point2D Centroid
        {
            get
            {
                if (Polygon == null || Polygon.VertexCount == 0)
                    return Point2D.Origin;
                double x = 0, y = 0;
                for (int i = 0; i < Polygon.VertexCount; i++)
                {
                    var p = Polygon.GetPointAt(i);
                    x += p.X;
                    y += p.Y;
                }
                int n = Polygon.VertexCount;
                return new Point2D(x / n, y / n);
            }
        }

        public bool ContainsPoint(Point2D point)
        {
            if (Polygon == null || Polygon.VertexCount < 3)
                return false;
            return HyCAD.Geometry.Algorithms.PolygonAlgorithms.ContainsPoint(Polygon.Vertices, point);
        }
    }
}
