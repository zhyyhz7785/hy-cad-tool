//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD.Geometry;
//using NetTopologySuite.Geometries;
//using System;
//using System.Collections.Generic;
//using Autodesk.AutoCAD.Runtime;
//using Exception = System.Exception;
//namespace HyCADTool.Tools
//{
//    public static partial class EtGpt
//    {
//        [CommandMethod("PLACE_PILE_INNER")]
//        public static void PlacePileInner()
//        {
//            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            Database db = Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            try
//            {
//                // 1. 获取原始闭合多段线 -> 转为NTS多边形 polygon (单位m或mm，根据您项目而定)
//                Polygon polygon = db.SelectAEntity<Polyline>().ConvertPolylineToNTSPolygon();
//                if (polygon == null)
//                {
//                    ed.WriteMessage("\n未能获取有效多边形，命令结束。");
//                    return;
//                }
//                // 2. 用户输入桩直径D，以及安全倍数k(默认1.5)
//                double diameter = 1.0; // 先给个默认
//                double k = 1.5;
//                PromptDoubleOptions pdoD = new PromptDoubleOptions("\n请输入桩直径(单位m或mm):");
//                pdoD.DefaultValue = 1.0;
//                var pdrD = ed.GetDouble(pdoD);
//                if (pdrD.Status != PromptStatus.OK) return;
//                diameter = pdrD.Value;
//                PromptDoubleOptions pdoK = new PromptDoubleOptions("\n请输入安全倍数k(默认1.5):");
//                pdoK.DefaultValue = 1.5;
//                var pdrK = ed.GetDouble(pdoK);
//                if (pdrK.Status == PromptStatus.OK)
//                {
//                    k = pdrK.Value;
//                }
//                double offsetDist = k * diameter; // 向内偏移距离 = k * D
//                // 3. 对多边形做负缓冲(内缩), 得到 offsetPolygon
//                var offsetGeometry = polygon.Buffer(-offsetDist);
//                if (offsetGeometry.IsEmpty)
//                {
//                    ed.WriteMessage($"\n多边形内缩 {offsetDist} 后为空 => 边界太窄，无法布桩。");
//                    return;
//                }
//                // 若返回MultiPolygon，可自行遍历,这里仅示例取第一段:
//                Polygon offsetPoly = null;
//                if (offsetGeometry is Polygon singlePoly)
//                {
//                    offsetPoly = singlePoly;
//                }
//                else if (offsetGeometry is MultiPolygon multiPoly && multiPoly.NumGeometries > 0)
//                {
//                    offsetPoly = multiPoly.GetGeometryN(0) as Polygon;
//                }
//                if (offsetPoly == null)
//                {
//                    ed.WriteMessage("\n内缩后不是Polygon类型, 无法处理。");
//                    return;
//                }
//                // 4. 在 offsetPoly 内部生成桩中心点
//                List<Point2d> pileCenters = GeneratePileCenters(offsetPoly);
//                // 5. 绘制到AutoCAD图层 "00-HY-桩"
//                // 如果AutoCAD是mm而NTS单位是m，需要把坐标和半径乘1000
//                using (Transaction tr = doc.TransactionManager.StartTransaction())
//                {
//                    // (a) 创建/获取图层
//                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-桩", Color.FromColorIndex(ColorMethod.ByAci, 3));
//                    // (b) 在模型空间插入圆表示桩
//                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
//                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                    // 假设直径D同样是m => 则半径(米->mm):
//                    double radiusInCad = (diameter * 0.5) * 1000.0;
//                    foreach (var pc in pileCenters)
//                    {
//                        double cx_mm = pc.X * 1000.0;
//                        double cy_mm = pc.Y * 1000.0;
//                        using (Circle c = new Circle())
//                        {
//                            c.Center = new Point3d(cx_mm, cy_mm, 0);
//                            c.Radius = radiusInCad;
//                            c.Layer = "00-HY-桩";
//                            btr.AppendEntity(c);
//                            tr.AddNewlyCreatedDBObject(c, true);
//                        }
//                    }
//                    tr.Commit();
//                }
//                ed.WriteMessage($"\n桩布置完成，共 {pileCenters.Count} 根，图层 = 00-HY-桩。");
//            }
//            catch (Exception ex)
//            {
//                ed.WriteMessage($"\n错误: {ex.Message}");
//            }
//        }
//        /// <summary>
//        /// 示例: 在 offsetPoly 内规则网格采样, 生成桩中心
//        /// 可改用四叉树/自适应网格
//        /// </summary>
//        private static List<Point2d> GeneratePileCenters(Polygon offsetPoly)
//        {
//            List<Point2d> result = new List<Point2d>();
//            var env = offsetPoly.EnvelopeInternal;
//            double step = 2.0; // 网格步长(单位m)
//            var gf = new GeometryFactory();
//            for (double y = env.MinY; y <= env.MaxY; y += step)
//            {
//                for (double x = env.MinX; x <= env.MaxX; x += step)
//                {
//                    var pt = gf.CreatePoint(new Coordinate(x, y));
//                    if (offsetPoly.Contains(pt))
//                    {
//                        result.Add(new Point2d(x, y));
//                    }
//                }
//            }
//            return result;
//        }
//        /// <summary>
//        /// 创建图层(若不存在)
//        /// </summary>
//        private static void CreateLayerIfNotExists(Transaction tr, Database db, string layerName, Color color)
//        {
//            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
//            if (!lt.Has(layerName))
//            {
//                lt.UpgradeOpen();
//                LayerTableRecord ltr = new LayerTableRecord();
//                ltr.Name = layerName;
//                ltr.Color = color;
//                lt.Add(ltr);
//                tr.AddNewlyCreatedDBObject(ltr, true);
//                lt.DowngradeOpen();
//            }
//        }
//        /// <summary>
//        /// 从AutoCAD中获取闭合多段线, 转为NTS Polygon (略)
//        /// </summary>
//        private static Polygon PromptAndGetPolygon()
//        {
//            // ... 省略 
//            return null;
//        }
//    }
//}
