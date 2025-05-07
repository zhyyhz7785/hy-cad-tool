using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyCADTool.Models.Cluster
{
    /// <summary>
    /// 聚类结果类，兼容标注框架所需字段
    /// </summary>
    public class ClusterResult
    {
        /// <summary>聚类ID</summary>
        public int ClusterId { get; set; }

        /// <summary>聚类包含的点</summary>
        public List<Point3d> Points { get; set; }

        /// <summary>聚类轮廓多段线</summary>
        public Polyline EnvelopePolyline { get; set; }

        /// <summary>可选：扩展轮廓</summary>
        public Polyline EnvelopeExpandedPolyline { get; set; }

        /// <summary>标注框架新增：聚类对应的 MBR 区域</summary>
        public Extents3d EnvelopeExtents { get; set; }

        /// <summary>标注框架新增：MBR 中心点（形心）</summary>
        public Point3d Center
        {
            get
            {
                var min = EnvelopeExtents.MinPoint;
                var max = EnvelopeExtents.MaxPoint;
                return new Point3d((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, 0);
            }
        }

        /// <summary>标注框架新增：包含在 MBR 内部的附加交点</summary>
        public List<Point3d> AdditionalIntersections { get; set; }

        /// <summary>标注框架新增：聚类中用于标注的全部点</summary>
        public List<Point3d> AllPoints
        {
            get
            {
                List<Point3d> result = new List<Point3d>();
                result.Add(Center);
                if (AdditionalIntersections != null && AdditionalIntersections.Count > 0)
                {
                    result.AddRange(AdditionalIntersections);
                }
                return result;
            }
        }

        public ClusterResult()
        {
            Points = new List<Point3d>();
            AdditionalIntersections = new List<Point3d>();
        }

        public List<Point3d> GetAnnotationPoints(Point3d? axisIntersection)
        {
            List<Point3d> result = new List<Point3d>();
            if (Points != null) result.AddRange(Points);
            if (AdditionalIntersections != null) result.AddRange(AdditionalIntersections);
            if (axisIntersection.HasValue) result.Add(axisIntersection.Value);
            return result;
        }

    }

}
