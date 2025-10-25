using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.BreakCurvesCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// BreakCurves 命令 - 在交点处打断曲线（优化版�?
    /// 
    /// 功能�?
    /// 1. 选择曲线（LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE�?
    /// 2. Polyline 先分解为 Line �?
    /// 3. 计算所有交�?
    /// 4. 在交点处分割曲线
    /// 5. 删除原曲线，创建新曲线段
    /// 
    /// 优化�?
    /// - Domain 层：CurveIntersectionService（平台无关的交点计算逻辑�?
    /// - Infrastructure 层：CurveSegmentService（AutoCAD 特定的曲线操作）
    /// - 测试命令：C13（由 Recall.cs 动态调用，支持热重启）
    /// </summary>
    public class BreakCurvesCommand
    {
        private readonly CurveIntersectionService _intersectionService;
        private readonly CurveSegmentService _segmentService;

        public BreakCurvesCommand()
        {
            _intersectionService = ServiceLocator.Resolve<CurveIntersectionService>();
            _segmentService = ServiceLocator.Resolve<CurveSegmentService>();
        }

        [CommandMethod("HYBC")]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 提示用户选择曲线
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的曲线："
            };

            // 设置过滤器，选择曲线对象
            SelectionFilter filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,ARC,CIRCLE,LWPOLYLINE,POLYLINE,SPLINE")
            });

            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

            // 获取选择的对象集合
            long selectionTime = totalStopwatch.ElapsedMilliseconds;
            ObjectId[] selectedObjects = selRes.Value.GetObjectIds();
            ed.WriteMessage($"\n已选择 {selectedObjects.Length} 个对象");

            // 开始事务
            long transStartTime = totalStopwatch.ElapsedMilliseconds;
            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
            {
                long transCreatedTime = totalStopwatch.ElapsedMilliseconds;
                phaseStopwatch.Restart();
                // 存储所有曲线对象（使用整数索引而非 ObjectId�?
                Dictionary<int, Curve> curveObjects = new Dictionary<int, Curve>();
                
                // 存储每条曲线的交点参数列�?
                Dictionary<int, List<double>> curveIntersections = new Dictionary<int, List<double>>();
                
                // 存储需要删除的原始对象
                List<ObjectId> objectsToDelete = new List<ObjectId>();

                int curveIndex = 0;

                // 第一步：处理所有选中对象
                foreach (ObjectId objId in selectedObjects)
                {
                    Entity ent = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                    
                    if (ent is Polyline pline)
                    {
                        // Polyline：使用服务分�?
                        var segments = _segmentService.ExplodePolyline(pline);
                        foreach (var seg in segments)
                        {
                            curveObjects[curveIndex] = seg;
                            var data = _intersectionService.CreateIntersectionData(
                                curveIndex, seg.StartParam, seg.EndParam);
                            curveIntersections[curveIndex] = data.Parameters;
                            curveIndex++;
                        }
                        objectsToDelete.Add(objId);
                    }
                    else if (ent is Polyline2d pline2d)
                    {
                        // Polyline2d：使用服务分�?
                        var segments = _segmentService.ExplodePolyline2d(pline2d, trans);
                        foreach (var seg in segments)
                        {
                            curveObjects[curveIndex] = seg;
                            var data = _intersectionService.CreateIntersectionData(
                                curveIndex, seg.StartParam, seg.EndParam);
                            curveIntersections[curveIndex] = data.Parameters;
                            curveIndex++;
                        }
                        objectsToDelete.Add(objId);
                    }
                    else if (ent is Circle circle)
                    {
                        curveObjects[curveIndex] = circle;
                        var data = _intersectionService.CreateIntersectionData(
                            curveIndex, circle.StartParam, circle.EndParam);
                        curveIntersections[curveIndex] = data.Parameters;
                        curveIndex++;
                        objectsToDelete.Add(objId);
                    }
                    else if (ent is Curve curve)
                    {
                        curveObjects[curveIndex] = curve;
                        var data = _intersectionService.CreateIntersectionData(
                            curveIndex, curve.StartParam, curve.EndParam);
                        curveIntersections[curveIndex] = data.Parameters;
                        curveIndex++;
                        objectsToDelete.Add(objId);
                    }
                }

                if (curveObjects.Count == 0)
                {
                    ed.WriteMessage("\n没有找到有效的曲线。");
                    return;
                }

                long phase1Time = phaseStopwatch.ElapsedMilliseconds;
                ed.WriteMessage($"\n[阶段1] 处理对象和分解多段线：{phase1Time} 毫秒（{curveObjects.Count} 条曲线）");

                // 第二步：计算所有曲线对之间的交点（使用空间索引优化）
                phaseStopwatch.Restart();
            var curveIds = curveObjects.Keys.ToList();

                // 构建空间索引（参考 HYOV 的方法）
                var gridSize = 100.0; // 网格大小
                var spatialIndex = BuildSpatialIndex(curveObjects, gridSize);

                // 使用并行计算优化（参考 HYOV）
                var lockObj = new object();
            int totalIntersections = 0;

                System.Threading.Tasks.Parallel.For(0, curveIds.Count, i =>
                {
                    int curveId1 = curveIds[i];
                    Curve curve1 = curveObjects[curveId1];

                    // 获取附近的曲线（使用空间索引）
                    var nearbyIndices = GetNearbyCurveIndices(curve1, spatialIndex, gridSize, curveObjects);

                    foreach (int curveId2 in nearbyIndices)
                    {
                        if (curveId2 <= curveId1) continue; // 避免重复检测

                        Curve curve2 = curveObjects[curveId2];

                        // 使用服务计算交点
                        Point3dCollection intersectionPoints = _segmentService.CalculateIntersections(curve1, curve2);

                    if (intersectionPoints.Count > 0)
                        {
                            lock (lockObj)
                    {
                        totalIntersections += intersectionPoints.Count;
                            }

                        foreach (Point3d intersectionPoint in intersectionPoints)
                        {
                            // 检查交点是否在曲线1上
                                if (_segmentService.IsPointOnCurve(curve1, intersectionPoint))
                            {
                                    double param1 = _segmentService.GetParameterAtPoint(curve1, intersectionPoint);
                                if (!double.IsNaN(param1))
                                    {
                                        lock (curveIntersections[curveId1])
                                        {
                                            curveIntersections[curveId1].Add(param1);
                                        }
                                    }
                            }

                            // 检查交点是否在曲线2上
                                if (_segmentService.IsPointOnCurve(curve2, intersectionPoint))
                            {
                                    double param2 = _segmentService.GetParameterAtPoint(curve2, intersectionPoint);
                                if (!double.IsNaN(param2))
                                    {
                                        lock (curveIntersections[curveId2])
                                        {
                                            curveIntersections[curveId2].Add(param2);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                long phase2Time = phaseStopwatch.ElapsedMilliseconds;
                int totalIntersectionParams = curveIntersections.Values.Sum(list => list.Count);
                ed.WriteMessage($"\n[阶段2] 计算交点：{phase2Time} 毫秒（{totalIntersectionParams} 个交点参数）");

                // 第三步：分割曲线（使用服务）
                phaseStopwatch.Restart();
                List<Curve> newCurves = new List<Curve>();

                foreach (var item in curveIntersections)
                {
                    int curveId = item.Key;
                    Curve curve = curveObjects[curveId];

                    if (item.Value.Count > 0)
                    {
                        // 特殊处理：Circle（圆）- 直接使用交点分割
                        if (curve is Circle circle)
                        {
                            // 收集所有交点
                            Point3dCollection splitPoints = new Point3dCollection();
                            foreach (double param in item.Value)
                            {
                                Point3d pt = circle.GetPointAtParameter(param);
                                splitPoints.Add(pt);
                            }

                            // 使用 GetSplitCurves 直接分割圆
                            try
                            {
                                DBObjectCollection segments = circle.GetSplitCurves(splitPoints);
                                foreach (DBObject obj in segments)
                                {
                                    if (obj is Curve seg)
                                    {
                                        newCurves.Add(seg);
                                    }
                                }
                            }
                            catch
                            {
                                // 分割失败，保留原圆
                                Curve clonedCurve = curve.Clone() as Curve;
                                if (clonedCurve != null)
                                    newCurves.Add(clonedCurve);
                            }
                        }
                        else
                        {
                            // 其他曲线：使用参数段方式
                            var intersectionData = _intersectionService.CreateIntersectionData(
                                curveId, curve.StartParam, curve.EndParam);
                            intersectionData.Parameters = item.Value;

                            // 计算分割参数
                            var splitParams = _intersectionService.CalculateSplitParameters(intersectionData);
                            
                            // 生成参数段
                            var paramSegments = _intersectionService.GenerateParameterSegments(splitParams);

                        // 创建新的曲线段
                        List<Curve> segments = new List<Curve>();
                            foreach (var paramSeg in paramSegments)
                            {
                                Curve segment = _segmentService.SplitCurveSegment(
                                    curve, paramSeg.StartParam, paramSeg.EndParam);
                                if (segment != null)
                                {
                                    segments.Add(segment);
                            }
                        }

                        if (segments.Count > 0)
                        {
                            newCurves.AddRange(segments);
                        }
                        else
                        {
                            // 分割全部失败，保留原曲线
                            Curve clonedCurve = curve.Clone() as Curve;
                            if (clonedCurve != null)
                                newCurves.Add(clonedCurve);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有交点，保留原曲线
                        Curve newCurve = curve.Clone() as Curve;
                        if (newCurve != null)
                            newCurves.Add(newCurve);
                    }
                }

                long phase3Time = phaseStopwatch.ElapsedMilliseconds;
                ed.WriteMessage($"\n[阶段3] 分割曲线：{phase3Time} 毫秒（生成 {newCurves.Count} 条新曲线）");

                // 第四步：更新图纸
                phaseStopwatch.Restart();
                BlockTable bt = trans.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 添加新曲线段
                foreach (Curve newCurve in newCurves)
                {
                    btr.AppendEntity(newCurve);
                    trans.AddNewlyCreatedDBObject(newCurve, true);
                }

                // 删除原始对象
                foreach (ObjectId id in objectsToDelete)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                }

                long phase4Time = phaseStopwatch.ElapsedMilliseconds;
                ed.WriteMessage($"\n[阶段4] 更新图纸：{phase4Time} 毫秒");

                phaseStopwatch.Restart();
                trans.Commit();
                long commitTime = phaseStopwatch.ElapsedMilliseconds;
                ed.WriteMessage($"\n[阶段5] 提交事务：{commitTime} 毫秒");

                totalStopwatch.Stop();
                
                // 详细时间分析
                long transCreateTime = transCreatedTime - transStartTime;
                long knownTime = phase1Time + phase2Time + phase3Time + phase4Time + commitTime;
                long totalTime = totalStopwatch.ElapsedMilliseconds;
                long unknownTime = totalTime - knownTime - selectionTime - transCreateTime;
                
                ed.WriteMessage($"\n处理完成：{objectsToDelete.Count} → {newCurves.Count} 曲线");
                ed.WriteMessage($"\n总时间：{totalTime} 毫秒");
                ed.WriteMessage($"\n用户选择：{selectionTime} 毫秒");
                ed.WriteMessage($"\n事务创建：{transCreateTime} 毫秒");
                ed.WriteMessage($"\n已知阶段：{knownTime} 毫秒");
                ed.WriteMessage($"\n其他开销：{unknownTime} 毫秒");
            }
        }

        #region 空间索引辅助方法（参考 HYOV）

        /// <summary>
        /// 构建空间网格索引
        /// </summary>
        private Dictionary<(long, long), List<int>> BuildSpatialIndex(Dictionary<int, Curve> curves, double gridSize)
        {
            var index = new Dictionary<(long, long), List<int>>();

            foreach (var kvp in curves)
            {
                int curveId = kvp.Key;
                Curve curve = kvp.Value;

                // 获取曲线的边界框
                var extents = curve.GeometricExtents;
                double minX = extents.MinPoint.X;
                double minY = extents.MinPoint.Y;
                double maxX = extents.MaxPoint.X;
                double maxY = extents.MaxPoint.Y;

                // 计算曲线占据的网格范围
                long gridMinX = (long)Math.Floor(minX / gridSize);
                long gridMinY = (long)Math.Floor(minY / gridSize);
                long gridMaxX = (long)Math.Floor(maxX / gridSize);
                long gridMaxY = (long)Math.Floor(maxY / gridSize);

                // 将曲线添加到所有相关网格
                for (long gx = gridMinX; gx <= gridMaxX; gx++)
                {
                    for (long gy = gridMinY; gy <= gridMaxY; gy++)
                    {
                        var gridKey = (gx, gy);
                        if (!index.ContainsKey(gridKey))
                        {
                            index[gridKey] = new List<int>();
                        }
                        index[gridKey].Add(curveId);
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// 获取附近的曲线索引
        /// </summary>
        private HashSet<int> GetNearbyCurveIndices(
            Curve curve,
            Dictionary<(long, long), List<int>> spatialIndex,
            double gridSize,
            Dictionary<int, Curve> allCurves)
        {
            var nearbyIndices = new HashSet<int>();

            // 获取曲线的边界框
            var extents = curve.GeometricExtents;
            double minX = extents.MinPoint.X;
            double minY = extents.MinPoint.Y;
            double maxX = extents.MaxPoint.X;
            double maxY = extents.MaxPoint.Y;

            // 扩展搜索范围（包括相邻网格）
            long gridMinX = (long)Math.Floor(minX / gridSize) - 1;
            long gridMinY = (long)Math.Floor(minY / gridSize) - 1;
            long gridMaxX = (long)Math.Floor(maxX / gridSize) + 1;
            long gridMaxY = (long)Math.Floor(maxY / gridSize) + 1;

            // 收集所有相关网格中的曲线
            for (long gx = gridMinX; gx <= gridMaxX; gx++)
            {
                for (long gy = gridMinY; gy <= gridMaxY; gy++)
                {
                    var gridKey = (gx, gy);
                    if (spatialIndex.ContainsKey(gridKey))
                    {
                        foreach (int curveId in spatialIndex[gridKey])
                        {
                            nearbyIndices.Add(curveId);
                        }
                    }
                }
            }

            return nearbyIndices;
        }

        #endregion
    }
}
