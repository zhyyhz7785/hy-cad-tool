using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Entities;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.Services.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Workflows
{
    /// <summary>
    /// 墙体生成服务
    /// 协调墙体生成流程：遍历边、判断类型、计算参数、创建实体
    /// </summary>
    public class WallGenerationService
    {
        private readonly WallGeometryCalculator _calculator;
        private readonly AdjacencyDetector _detector;
        private readonly Editor _editor;
        
        public WallGenerationService(
            WallGeometryCalculator calculator,
            AdjacencyDetector detector,
            Editor editor = null)
        {
            _calculator = calculator;
            _detector = detector;
            _editor = editor;
        }
        
        /// <summary>
        /// 生成所有墙体
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="models">所有模型</param>
        /// <param name="wallBuilder">墙体构建器</param>
        /// <returns>创建的墙体数量</returns>
        public int GenerateWalls(
            Transaction tr,
            List<SurfaceBasedModel> models,
            IWallBuilder wallBuilder)
        {
            int wallCount = 0;
            
            // 检测所有相邻边
            var adjacentEdgesMap = _detector.DetectAdjacentPolygons(models);
            
            _editor?.WriteMessage("\n开始创建墙体（逐边生成，相邻边开口）...");
            
            for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
            {
                var model = models[modelIndex];
                var polygon = model.TopSurface.Polygon;
                var currentElevation = model.TopSurface.Elevation;
                
                // 获取该模型的相邻边列表
                var adjacentEdges = adjacentEdgesMap.ContainsKey(modelIndex) 
                    ? adjacentEdgesMap[modelIndex] 
                    : new List<Line2D>();
                
                if (adjacentEdges.Count > 0)
                {
                    _editor?.WriteMessage($"\n[模型 {modelIndex + 1}] 多边形 {polygon.VertexCount} 边, 标高 {currentElevation.Value / 1000.0:F3}, 其中 {adjacentEdges.Count} 条为相邻边（将开口）");
                }
                
                // 遍历多边形的每条边
                var edges = GetPolygonEdges(polygon);
                
                foreach (var edge in edges)
                {
                    // 判断该边是否为相邻边
                    bool isAdjacentEdge = IsEdgeInList(edge, adjacentEdges);
                    
                    if (isAdjacentEdge)
                    {
                        // 相邻边：创建连接墙体
                        var adjacentModel = _detector.FindAdjacentModel(edge, models, modelIndex);
                        if (adjacentModel != null)
                        {
                            wallCount += CreateConnectingWall(
                                tr, 
                                edge, 
                                currentElevation, 
                                adjacentModel.TopSurface.Elevation,
                                model.BottomSurface.Elevation.Value,
                                wallBuilder);
                        }
                    }
                    else
                    {
                        // 非相邻边：如果需要，创建挡土墙
                        if (_calculator.NeedsRetainingWall(currentElevation))
                        {
                            wallCount += CreateRetainingWall(
                                tr,
                                edge,
                                currentElevation,
                                model.BottomSurface.Elevation.Value,
                                wallBuilder);
                        }
                    }
                }
            }
            
            _editor?.WriteMessage($"\n墙体创建完成：成功 {wallCount} 个");
            
            return wallCount;
        }
        
        /// <summary>
        /// 创建连接墙体
        /// </summary>
        private int CreateConnectingWall(
            Transaction tr,
            Line2D edge,
            Domain.ValueObjects.Elevation currentElevation,
            Domain.ValueObjects.Elevation adjacentElevation,
            double currentBottomElevation,
            IWallBuilder wallBuilder)
        {
            (double bottom, double top, int offsetDirection) = _calculator.CalculateConnectingWall(
                currentElevation,
                adjacentElevation);
            
            double height = _calculator.CalculateWallHeight(bottom, top);
            
            if (height <= 0)
            {
                return 0; // 无需创建墙体
            }
            
            _editor?.WriteMessage($"\n  [连接墙体] 当前标高={currentElevation.Value / 1000.0:F3}, 相邻标高={adjacentElevation.Value / 1000.0:F3}, 底={bottom:F0}mm, 顶={top:F0}mm, 高={height:F0}mm, 偏移={offsetDirection}");
            
            try
            {
                var wall = wallBuilder.CreateWall(
                    tr,
                    edge,
                    wallThickness: 100.0, // 默认墙厚100mm
                    wallHeight: height,
                    offsetDirection: offsetDirection,
                    baseElevation: bottom,
                    layerName: "00_hy_墙体3D_连接墙");
                
                return wall != null ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 创建挡土墙
        /// </summary>
        private int CreateRetainingWall(
            Transaction tr,
            Line2D edge,
            Domain.ValueObjects.Elevation baseElevation,
            double bottomElevation,
            IWallBuilder wallBuilder)
        {
            (double bottom, double top, int offsetDirection) = _calculator.CalculateRetainingWall(
                baseElevation,
                bottomElevation);
            
            double height = _calculator.CalculateWallHeight(bottom, top);
            
            if (height <= 0)
            {
                return 0;
            }
            
            try
            {
                var wall = wallBuilder.CreateWall(
                    tr,
                    edge,
                    wallThickness: 100.0,
                    wallHeight: height,
                    offsetDirection: offsetDirection,
                    baseElevation: bottom,
                    layerName: "00_hy_墙体3D_挡土墙");
                
                return wall != null ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 获取多边形的所有边
        /// </summary>
        private List<Line2D> GetPolygonEdges(Polygon2D polygon)
        {
            var edges = new List<Line2D>();
            
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                var v1 = polygon.Vertices[i];
                var v2 = polygon.Vertices[(i + 1) % polygon.VertexCount];
                edges.Add(new Line2D(v1, v2));
            }
            
            return edges;
        }
        
        /// <summary>
        /// 判断边是否在列表中
        /// </summary>
        private bool IsEdgeInList(Line2D edge, List<Line2D> edgeList)
        {
            foreach (var listEdge in edgeList)
            {
                if (GeometryUtils.AreEdgesEqual(edge, listEdge, tolerance: 1.0))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 生成墙体（简化版：矩形拉伸，不使用布尔运算）
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="models">模型列表</param>
        /// <param name="solidBuilder">3D 实体构建器</param>
        /// <param name="layerManager">图层管理器</param>
        /// <param name="wallThickness">墙体厚度（默认 100mm）</param>
        /// <param name="slabThickness">板厚（默认 400mm）</param>
        /// <returns>创建的墙体数量</returns>
        public int GenerateWallsSimple(
            Database db,
            List<SurfaceBasedModel> models,
            ISolid3DBuilder solidBuilder,
            ILayerManager layerManager,
            double wallThickness = 100.0,
            double slabThickness = 400.0)
        {
            int count = 0;
            
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var modelSpace = (BlockTableRecord)tr.GetObject(
                    db.CurrentSpaceId,
                    OpenMode.ForWrite);
                
                // 创建墙体图层
                layerManager.EnsureLayer(tr, "00_hy_墙体3D_挡土墙", 2);
                
                for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
                {
                    var model = models[modelIndex];
                    var topElevation = model.TopSurface.Elevation;
                    
                    // 只处理负标高的模型
                    if (topElevation.Value >= 0) continue;
                    
                    var bottomElevation = model.BottomSurface.Elevation;
                    var polygon = model.TopSurface.Polygon;
                    
                    // 遍历多边形的每条边
                    for (int i = 0; i < polygon.VertexCount; i++)
                    {
                        var v1 = polygon.Vertices[i];
                        var v2 = polygon.Vertices[(i + 1) % polygon.VertexCount];
                        var edge = new Line2D(v1, v2);
                        
                        // 检查是否有相邻模型
                        var adjacentModel = _detector.FindAdjacentModel(edge, models, modelIndex);
                        var outwardNormal = GeometryUtils.CalculateOutwardNormal(edge, polygon);
                        
                        try
                        {
                            Solid3d wallSolid;
                            
                            if (adjacentModel != null)
                            {
                                // 相邻墙体：连接两个不同标高
                                var adjElevation = adjacentModel.TopSurface.Elevation;
                                double wallBottom = Math.Min(topElevation.Value, adjElevation.Value) - slabThickness;
                                double wallTop = Math.Max(topElevation.Value, adjElevation.Value) - slabThickness;
                                int offsetDir = (topElevation.Value > adjElevation.Value) ? -1 : 1;
                                
                                wallSolid = solidBuilder.CreateWallSolid(
                                    v1, v2,
                                    wallBottom,
                                    wallTop - wallBottom,
                                    outwardNormal,
                                    wallThickness,
                                    offsetDir);
                            }
                            else
                            {
                                // 外边界墙体
                                wallSolid = solidBuilder.CreateWallSolid(
                                    v1, v2,
                                    bottomElevation.Value,
                                    -bottomElevation.Value,
                                    outwardNormal,
                                    wallThickness);
                            }
                            
                            modelSpace.AppendEntity(wallSolid);
                            tr.AddNewlyCreatedDBObject(wallSolid, true);
                            wallSolid.Layer = "00_hy_墙体3D_挡土墙";
                            count++;
                        }
                        catch
                        {
                            // 静默处理单个墙体创建失败
                        }
                    }
                }
                
                tr.Commit();
            }
            
            return count;
        }
    }
}

