using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Colors;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.Elevation3DCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// HY3_ELEVATION3D 命令（重构版本）
    /// 从平面基础配筋图和标高数据生成三维基础模型
    /// </summary>
    public class Elevation3DCommand
    {
        // Domain服务
        private readonly GeometryAnalyzer _geometryAnalyzer;
        private readonly WallBufferGenerator _wallBufferGenerator;
        private readonly WallConnectionHandler _wallConnectionHandler;
        private readonly PolygonMerger _polygonMerger;
        private readonly Elevation3DCalculator _elevation3DCalculator;
        
        // Infrastructure适配器
        private readonly Geometry3DBuilder _geometry3DBuilder;
        private readonly PolygonAdapter _polygonAdapter;
        
        // 配置参数
        private WallThickness _defaultWallThickness;
        private SlabThickness _raftThickness;
        private SlabThickness _baseThickness;
        
        public Elevation3DCommand()
        {
            // 初始化Domain服务
            _geometryAnalyzer = new GeometryAnalyzer();
            _wallBufferGenerator = new WallBufferGenerator();
            _wallConnectionHandler = new WallConnectionHandler();
            _polygonMerger = new PolygonMerger();
            _elevation3DCalculator = new Elevation3DCalculator();
            
            // 初始化Infrastructure适配器
            _geometry3DBuilder = new Geometry3DBuilder(scale: 1.0);
            _polygonAdapter = new PolygonAdapter(scale: 1.0);
            
            // 默认配置（与原代码保持一致）
            _defaultWallThickness = WallThickness.Create(350); // 350mm
            _raftThickness = SlabThickness.Create(500);        // 500mm
            _baseThickness = SlabThickness.Create(500);        // 500mm
        }
        
        /// <summary>
        /// HY3 命令入口（保持原命令名称）
        /// </summary>
        [CommandMethod("HY3")]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ed.WriteMessage("\n=== HY3 三维基础模型生成（重构版 v3.9 - 正值标高筏板延伸到地面） ===");
                
                // 步骤1: 选择基础多边形
                var polygons = SelectBasePolygons(ed, db);
                if (polygons == null || polygons.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何基础多边形，命令终止。");
                    return;
                }
                ed.WriteMessage($"\n已选择 {polygons.Count} 个基础多边形");
                
                // 步骤2: 提取标高数据
                var elevationDict = ExtractElevationData(ed, db, polygons);
                if (elevationDict == null || elevationDict.Count == 0)
                {
                    ed.WriteMessage("\n未找到标高数据，命令终止。");
                    return;
                }
                ed.WriteMessage($"\n找到 {elevationDict.Count} 个标高数据");
                
                // 步骤3: 分析几何边界条件
                var geometryDataList = AnalyzeGeometry(polygons, elevationDict);
                ed.WriteMessage($"\n几何分析完成，识别到 {geometryDataList.Count} 个几何区域");
                
                // 调试：显示每个区域的详细信息
                for (int i = 0; i < geometryDataList.Count; i++)
                {
                    var gd = geometryDataList[i];
                    ed.WriteMessage($"\n  区域 {i + 1}: 顶点数={gd.Polygon.VertexCount}, 标高={gd.Elevation.Value:F2}m, 墙体数={gd.WallsOnly.Count()}");
                }
                
                // 步骤4: 计算3D参数
                var walls = _elevation3DCalculator.CalculateAllWalls(geometryDataList);
                var rafts = _elevation3DCalculator.CalculateAllRafts(geometryDataList, _raftThickness);
                
                ed.WriteMessage($"\n计算完成：墙体 {walls.Count} 个，筏板 {rafts.Count} 个");
                
                // 步骤5: 生成3D Solid并添加到图纸
                int wallCount = CreateAndAddWalls(doc, db, walls);
                int raftCount = CreateAndAddSlabs(doc, db, rafts, "00_hy_筏板3D");
                
                stopwatch.Stop();
                
                ed.WriteMessage($"\n生成完成：{wallCount} 个墙体，{raftCount} 个筏板");
                ed.WriteMessage($"\n总耗时：{stopwatch.ElapsedMilliseconds} 毫秒");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 步骤1: 选择基础多边形
        /// </summary>
        private List<Polyline> SelectBasePolygons(Editor ed, Database db)
        {
            var result = new List<Polyline>();
            int outerCount = 0;
            int innerCount = 0;
            
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择基础多边形（支持 dcelOuter 和 dcelInner 图层）："
            };
            
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            
            var selection = ed.GetSelection(options, filter);
            
            if (selection.Status != PromptStatus.OK)
                return null;
            
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selectedObj in selection.Value)
                {
                    if (selectedObj != null)
                    {
                        var polyline = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Polyline;
                        if (polyline != null && polyline.Closed)
                        {
                            result.Add(polyline);
                            
                            // 统计图层信息
                            if (polyline.Layer == "dcelOuter")
                                outerCount++;
                            else if (polyline.Layer == "dcelInner")
                                innerCount++;
                        }
                    }
                }
                tr.Commit();
            }
            
            ed.WriteMessage($"\n已选择 {result.Count} 个基础多边形（dcelOuter: {outerCount}, dcelInner: {innerCount}）");
            return result;
        }
        
        /// <summary>
        /// 步骤2: 提取标高数据
        /// </summary>
        private Dictionary<Polyline, Elevation> ExtractElevationData(
            Editor ed,
            Database db,
            List<Polyline> polygons)
        {
            var elevationDict = new Dictionary<Polyline, Elevation>();
            
            // 选择标高文本
            var textOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择标高文本："
            };
            
            var textFilter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "TEXT")
            });
            
            var textSelection = ed.GetSelection(textOptions, textFilter);
            
            if (textSelection.Status != PromptStatus.OK)
                return elevationDict;
            
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 提取所有文本的标高值
                var elevationTexts = new List<(DBText text, Elevation elevation)>();
                
                foreach (SelectedObject selectedObj in textSelection.Value)
                {
                    if (selectedObj != null)
                    {
                        var text = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as DBText;
                        if (text != null)
                        {
                            if (Elevation.TryParse(text.TextString, out var elevation))
                            {
                                elevationTexts.Add((text, elevation));
                            }
                        }
                    }
                }
                
                // 为每个多边形匹配最近的标高文本
                foreach (var polyline in polygons)
                {
                    var polygon2D = _polygonAdapter.ConvertToPolygon2D(polyline);
                    var centroid = polygon2D.GetCentroid();
                    
                    // 查找包含在多边形内或最近的标高文本
                    var nearestText = elevationTexts
                        .OrderBy(t => centroid.DistanceTo(new Domain.ValueObjects.Geometry.Point2D(
                            t.text.Position.X,
                            t.text.Position.Y)))
                        .FirstOrDefault();
                    
                    if (nearestText != default)
                    {
                        elevationDict[polyline] = nearestText.elevation;
                    }
                }
                
                tr.Commit();
            }
            
            return elevationDict;
        }
        
        /// <summary>
        /// 步骤3: 分析几何边界条件
        /// </summary>
        private List<GeometryData> AnalyzeGeometry(
            List<Polyline> polygons,
            Dictionary<Polyline, Elevation> elevationDict)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            
            // 转换为Polygon2D和标高字典
            var polygon2DList = polygons.Select(p => _polygonAdapter.ConvertToPolygon2D(p)).ToList();
            var polygon2DToElevation = new Dictionary<Polygon2D, Elevation>();
            var polylineToPolygon2D = new Dictionary<Polyline, Polygon2D>();
            
            foreach (var kvp in elevationDict)
            {
                var polygon2D = _polygonAdapter.ConvertToPolygon2D(kvp.Key);
                polygon2DToElevation[polygon2D] = kvp.Value;
                polylineToPolygon2D[kvp.Key] = polygon2D;
            }
            
            // 识别外轮廓：按图层名称 "dcelOuter"
            var outerContours = new List<Polygon2D>();
            var innerPolygons = new List<Polygon2D>();
            
            foreach (var polyline in polygons)
            {
                var polygon2D = polylineToPolygon2D.ContainsKey(polyline) 
                    ? polylineToPolygon2D[polyline] 
                    : _polygonAdapter.ConvertToPolygon2D(polyline);
                
                if (polyline.Layer == "dcelOuter")
                {
                    outerContours.Add(polygon2D);
                    ed.WriteMessage($"\n[外轮廓] 顶点数={polygon2D.VertexCount}, 面积={polygon2D.GetArea():F2}");
                }
                else if (polyline.Layer == "dcelInner")
                {
                    innerPolygons.Add(polygon2D);
                    ed.WriteMessage($"\n[内部区域] 顶点数={polygon2D.VertexCount}, 面积={polygon2D.GetArea():F2}");
                }
            }
            
            ed.WriteMessage($"\n识别结果：外轮廓 {outerContours.Count} 个，内部区域 {innerPolygons.Count} 个");
            ed.WriteMessage($"\n只为内部区域生成筏板，外轮廓不生成筏板");
            
            var geometryDataList = new List<GeometryData>();
            
            foreach (var kvp in polygon2DToElevation)
            {
                var polygon2D = kvp.Key;
                var elevation = kvp.Value;
                
                // 跳过外轮廓（不生成筏板）
                if (outerContours.Contains(polygon2D))
                {
                    ed.WriteMessage($"\n跳过外轮廓（顶点数={polygon2D.VertexCount}），不生成筏板");
                    continue;
                }
                
                // 只处理内部区域
                if (!innerPolygons.Contains(polygon2D))
                {
                    ed.WriteMessage($"\n警告：多边形（顶点数={polygon2D.VertexCount}）不在 dcelOuter 或 dcelInner 图层，跳过");
                    continue;
                }
                
                // 分析边界条件（使用外轮廓作为土壤边界）
                var boundaryConditions = _geometryAnalyzer.AnalyzeBoundaryConditions(
                    polygon2D,
                    elevation,
                    outerContours,
                    polygon2DToElevation);
                
                // 从边界条件创建 WallData 列表
                var allWalls = new List<WallData>();
                foreach (var boundary in boundaryConditions)
                {
                    // 计算 outerElevation：
                    // 1. 如果有相邻多边形，使用相邻标高
                    // 2. 如果是土壤边界（外轮廓），使用 0（地面标高）
                    // 3. 否则使用 innerElevation
                    Elevation outerElevation;
                    if (boundary.AdjacentElevation != null)
                    {
                        outerElevation = boundary.AdjacentElevation;
                    }
                    else if (boundary.IsSoilBoundary)
                    {
                        outerElevation = Elevation.FromMeters(0.0); // 土壤边界默认地面标高
                    }
                    else
                    {
                        outerElevation = elevation; // 无相邻信息，使用自身标高
                    }
                    
                    var wallData = WallData.Create(
                        edge: boundary.Edge,
                        innerElevation: elevation,
                        outerElevation: outerElevation,
                        thickness: _defaultWallThickness,
                        boundary: boundary);
                    
                    allWalls.Add(wallData);
                }
                
                // 创建GeometryData
                var geometryData = GeometryData.Create(
                    polygon2D,
                    elevation,
                    allWalls,
                    _raftThickness);
                
                // 分类墙体段
                _geometryAnalyzer.ClassifyWallSegments(geometryData);
                
                geometryDataList.Add(geometryData);
            }
            
            return geometryDataList;
        }
        
        /// <summary>
        /// 步骤5: 创建并添加墙体
        /// </summary>
        private int CreateAndAddWalls(
            Document doc,
            Database db,
            List<Domain.Entities.Wall3D> walls)
        {
            int count = 0;
            
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 确保图层存在
                EnsureLayerExists(tr, db, "00_hy_墙体3D");
                EnsureLayerExists(tr, db, "00_hy_挡土墙3D");
                
                foreach (var wall in walls)
                {
                    try
                    {
                        var solid = _geometry3DBuilder.CreateWallSolid(wall);
                        
                        // 先添加到数据库
                        modelSpace.AppendEntity(solid);
                        tr.AddNewlyCreatedDBObject(solid, true);
                        
                        // 然后设置图层（必须在添加到数据库之后）
                        solid.Layer = wall.IsRetainingWall ? "00_hy_挡土墙3D" : "00_hy_墙体3D";
                        
                        count++;
                    }
                    catch (System.Exception ex)
                    {
                        doc.Editor.WriteMessage($"\n创建墙体失败：{ex.Message}");
                    }
                }
                
                tr.Commit();
            }
            
            return count;
        }
        
        /// <summary>
        /// 步骤5: 创建并添加筏板
        /// </summary>
        private int CreateAndAddSlabs(
            Document doc,
            Database db,
            List<Domain.Entities.Slab3D> slabs,
            string layerName)
        {
            int count = 0;
            
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 确保图层存在（蓝色，便于观察）
                EnsureLayerExists(tr, db, layerName, colorIndex: 5);
                
                doc.Editor.WriteMessage($"\n[DEBUG] 准备创建 {slabs.Count} 个筏板");
                
                foreach (var slab in slabs)
                {
                    try
                    {
                        doc.Editor.WriteMessage($"\n[DEBUG] 筏板 {count + 1}: 顶点数={slab.Region.VertexCount}, 底标高={slab.BottomElevation.Value:F2}, 顶标高={slab.TopElevation.Value:F2}");
                        
                        var solid = _geometry3DBuilder.CreateSlabSolid(slab);
                        solid.Layer = layerName;
                        
                        modelSpace.AppendEntity(solid);
                        tr.AddNewlyCreatedDBObject(solid, true);
                        count++;
                        
                        doc.Editor.WriteMessage($"\n[DEBUG] 筏板 {count} 创建成功");
                    }
                    catch (System.Exception ex)
                    {
                        doc.Editor.WriteMessage($"\n创建筏板失败：{ex.Message}");
                    }
                }
                
                tr.Commit();
            }
            
            return count;
        }
        
        /// <summary>
        /// 确保图层存在
        /// </summary>
        private void EnsureLayerExists(Transaction tr, Database db, string layerName, short colorIndex = 7)
        {
            var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            
            if (!layerTable.Has(layerName))
            {
                // 图层不存在，创建新图层
                layerTable.UpgradeOpen();
                var layerTableRecord = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };
                layerTable.Add(layerTableRecord);
                tr.AddNewlyCreatedDBObject(layerTableRecord, true);
            }
            else
            {
                // 图层已存在，更新颜色
                var layerId = layerTable[layerName];
                var layerTableRecord = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;
                layerTableRecord.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            }
        }
    }
}

