using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.Services.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.Entities;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.HyApplication.Services;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 基于表面的三维建模命令（新方法）
    /// 
    /// 命令名：C15
    /// 原理：
    /// 1. 上表面 = 多边形 @ 标高
    /// 2. 下表面 = 根据标高规则计算
    /// 3. 墙体 = 外侧扩张 300mm
    /// 4. 底板 = 内部区域，厚度 400mm
    /// 5. 0.000 平面 = 土壤分界线
    /// </summary>
    public class SurfaceBasedElevation3DCommand
    {
        private readonly PolygonAdapter _polygonAdapter;
        private readonly ISolid3DBuilder _solidBuilder;
        private readonly ILayerManager _layerManager;
        private readonly SlabGenerationService _slabService;
        private readonly WallGenerationService _wallService;
        
        private readonly double _scale = 1.0; // 比例
        
        public SurfaceBasedElevation3DCommand()
        {
            // 初始化服务
            _polygonAdapter = new PolygonAdapter(_scale);
            _solidBuilder = new Solid3DBuilder();
            _layerManager = new LayerManager();
            _slabService = new SlabGenerationService(_solidBuilder, _layerManager);
            
            var detector = new AdjacencyDetector(tolerance: 1.0);
            _wallService = new WallGenerationService(
                new HyApplication.Services.WallGeometryCalculator(),
                detector,
                editor: null);
        }
        
        [CommandMethod("C15")]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            
            // 性能计时器
            var totalStopwatch = Stopwatch.StartNew();
            var stepStopwatch = new Stopwatch();
            var timings = new Dictionary<string, long>();
            
            try
            {
                // 步骤1: 一次性选择多边形和文本
                stepStopwatch.Restart();
                var (polygons, elevationDict) = SelectPolygonsAndTexts(ed, db);
                stepStopwatch.Stop();
                timings["1-数据准备"] = stepStopwatch.ElapsedMilliseconds;
                
                if (polygons == null || polygons.Count == 0 || elevationDict == null || elevationDict.Count == 0)
                {
                    ed.WriteMessage("\n数据不足，命令终止。");
                    return;
                }
                
                // 步骤2: 构建三维模型
                stepStopwatch.Restart();
                var builder = new SurfaceBasedElevation3DBuilder(defaultSlabThickness: 400.0, defaultWallThickness: 300.0);
                var models = new List<SurfaceBasedModel>();
                foreach (var kvp in elevationDict)
                {
                    var polygon2D = _polygonAdapter.ConvertToPolygon2D(kvp.Key);
                    models.Add(builder.BuildModel(polygon2D, kvp.Value));
                }
                stepStopwatch.Stop();
                timings["2-构建模型"] = stepStopwatch.ElapsedMilliseconds;
                
                // 步骤3: 生成筏板（使用服务）
                stepStopwatch.Restart();
                int solidCount = _slabService.GenerateSlabs(db, models);
                stepStopwatch.Stop();
                timings["3-创建筏板"] = stepStopwatch.ElapsedMilliseconds;
                
                // 步骤4: 生成墙体（使用服务）
                stepStopwatch.Restart();
                int wallCount = _wallService.GenerateWallsSimple(db, models, _solidBuilder, _layerManager);
                stepStopwatch.Stop();
                timings["4-创建墙体"] = stepStopwatch.ElapsedMilliseconds;
                
                totalStopwatch.Stop();
                ed.WriteMessage($"\n完成：{solidCount} 筏板 + {wallCount} 墙体，耗时 {totalStopwatch.ElapsedMilliseconds}ms");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }
        
        private (List<Polyline>, Dictionary<Polyline, Elevation>) SelectPolygonsAndTexts(Editor ed, Database db)
        {
            // 一次性选择多边形和文本
            var options = new PromptSelectionOptions { MessageForAdding = "\n请选择基础多边形和标高文本：" };
            var selection = ed.GetSelection(options);
            if (selection.Status != PromptStatus.OK) return (null, null);
            
            var polygons = new List<Polyline>();
            var textObjectIds = new ObjectIdCollection();
            
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selectedObj in selection.Value)
                {
                    if (selectedObj == null) continue;
                    var entity = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead);
                    
                    if (entity is Polyline polyline && polyline.Closed)
                        polygons.Add(polyline);
                    else if (entity is DBText || entity is MText)
                        textObjectIds.Add(selectedObj.ObjectId);
                }
                
                if (polygons.Count == 0 || textObjectIds.Count == 0)
                {
                    tr.Commit();
                    return (null, null);
                }
                
                var extractor = new HyApplication.Services.ElevationDataExtractor(ed, silentMode: true);
                var elevationDict = extractor.ExtractElevationData(tr, polygons, textObjectIds);
                tr.Commit();
                
                return (polygons, elevationDict);
            }
        }
        
        // ✅ 所有业务逻辑已移至服务层：
        // - CreateSlabs → SlabGenerationService
        // - CreateWallsSimple → WallGenerationService.GenerateWallsSimple
        // - CreateSingleWall → Solid3DBuilder.CreateWallSolid
        // - EnsureLayerExists → LayerManager.EnsureLayer
    }
}

