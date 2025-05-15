//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.GeoUtils;
//using HyCADTool.HelpClass;
//using HyCADTool.Models.Cluster;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Annotation
//{
//    /// <summary>
//    /// 标注系统主流程调度器
//    /// </summary>
//    public class AnnotationFramework
//    {
//        /// <summary>
//        /// 执行标注流程
//        /// </summary>
//        /// <param name="clusterResults">聚类结果，每项含 MBR 和中心点</param>
//        /// <param name="xAxes">X 向轴线（水平线）</param>
//        /// <param name="yAxes">Y 向轴线（竖直线）</param>
//        /// <param name="scale">图纸比例系数</param>
//        public void Run(List<ClusterResult> clusterResults, List<Line> xAxes, List<Line> yAxes, double scale)
//        {
//            // 步骤 1：建立 cluster → 最近交点 + 附加交点 映射
//            var mapper = new AxisIntersectionMapper();
//            var mapping = mapper.MapClustersToIntersections(clusterResults, xAxes, yAxes);
//            // 步骤 2：根据点集合完成自动标注
//            var annotator = new ClusterAnnotator(scale);
//            annotator.Annotate(mapping);
//        }
//    }
//}
