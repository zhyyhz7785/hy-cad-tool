using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static void PlacePileAndVoronoiWithLloydOptimization()
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
                var (diameter, replacementRate, numberOfPiles) = GetPileParametersAndCount1(polygon);
                ed.WriteMessage($"\n对应置换率，需要的桩为{numberOfPiles}棵");
                // 4. 生成多边形内部的随机点作为桩的位置
                List<Coordinate> pileCenters = GeneratePointsInsidePolygon(polygon, numberOfPiles);
                // 5. 使用 Lloyd 算法优化桩的位置
                pileCenters = ApplyLloydOptimization(polygon, pileCenters, 4500, diameter); // 进行10轮迭代优化桩的位置
                                                                                           // 6. 构造 Voronoi 图
                var voronoiDiagram = CreateVoronoiDiagram(polygon, pileCenters);
                // 7. 在 AutoCAD 中绘制 Voronoi 区域和桩
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-桩", Color.FromColorIndex(ColorMethod.ByAci, 3));
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-Voronoi", Color.FromColorIndex(ColorMethod.ByAci, 2)); // Voronoi 区域图层
                    // 绘制 Voronoi 区域
                    DrawVoronoiRegion(tr, btr, voronoiDiagram, polygon);
                    DrawPiles(tr, btr, pileCenters, diameter);
                    tr.Commit();
                }
                ed.WriteMessage($"\n桩布置完成，共 {pileCenters.Count} 根桩，置换率: {replacementRate:P2}。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
        public static void PlacePileAndVoronoiWithLloydOptimization(List<Coordinate> pileCenters)
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
                var (diameter, replacementRate, numberOfPiles) = GetPileParametersAndCount(polygon);
                ed.WriteMessage($"\n对应置换率，需要的桩为{numberOfPiles}棵");
                // 5. 使用 Lloyd 算法优化桩的位置
                pileCenters = ApplyLloydOptimization(polygon, pileCenters, 500, diameter); // 进行10轮迭代优化桩的位置
                                                                                           // 6. 构造 Voronoi 图
                var voronoiDiagram = CreateVoronoiDiagram(polygon, pileCenters);
                // 7. 在 AutoCAD 中绘制 Voronoi 区域和桩
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-桩", Color.FromColorIndex(ColorMethod.ByAci, 3));
                    CreateLayerIfNotExists(tr, doc.Database, "00-HY-Voronoi", Color.FromColorIndex(ColorMethod.ByAci, 2)); // Voronoi 区域图层
                    // 绘制 Voronoi 区域
                    DrawVoronoiRegion(tr, btr, voronoiDiagram, polygon);
                    DrawPiles(tr, btr, pileCenters, diameter);
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
