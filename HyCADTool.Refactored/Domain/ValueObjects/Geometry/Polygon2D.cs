using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维多边形值对象（平台无关）
    /// 假设多边形为简单多边形（无自交）
    /// </summary>
    public class Polygon2D
    {
        private readonly List<Point2D> _vertices;

        /// <summary>
        /// 顶点列表（只读）
        /// </summary>
        public IReadOnlyList<Point2D> Vertices => _vertices.AsReadOnly();

        /// <summary>
        /// 是否闭合（默认为 true）
        /// </summary>
        public bool IsClosed { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="vertices">顶点集合</param>
        /// <param name="isClosed">是否闭合（默认 true）</param>
        public Polygon2D(IEnumerable<Point2D> vertices, bool isClosed = true)
        {
            if (vertices == null)
                throw new ArgumentNullException(nameof(vertices));

            _vertices = new List<Point2D>(vertices);

            if (_vertices.Count < 3)
                throw new ArgumentException("Polygon must have at least 3 vertices", nameof(vertices));

            IsClosed = isClosed;
        }

        /// <summary>
        /// 顶点数量
        /// </summary>
        public int VertexCount => _vertices.Count;

        /// <summary>
        /// 计算多边形有向面积（Signed Area）
        /// 正值表示逆时针，负值表示顺时针
        /// </summary>
        public double GetSignedArea()
        {
            double area = 0.0;
            int n = _vertices.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += _vertices[i].X * _vertices[j].Y;
                area -= _vertices[j].X * _vertices[i].Y;
            }

            return area / 2.0;
        }

        /// <summary>
        /// 计算多边形面积（绝对值）
        /// </summary>
        public double GetArea()
        {
            return Math.Abs(GetSignedArea());
        }

        /// <summary>
        /// 判断多边形方向（逆时针 = true，顺时针 = false）
        /// </summary>
        public bool IsCounterClockwise()
        {
            return GetSignedArea() > 0;
        }

        /// <summary>
        /// 计算多边形质心（几何中心）
        /// 使用面积加权法计算
        /// </summary>
        public Point2D GetCentroid()
        {
            double cx = 0.0, cy = 0.0;
            double signedArea = 0.0;
            int n = _vertices.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double cross = _vertices[i].X * _vertices[j].Y - _vertices[j].X * _vertices[i].Y;
                signedArea += cross;
                cx += (_vertices[i].X + _vertices[j].X) * cross;
                cy += (_vertices[i].Y + _vertices[j].Y) * cross;
            }

            signedArea /= 2.0;
            double area = Math.Abs(signedArea);

            if (area < 1e-10)
            {
                // 退化情况：返回顶点的算术平均值
                double sumX = _vertices.Sum(v => v.X);
                double sumY = _vertices.Sum(v => v.Y);
                return new Point2D(sumX / n, sumY / n);
            }

            cx /= (6.0 * signedArea);
            cy /= (6.0 * signedArea);

            return new Point2D(cx, cy);
        }

        /// <summary>
        /// 计算多边形边界框
        /// </summary>
        public BoundingBox GetBoundingBox()
        {
            double minX = _vertices.Min(v => v.X);
            double minY = _vertices.Min(v => v.Y);
            double maxX = _vertices.Max(v => v.X);
            double maxY = _vertices.Max(v => v.Y);

            return new BoundingBox(
                new Point2D(minX, minY),
                new Point2D(maxX, maxY)
            );
        }

        /// <summary>
        /// 判断点是否在多边形内（使用射线法）
        /// </summary>
        /// <param name="point">待检测的点</param>
        /// <returns>如果点在多边形内返回 true</returns>
        public bool ContainsPoint(Point2D point)
        {
            bool inside = false;
            int n = _vertices.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = _vertices[i].X, yi = _vertices[i].Y;
                double xj = _vertices[j].X, yj = _vertices[j].Y;

                bool intersect = ((yi > point.Y) != (yj > point.Y)) &&
                    (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>
        /// 获取多边形的所有边
        /// </summary>
        public IEnumerable<Line2D> GetEdges()
        {
            int n = _vertices.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                yield return new Line2D(_vertices[i], _vertices[j]);
            }
        }

        /// <summary>
        /// 反转多边形顶点顺序
        /// </summary>
        public Polygon2D Reverse()
        {
            var reversedVertices = _vertices.AsEnumerable().Reverse();
            return new Polygon2D(reversedVertices, IsClosed);
        }

        public override string ToString()
        {
            return $"Polygon2D[{VertexCount} vertices, Area={GetArea():F2}]";
        }
    }
}

