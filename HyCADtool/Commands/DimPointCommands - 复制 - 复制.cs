//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Config;
//using HyCADTool.Models;
//using HyCADTool.Tools;
//using System.Collections.Generic;



//namespace HyCADTool.Commands
//{
//    public static partial class HyCommand
//    {
//        [CommandMethod("HY_HighlightRegionPoints")]
//        public static void AnnotateAxes11()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ed = doc.Editor;

//            var data = HyCADTool.Models.Cluster.DimPointsAndAxis.GetInput();
//            if (data == null || data.AxisLines == null || data.AxisLines.Count == 0)
//            {
//                ed.WriteMessage("\n未获取到有效输入。");
//                return;
//            }

//            // 构建 AxisDatas，含点区域映射
//            var ann = AxisDatas.FromLines(data.AxisLines, data.SelectPoints);

//            var allEntities = new List<Entity>();
//            int colorIndex = 1;

//            // 区域图元及其对应点的绘制
//            foreach (var regionKvp in ann.RegionMap)
//            {
//                string regionName = regionKvp.Key;
//                Extents3d ext = regionKvp.Value;
//                Point3d labelPoint = ann.RegionLabels[regionName];

//                // 创建边框多段线
//                var pl = new Polyline(4) { Closed = true };
//                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0, 0, 0);
//                pl.AddVertexAt(1, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0, 0, 0);
//                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0, 0, 0);
//                pl.AddVertexAt(3, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0, 0, 0);
//                pl.ColorIndex = colorIndex;
//                allEntities.Add(pl);

//                // 添加区域文字
//                var txt = new DBText
//                {
//                    TextString = regionName,
//                    Position = labelPoint,
//                    Height = 5 * BaseConfig.Scale,
//                    HorizontalMode = TextHorizontalMode.TextCenter,
//                    VerticalMode = TextVerticalMode.TextVerticalMid,
//                    AlignmentPoint = labelPoint,
//                    ColorIndex = colorIndex
//                };
//                allEntities.Add(txt);

//                // 若区域包含点，添加 DBPoint
//                if (ann.PointsMap.ContainsKey(ext))
//                {
//                    foreach (var pt in ann.PointsMap[ext])
//                    {
//                        var dbPt = new DBPoint(pt) { ColorIndex = colorIndex };
//                        allEntities.Add(dbPt);
//                    }
//                }

//                colorIndex = (colorIndex % 255) + 1; // 避免超出有效 ACI 范围（1~255）
//            }

//            // 添加轴线圆和文字（默认颜色）
//            allEntities.AddRange(ann.AxisCircles);
//            allEntities.AddRange(ann.AxisTexts);

//            // 写入模型空间
//            allEntities.ToSpace();
//        }

//        /// <summary>
//        /// 从 Polyline 生成 Region 对象，用于几何判定
//        /// </summary>
//        private static Region CreateRegionFromPolyline(Polyline pline, Database db, Transaction tr)
//        {
//            var curves = new DBObjectCollection();
//            var clone = (Polyline)pline.Clone();
//            clone.Closed = true;
//            clone.ReverseCurve(); // 防止法向量异常
//            curves.Add(clone);

//            var regions = Region.CreateFromCurves(curves);
//            return regions.Count > 0 ? regions[0] as Region : null;
//        }

      

//        /// <summary>
//        /// 示例点集（请替换为实际点集）
//        /// </summary>
//        private static List<Point3d> GetTestPoints()
//        {
//            var points = new List<Point3d>();
//            for (int i = 0; i < 100; i++)
//            {
//                double x = 10000 + i * 100;
//                double y = 10000 + (i % 10) * 150;
//                points.Add(new Point3d(x, y, 0));
//            }
//            return points;
//        }


//    }
//}
