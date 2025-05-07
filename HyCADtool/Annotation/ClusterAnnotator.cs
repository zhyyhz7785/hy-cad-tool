using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System.Collections.Generic;

namespace HyCADTool.Annotation
{
    /// <summary>
    /// 聚类标注调度器：将聚类结果与轴线交点映射后完成标注
    /// </summary>
    public class ClusterAnnotator
    {
        private readonly double _scale;

        public ClusterAnnotator(double scale)
        {
            _scale = scale;
        }

        /// <summary>
        /// 执行每个聚类的标注绘制
        /// </summary>
        /// <param name="mapping">聚类与最近轴线交点的映射</param>
        public void Annotate(Dictionary<ClusterResult, Point3d> mapping)
        {
            if (mapping == null || mapping.Count == 0) return;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            // 创建图层（可复用）
            ObjectId layerX = EtGpt.CreateLayer("00_hy_3公共_标注2_内x", 93, db);
            ObjectId layerY = EtGpt.CreateLayer("00_hy_3公共_标注2_内y", 45, db);

            List<RotatedDimension> allDims = new List<RotatedDimension>();

            foreach (KeyValuePair<ClusterResult, Point3d> kv in mapping)
            {
                ClusterResult cluster = kv.Key;
                Point3d axisIntersection = kv.Value;

                List<Point3d> annotationPoints = cluster.GetAnnotationPoints(axisIntersection);
                if (annotationPoints.Count < 2) continue;

                // 调用原始双侧标注方法
                List<RotatedDimension> dims = EtGpt.CreateDimensionsForClusterBothSides(
                    annotationPoints,
                    layerX,
                    layerY,
                    new ClusterDimOptions
                    {
                        Scale = _scale
                        // 可添加更多选项：最小间距、偏移量等
                    });

                allDims.AddRange(dims);
            }

            // 写入模型空间
            if (allDims.Count > 0)
            {
                allDims.ToSpace(db);
            }
        }
    }
}
