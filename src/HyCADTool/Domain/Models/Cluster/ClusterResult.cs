using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Domain.Models.Cluster
{
    /// <summary>
    /// 聚类结果，包含聚类相关所有图形与分析数据（平台无关）
    /// </summary>
    public class ClusterResult
    {
        /// <summary>聚类ID</summary>
        public int ClusterId { get; set; }

        /// <summary>聚类包含的点</summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>辅助点（如轴线交点）</summary>
        public List<Point2D> AuxiliaryPoints { get; set; } = new List<Point2D>();

        /// <summary>所有点（Points + AuxiliaryPoints 去重）</summary>
        public IEnumerable<Point2D> AllPoints =>
            Points.Concat(AuxiliaryPoints)
                  .Distinct(new Point2DComparer(0.001));

        /// <summary>聚类轮廓所占的区域（AABB 包围盒）</summary>
        public BoundingBox Envelope { get; set; }

        /// <summary>扩展后的包围盒（考虑 ExpandMargins 后）</summary>
        public BoundingBox ExpandedEnvelope { get; set; }

        /// <summary>聚类中心点（Envelope 的几何中心）</summary>
        public Point2D Center => Envelope.Center;

        /// <summary>包含在 MBR 内的附加交点</summary>
        public List<Point2D> AdditionalIntersections { get; set; } = new List<Point2D>();

        /// <summary>聚类点集形成的凸包顶点（可选）</summary>
        public List<Point2D> ConvexHullVertices { get; set; }

        /// <summary>去除重复点</summary>
        public void EnsureUniquePoints(double tolerance)
        {
            if (Points == null || Points.Count <= 1) return;
            Points = Points.Distinct(new Point2DComparer(tolerance)).ToList();
        }
    }

    /// <summary>
    /// Point2D 相等比较器（基于容差）
    /// </summary>
    public class Point2DComparer : IEqualityComparer<Point2D>
    {
        private readonly double _tolerance;

        public Point2DComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(Point2D p1, Point2D p2)
        {
            return Math.Abs(p1.X - p2.X) <= _tolerance
                && Math.Abs(p1.Y - p2.Y) <= _tolerance;
        }

        public int GetHashCode(Point2D p)
        {
            int hx = (int)(p.X / _tolerance);
            int hy = (int)(p.Y / _tolerance);
            return hx * 397 ^ hy;
        }
    }
}
