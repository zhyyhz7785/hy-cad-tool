//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Config;
//using HyCADTool.Models.Cluster;
//using HyCADTool.Tools;
//using System;
//using System.Linq;
//using System.Collections.Generic;
//namespace HyCADTool.Annotation
//{
//    /// <summary>
//    /// 聚类标注调度器：将聚类结果与轴线交点映射后完成标注
//    /// </summary>
//    public class ClusterAnnotator
//    {
//        private readonly double _scale;
//        public ClusterAnnotator(double scale)
//        {
//            _scale = scale;
//        }
//        public void Annotate(Dictionary<ClusterResult, Tuple<Point3d, Point3d>> mapping)
//        {
//            if (mapping == null || mapping.Count == 0) return;

//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;

//            ObjectId layerX = EtGpt.CreateLayer("00_hy_3公共_标注2_内x", 93, db);
//            ObjectId layerY = EtGpt.CreateLayer("00_hy_3公共_标注2_内y", 45, db);

//            List<RotatedDimension> allDims = new List<RotatedDimension>();

//            foreach (var kv in mapping)
//            {
//                ClusterResult cluster = kv.Key;
//                Point3d axisPt1 = kv.Value.Item1;
//                Point3d axisPt2 = kv.Value.Item2;

//                // 获取两个参考点生成的标注点集合
//                var annotationPoints = new List<Point3d>();
//                annotationPoints.AddRange(cluster.GetAnnotationPoints(axisPt1));
//                annotationPoints.AddRange(cluster.GetAnnotationPoints(axisPt2));

//                // 去重 + 排序（按 X 升序，Y 次序）
//                var processedPoints = annotationPoints
//                    .Distinct(new EtGpt.Point3dComparer(0.001))
//                    .OrderBy(p => p.X)
//                    .ThenBy(p => p.Y)
//                    .ToList();

//                // 至少两个点才能标注
//                if (processedPoints.Count >= 2)
//                {
//                    allDims.AddRange(EtGpt.CreateDimensionsForClusterBothSides(
//                        processedPoints, layerX, layerY, new ClusterDimOptions { Scale = _scale }));
//                }
//            }

//            if (allDims.Count > 0)
//            {
//                allDims.ToSpace(db);
//            }
//        }



//    }
//}
