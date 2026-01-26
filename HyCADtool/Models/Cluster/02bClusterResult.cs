using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HyCADTool.Tools.ZTools;
namespace HyCADTool.Models.Cluster
{
    public class ClusterResult
    {
        public IEnumerable<Point3d> AllPoints => Points.Concat(AuxiliaryPoints).Distinct(new Point3dComparer(0.001));

        public List<Point3d> AuxiliaryPoints { get; set; } = new List<Point3d>();

        /// <summary>聚类ID</summary>
        public int ClusterId { get; set; }

        /// <summary>聚类包含的点</summary>
        public List<Point3d> Points { get; set; } = new List<Point3d>();

        /// <summary>聚类轮廓多段线（最小外包矩形）</summary>
        public Polyline EnvelopePolyline { get; set; }

        /// <summary>扩展轮廓多段线（考虑 ExpandMargins 后）</summary>
        public Polyline EnvelopeExpandedPolyline { get; set; }

        /// <summary>聚类轮廓所占的区域（Extents）</summary>
        public Extents3d EnvelopeExtents { get; set; }

        /// <summary>聚类中心点（EnvelopeExtents 的几何中心）</summary>
        public Point3d Center => new Point3d(
            (EnvelopeExtents.MinPoint.X + EnvelopeExtents.MaxPoint.X) / 2.0,
            (EnvelopeExtents.MinPoint.Y + EnvelopeExtents.MaxPoint.Y) / 2.0,
            0);

        /// <summary>包含在 MBR 内的附加交点</summary>
        public List<Point3d> AdditionalIntersections { get; set; } = new List<Point3d>();

        /// <summary>聚类点集形成的标准凸包（可选）</summary>
        public Polyline ConvexHullPolyline { get; set; }

        /// <summary>聚类中用于标注的全部点（包含 Center 与交点）</summary>
     
        public void EnsureUniquePoints(double tolerance)
        {
            if (Points == null || Points.Count <= 1) return;

            var comparer = new Tools.ZTools.Point3dComparer(tolerance);
            Points = Points.Distinct(comparer).ToList();
        }
        /// <summary>
        /// 获取用于标注的点集（Points + 交点 + 选定轴线交点）
        /// </summary>
        public List<Point3d> GetAnnotationPoints(Point3d? axisIntersection)
        {
            var result = new List<Point3d>(Points);
            if (AdditionalIntersections != null)
                result.AddRange(AdditionalIntersections);
            if (axisIntersection.HasValue)
                result.Add(axisIntersection.Value);
            return result;
        }
    }
}
