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
    public static partial class HyTool
    {
        public static void PlacePileAndVoronoi()
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
                // 2. 输入桩直径和生成点（桩的位置）
                double diameter = 400.0; // 默认桩直径400mm
                List<Coordinate> pileCenters = GeneratePointsInsidePolygon(polygon, 4); // 生成4个点
                                                                                        // 3. 构造 Voronoi 图
                VoronoiDiagramBuilder builder = new VoronoiDiagramBuilder();
                builder.SetSites(pileCenters);  // 设置生成点
                builder.ClipEnvelope = polygon.EnvelopeInternal;  // 限制边界
                GeometryCollection voronoiDiagram = builder.GetDiagram(new GeometryFactory());
                // 4. 在 AutoCAD 中绘制 Voronoi 区域和桩
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
                ed.WriteMessage("\n桩和Voronoi区域绘制完成。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
