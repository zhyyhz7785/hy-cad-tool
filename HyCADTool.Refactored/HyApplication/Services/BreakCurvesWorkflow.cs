using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.Services.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HyCADTool.Refactored.HyApplication.Services
{
    /// <summary>
    /// 打断曲线工作流 - 业务流程编排
    /// 负责：
    /// 1. 流程协调
    /// 2. 详细时间分析
    /// 3. 返回结构化结果
    /// </summary>
    public class BreakCurvesWorkflow
    {
        private readonly CurveIntersectionService _intersectionService;
        private readonly CurveSegmentService _segmentService;
        private readonly SpatialIndexService<Curve> _spatialIndexService;

        public BreakCurvesWorkflow(
            CurveIntersectionService intersectionService,
            CurveSegmentService segmentService,
            SpatialIndexService<Curve> spatialIndexService)
        {
            _intersectionService = intersectionService;
            _segmentService = segmentService;
            _spatialIndexService = spatialIndexService;
        }

        /// <summary>
        /// 执行打断曲线工作流
        /// </summary>
        /// <param name="selectedObjects">选中的对象ID列表</param>
        /// <param name="transaction">事务</param>
        /// <returns>打断曲线结果</returns>
        public BreakCurvesResult Execute(ObjectId[] selectedObjects, Transaction transaction)
        {
            var stopwatch = Stopwatch.StartNew();
            var phaseTimings = new Dictionary<string, long>();
            var allIntersectionPoints = new List<Point3d>();

            // 阶段 1: 处理对象和分解多段线
            var phase1Watch = Stopwatch.StartNew();
            var curveObjects = new Dictionary<int, Curve>();
            var curveIntersections = new Dictionary<int, List<double>>();
            var objectsToDelete = new List<ObjectId>();
            int curveIndex = 0;

            foreach (ObjectId objId in selectedObjects)
            {
                Entity ent = transaction.GetObject(objId, OpenMode.ForRead) as Entity;

                if (ent is Polyline pline)
                {
                    var segments = _segmentService.ExplodePolyline(pline);
                    foreach (var seg in segments)
                    {
                        curveObjects[curveIndex] = seg;
                        var data = _intersectionService.CreateIntersectionData(curveIndex, seg.StartParam, seg.EndParam);
                        curveIntersections[curveIndex] = data.Parameters;
                        curveIndex++;
                    }
                    objectsToDelete.Add(objId);
                }
                else if (ent is Polyline2d pline2d)
                {
                    var segments = _segmentService.ExplodePolyline2d(pline2d, transaction);
                    foreach (var seg in segments)
                    {
                        curveObjects[curveIndex] = seg;
                        var data = _intersectionService.CreateIntersectionData(curveIndex, seg.StartParam, seg.EndParam);
                        curveIntersections[curveIndex] = data.Parameters;
                        curveIndex++;
                    }
                    objectsToDelete.Add(objId);
                }
                else if (ent is Circle circle)
                {
                    curveObjects[curveIndex] = circle;
                    var data = _intersectionService.CreateIntersectionData(curveIndex, circle.StartParam, circle.EndParam);
                    curveIntersections[curveIndex] = data.Parameters;
                    curveIndex++;
                    objectsToDelete.Add(objId);
                }
                else if (ent is Curve curve)
                {
                    curveObjects[curveIndex] = curve;
                    var data = _intersectionService.CreateIntersectionData(curveIndex, curve.StartParam, curve.EndParam);
                    curveIntersections[curveIndex] = data.Parameters;
                    curveIndex++;
                    objectsToDelete.Add(objId);
                }
            }

            phaseTimings["1-处理对象和分解"] = phase1Watch.ElapsedMilliseconds;

            if (curveObjects.Count == 0)
            {
                return new BreakCurvesResult
                {
                    NewCurves = new List<Curve>(),
                    ObjectsToDelete = objectsToDelete,
                    IntersectionPoints = allIntersectionPoints,
                    PhaseTimings = phaseTimings,
                    TotalTime = stopwatch.ElapsedMilliseconds
                };
            }

            // 阶段 2: 构建空间索引
            var phase2aWatch = Stopwatch.StartNew();
            var gridSize = 100.0;
            var spatialIndex = _spatialIndexService.BuildIndex(
                curveObjects.Values.ToList(),
                curve =>
                {
                    var extents = curve.GeometricExtents;
                    return (extents.MinPoint.X, extents.MinPoint.Y, extents.MaxPoint.X, extents.MaxPoint.Y);
                },
                gridSize);
            phaseTimings["2a-空间索引构建"] = phase2aWatch.ElapsedMilliseconds;

            // 阶段 2b: 并行计算交点
            var phase2bWatch = Stopwatch.StartNew();
            var curveIds = curveObjects.Keys.ToList();
            var lockObj = new object();
            int totalIntersections = 0;

            System.Threading.Tasks.Parallel.For(0, curveIds.Count, i =>
            {
                int curveId1 = curveIds[i];
                Curve curve1 = curveObjects[curveId1];

                var bounds = curve1.GeometricExtents;
                var nearbyIndices = _spatialIndexService.GetNearbyIndices(
                    (bounds.MinPoint.X, bounds.MinPoint.Y, bounds.MaxPoint.X, bounds.MaxPoint.Y),
                    spatialIndex,
                    gridSize,
                    gridSize);

                foreach (int curveId2 in nearbyIndices)
                {
                    if (curveId2 <= curveId1) continue;

                    Curve curve2 = curveObjects[curveId2];
                    Point3dCollection intersectionPoints = _segmentService.CalculateIntersections(curve1, curve2);

                    if (intersectionPoints.Count > 0)
                    {
                        lock (lockObj)
                        {
                            totalIntersections += intersectionPoints.Count;
                        }

                        foreach (Point3d intersectionPoint in intersectionPoints)
                        {
                            // 收集所有交点用于标记
                            lock (allIntersectionPoints)
                            {
                                allIntersectionPoints.Add(intersectionPoint);
                            }

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

            phaseTimings["2b-并行计算交点"] = phase2bWatch.ElapsedMilliseconds;
            phaseTimings["INFO-找到交点数"] = totalIntersections;  // 这是数量，不是时间

            // 阶段 3: 分割曲线
            var phase3Watch = Stopwatch.StartNew();
            List<Curve> newCurves = new List<Curve>();

            foreach (var item in curveIntersections)
            {
                int curveId = item.Key;
                Curve curve = curveObjects[curveId];

                if (item.Value.Count > 0)
                {
                    if (curve is Circle circle)
                    {
                        Point3dCollection splitPoints = new Point3dCollection();
                        foreach (double param in item.Value)
                        {
                            Point3d pt = circle.GetPointAtParameter(param);
                            splitPoints.Add(pt);
                        }

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
                            Curve clonedCurve = curve.Clone() as Curve;
                            if (clonedCurve != null)
                                newCurves.Add(clonedCurve);
                        }
                    }
                    else
                    {
                        var intersectionData = _intersectionService.CreateIntersectionData(curveId, curve.StartParam, curve.EndParam);
                        intersectionData.Parameters = item.Value;

                        var splitParams = _intersectionService.CalculateSplitParameters(intersectionData);
                        var paramSegments = _intersectionService.GenerateParameterSegments(splitParams);

                        List<Curve> segments = new List<Curve>();
                        foreach (var paramSeg in paramSegments)
                        {
                            Curve segment = _segmentService.SplitCurveSegment(curve, paramSeg.StartParam, paramSeg.EndParam);
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
                            Curve clonedCurve = curve.Clone() as Curve;
                            if (clonedCurve != null)
                                newCurves.Add(clonedCurve);
                        }
                    }
                }
                else
                {
                    Curve newCurve = curve.Clone() as Curve;
                    if (newCurve != null)
                        newCurves.Add(newCurve);
                }
            }

            phaseTimings["3-分割曲线"] = phase3Watch.ElapsedMilliseconds;

            stopwatch.Stop();
            phaseTimings["总时间"] = stopwatch.ElapsedMilliseconds;

            return new BreakCurvesResult
            {
                NewCurves = newCurves,
                ObjectsToDelete = objectsToDelete,
                IntersectionPoints = allIntersectionPoints,
                PhaseTimings = phaseTimings,
                TotalTime = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// 打断曲线结果
    /// </summary>
    public class BreakCurvesResult
    {
        /// <summary>
        /// 新生成的曲线列表
        /// </summary>
        public List<Curve> NewCurves { get; set; }

        /// <summary>
        /// 需要删除的原始对象ID列表
        /// </summary>
        public List<ObjectId> ObjectsToDelete { get; set; }

        /// <summary>
        /// 所有交点（用于标记）
        /// </summary>
        public List<Point3d> IntersectionPoints { get; set; }

        /// <summary>
        /// 各阶段时间统计
        /// </summary>
        public Dictionary<string, long> PhaseTimings { get; set; }

        /// <summary>
        /// 总时间（毫秒）
        /// </summary>
        public long TotalTime { get; set; }
    }
}

