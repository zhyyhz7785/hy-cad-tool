using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Services;
using HyCADTool.Shared.AutoCAD.Helpers;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace HyCADTool.Features.Pile
{
    /// <summary>
    /// 桩布置与 Voronoi 优化命令
    /// 移植自 HYz_PlacePileAndVoronoiWithLloydOptimization
    /// 功能：选择多边形，输入参数，使用 Lloyd 算法优化桩位，绘制 Voronoi 图
    /// </summary>
    public class PileVoronoiOptimizationCommand
    {
        private readonly Document _doc;
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly VoronoiOptimizationService _voronoiService;

        public PileVoronoiOptimizationCommand()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            _db = _doc.Database;
            _ed = _doc.Editor;
            _voronoiService = HyCADTool.App.Bootstrap.ServiceLocator.Resolve<VoronoiOptimizationService>();
        }

        /// <summary>
        /// 执行命令（自动生成随机初始点）
        /// </summary>
        public void Execute()
        {
            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    // 1. 获取基础多边形
                    Polygon polygon = PolylineHelper.PromptAndGetPolygon(_ed, tr);
                    if (polygon == null)
                    {
                        _ed.WriteMessage("\n未能获取有效多边形，命令结束。");
                        return;
                    }

                    // 2. 输入桩参数
                    var (diameter, replacementRate, numberOfPiles) = GetPileParameters(polygon);
                    _ed.WriteMessage($"\n对应置换率，需要的桩为 {numberOfPiles} 棵");

                    // 3. 生成随机初始点
                    List<Coordinate> pileCenters = _voronoiService.GenerateRandomPointsInsidePolygon(polygon, numberOfPiles);

                    // 4. 执行优化和绘制
                    ExecuteOptimizationAndDraw(polygon, pileCenters, diameter, replacementRate, tr);

                    tr.Commit();
                }

                _ed.WriteMessage("\n桩布置完成。");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行命令（使用已有圆作为初始点）
        /// </summary>
        public void ExecuteWithExistingCircles()
        {
            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    // 1. 获取基础多边形
                    Polygon polygon = PolylineHelper.PromptAndGetPolygon(_ed, tr);
                    if (polygon == null)
                    {
                        _ed.WriteMessage("\n未能获取有效多边形，命令结束。");
                        return;
                    }

                    // 2. 选择已有圆
                    List<Coordinate> pileCenters = GetSelectedCircleCenters(tr);
                    if (pileCenters == null || pileCenters.Count == 0)
                    {
                        _ed.WriteMessage("\n未选择任何圆，命令结束。");
                        return;
                    }

                    // 3. 过滤多边形内的点
                    pileCenters = _voronoiService.GetPointsInsidePolygon(polygon, pileCenters);
                    _ed.WriteMessage($"\n多边形内共有 {pileCenters.Count} 个圆");

                    // 4. 输入桩参数
                    var (diameter, replacementRate, _) = GetPileParameters(polygon, pileCenters.Count);

                    // 5. 执行优化和绘制
                    ExecuteOptimizationAndDraw(polygon, pileCenters, diameter, replacementRate, tr);

                    tr.Commit();
                }

                _ed.WriteMessage("\n桩布置完成。");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行 Lloyd 优化并绘制结果
        /// </summary>
        private void ExecuteOptimizationAndDraw(Polygon polygon, List<Coordinate> pileCenters, 
            double diameter, double replacementRate, Transaction tr)
        {
            // 1. Lloyd 优化
            pileCenters = _voronoiService.ApplyLloydOptimization(polygon, pileCenters, 4500, diameter);

            // 2. 构造 Voronoi 图
            var voronoiDiagram = _voronoiService.CreateVoronoiDiagram(polygon, pileCenters);

            // 3. 绘制
            BlockTable bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            CreateLayerIfNotExists(tr, "00-HY-桩", Color.FromColorIndex(ColorMethod.ByAci, 3));
            CreateLayerIfNotExists(tr, "00-HY-Voronoi", Color.FromColorIndex(ColorMethod.ByAci, 2));

            DrawVoronoiRegion(tr, btr, voronoiDiagram, polygon);
            DrawPiles(tr, btr, pileCenters, diameter);

            _ed.WriteMessage($"\n桩布置完成，共 {pileCenters.Count} 根桩，置换率: {replacementRate:P2}。");
        }

        /// <summary>
        /// 获取桩参数（带用户输入桩数量）
        /// </summary>
        private (double diameter, double replacementRate, int numberOfPiles) GetPileParameters(Polygon polygon, int? existingCount = null)
        {
            double diameter = 400.0;
            double replacementRate = 0.022734275;
            int numberOfPiles = existingCount ?? 0;

            if (!existingCount.HasValue)
            {
                // 获取桩数量
                PromptIntegerOptions pdoPiles = new PromptIntegerOptions("\n请输入桩的根数 (0表示自动计算):");
                pdoPiles.DefaultValue = 0;
                var pdrPiles = _ed.GetInteger(pdoPiles);

                if (pdrPiles.Status == PromptStatus.OK && pdrPiles.Value != 0)
                {
                    numberOfPiles = pdrPiles.Value;
                }
                else
                {
                    double pileArea = Math.PI * Math.Pow(diameter / 2.0, 2);
                    double totalArea = polygon.Area;
                    numberOfPiles = (int)Math.Ceiling((totalArea * replacementRate) / pileArea);
                    _ed.WriteMessage($"\n对应置换率，需要的桩为 {numberOfPiles} 棵");
                }
            }

            // 获取桩直径
            PromptDoubleOptions pdoD = new PromptDoubleOptions("\n请输入桩直径(单位mm):");
            pdoD.DefaultValue = 400.0;
            var pdrD = _ed.GetDouble(pdoD);
            if (pdrD.Status == PromptStatus.OK)
            {
                diameter = pdrD.Value;
            }

            // 获取目标置换率
            PromptDoubleOptions pdoRate = new PromptDoubleOptions("\n请输入目标置换率 (0-100)%:");
            pdoRate.DefaultValue = 2.2;
            var pdrRate = _ed.GetDouble(pdoRate);
            if (pdrRate.Status == PromptStatus.OK)
            {
                replacementRate = pdrRate.Value / 100;
            }

            return (diameter, replacementRate, numberOfPiles);
        }

        /// <summary>
        /// 获取选中圆的圆心
        /// </summary>
        private List<Coordinate> GetSelectedCircleCenters(Transaction tr)
        {
            TypedValue[] filter = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "CIRCLE")
            };
            SelectionFilter selectionFilter = new SelectionFilter(filter);

            PromptSelectionOptions selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择一些圆:"
            };
            PromptSelectionResult selectionResult = _ed.GetSelection(selectionOptions, selectionFilter);

            if (selectionResult.Status != PromptStatus.OK)
            {
                _ed.WriteMessage("\n未选择任何圆。");
                return new List<Coordinate>();
            }

            List<Coordinate> circleCenters = new List<Coordinate>();
            SelectionSet selectionSet = selectionResult.Value;

            foreach (SelectedObject selectedObj in selectionSet)
            {
                if (selectedObj != null)
                {
                    Circle circle = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Circle;
                    if (circle != null)
                    {
                        Coordinate center = new Coordinate(circle.Center.X, circle.Center.Y);
                        circleCenters.Add(center);
                    }
                }
            }

            return circleCenters;
        }

        /// <summary>
        /// 绘制 Voronoi 区域
        /// </summary>
        private void DrawVoronoiRegion(Transaction tr, BlockTableRecord btr, GeometryCollection voronoiDiagram, Polygon polygon)
        {
            foreach (NtsGeometry cell in voronoiDiagram)
            {
                NtsGeometry intersection = cell.Intersection(polygon);
                if (intersection is Polygon clippedPolygon)
                {
                    PolylineHelper.DrawPolygon(btr, tr, clippedPolygon, "00-HY-Voronoi");
                }
            }
        }

        /// <summary>
        /// 绘制桩（圆）
        /// </summary>
        private void DrawPiles(Transaction tr, BlockTableRecord btr, List<Coordinate> pileCenters, double diameter)
        {
            double radius = diameter / 2.0;
            foreach (Coordinate center in pileCenters)
            {
                using (Circle circle = new Circle(new Point3d(center.X, center.Y, 0), Vector3d.ZAxis, radius))
                {
                    circle.Layer = "00-HY-桩";
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                }
            }
        }

        /// <summary>
        /// 创建图层（如果不存在）
        /// </summary>
        private void CreateLayerIfNotExists(Transaction tr, string layerName, Color color)
        {
            LayerTable layerTable = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (!layerTable.Has(layerName))
            {
                LayerTableRecord layerTableRecord = new LayerTableRecord
                {
                    Name = layerName,
                    Color = color
                };
                layerTable.UpgradeOpen();
                layerTable.Add(layerTableRecord);
                tr.AddNewlyCreatedDBObject(layerTableRecord, true);
            }
        }
    }
}
