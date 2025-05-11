using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Models.Cluster;
using System.Collections.Generic;

namespace HyCADTool.Services
{
    public class ClusterService : IClusterService
    {
        public List<ClusterResult> PerformClustering(List<Point3d> points, ClusterConfig config)
        {
            // TODO: 替换为原 PointsClusterHelper 中聚类逻辑
            var results = new List<ClusterResult>();

            // 示例聚类逻辑（待替换）
            if (points.Count == 0) return results;

            var result = new ClusterResult
            {
                Points = points,
                Center = new Point3d(0, 0, 0) // TODO: 计算实际重心
            };
            results.Add(result);
            return results;
        }

        public void AnnotateClusters(List<ClusterResult> results)
        {
            // TODO: 调用 AutoCAD API 添加注释（参考原 ClusterAnnotator）
        }
    }
}
