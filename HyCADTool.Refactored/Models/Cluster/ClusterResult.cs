using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Models.Cluster
{
    public class ClusterResult
    {
        public List<Point3d> Points { get; set; }
        public Point3d Center { get; set; }
        // 可扩展：外包矩形、编号等属性
    }
}
