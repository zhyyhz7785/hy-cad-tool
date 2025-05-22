using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static void PlacePileAndVoronoiWithReplacementRate()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                // 1. 获取基础多边形并转换为 NTS Polygon
                Polygon polygon = PromptAndGetPolygon();
                if (polygon == null)
                {
                    ed.WriteMessage("\n未能获取有效多边形，命令结束。");
                    return;
                }
                // 2. 输入桩直径和置换率
                double diameter = 400.0; // 默认桩直径400mm
                double replacementRate = 0.05; // 默认置换率5%
                PromptDoubleOptions pdoD = new PromptDoubleOptions("\n请输入桩直径(单位mm):");
                pdoD.DefaultValue = 400.0;
                var pdrD = ed.GetDouble(pdoD);
                if (pdrD.Status == PromptStatus.OK) diameter = pdrD.Value;
                PromptDoubleOptions pdoRate = new PromptDoubleOptions("\n请输入目标置换率 (0-1):");
                pdoRate.DefaultValue = 0.05;
                var pdrRate = ed.GetDouble(pdoRate);
                if (pdrRate.Status == PromptStatus.OK) replacementRate = pdrRate.Value;
                // 3. 计算桩的数量
                double pileArea = Math.PI * Math.Pow(diameter / 2.0, 2); // 单个桩的面积，单位mm²
                double totalArea = polygon.Area; // 多边形面积，单位mm²
                int numberOfPiles = (int)Math.Floor((totalArea * replacementRate) / pileArea); // 计算桩的数量
                                                                                               // 4. 生成多边形内部的随机点作为桩的位置
                List<Coordinate> pileCenters = GeneratePointsInsidePolygon(polygon, numberOfPiles);
                // 5. 构造 Voronoi 图
                VoronoiDiagramBuilder builder = new VoronoiDiagramBuilder();
                builder.SetSites(pileCenters);  // 设置生成点
                builder.ClipEnvelope = polygon.EnvelopeInternal;  // 限制边界
                GeometryCollection voronoiDiagram = builder.GetDiagram(new GeometryFactory());
                // 6. 在 AutoCAD 中绘制 Voronoi 区域和桩
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-桩", Color.FromColorIndex(ColorMethod.ByAci, 3));
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-Voronoi", Color.FromColorIndex(ColorMethod.ByAci, 1)); // Voronoi 区域图层
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 绘制 Voronoi 区域
                    foreach (Geometry cell in voronoiDiagram.Geometries)
                    {
                        // 裁剪 Voronoi 区域到基础多边形
                        Geometry intersection = cell.Intersection(polygon);
                        if (intersection is Polygon clippedPolygon)
                        {
                            // 将 Voronoi 区域绘制为多边形
                            List<Point2d> points = new List<Point2d>();
                            foreach (Coordinate coord in clippedPolygon.Coordinates)
                            {
                                points.Add(new Point2d(coord.X, coord.Y));
                            }
                            DrawPolygon(btr, tr, points, "00-HY-Voronoi");  // 绘制 Voronoi 区域
                        }
                    }
                    // 绘制桩
                    double radius = (diameter / 2.0); // 半径，单位mm
                    foreach (Coordinate center in pileCenters)
                    {
                        // 在每个生成点的位置绘制桩
                        using (Circle circle = new Circle(new Point3d(center.X, center.Y, 0), Vector3d.ZAxis, radius))
                        {
                            circle.Layer = "00-HY-桩";
                            btr.AppendEntity(circle);
                            tr.AddNewlyCreatedDBObject(circle, true);
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\n桩布置完成，共 {pileCenters.Count} 根桩，置换率: {replacementRate:P2}。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
