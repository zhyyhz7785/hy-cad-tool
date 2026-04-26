using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Shared.Geometry.Algorithms;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.App.Bootstrap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Misc
{
    public class BreakCurvesCommand
    {
        private const double PARAM_TOLERANCE = 1e-9;
        private const double POINT_DUPLICATE_TOLERANCE = 1e-6;
        private const int SMALL_DATASET_THRESHOLD = 100;

        private readonly CurveIntersectionService _intersectionService;
        private readonly CurveSegmentService _segmentService;
        private readonly SpatialIndexService<Curve> _spatialIndexService;

        public BreakCurvesCommand()
        {
            _intersectionService = ServiceLocator.Resolve<CurveIntersectionService>();
            _segmentService = ServiceLocator.Resolve<CurveSegmentService>();
            _spatialIndexService = ServiceLocator.Resolve<SpatialIndexService<Curve>>();
        }

        public void Execute()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            try
            {
                var selectedObjects = SelectCurves(ed);
                if (selectedObjects == null || selectedObjects.Length == 0)
            {
                ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

                int inputCount = selectedObjects.Length;
                var stopwatch = Stopwatch.StartNew();

                List<Curve> newCurves;
                List<ObjectId> objectsToDelete;

            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
                {
                    var (curveObjects, curveIntersections, toDelete) = ProcessObjects(selectedObjects, trans);
                    objectsToDelete = toDelete;

                    if (curveObjects.Count > 0)
                    {
                        var (spatialIndex, gridSize) = BuildSpatialIndex(curveObjects);
                        CalculateIntersections(curveObjects, curveIntersections, spatialIndex, gridSize);
                        newCurves = SplitCurves(curveObjects, curveIntersections);
                    }
                    else
                    {
                        newCurves = new List<Curve>();
                    }

                    trans.Abort();
                }

                stopwatch.Stop();
                ed.WriteMessage($"\n处理完成：{inputCount} → {newCurves.Count} 曲线");

                using (doc.LockDocument())
                using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)trans.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var curve in newCurves)
                    {
                        btr.AppendEntity(curve);
                        trans.AddNewlyCreatedDBObject(curve, true);
                    }

                    foreach (var id in objectsToDelete)
                    {
                        var ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                        ent?.Erase();
                    }

                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }

        private ObjectId[] SelectCurves(Editor ed)
        {
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,ARC,CIRCLE,ELLIPSE,LWPOLYLINE,POLYLINE,SPLINE")
            });

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n请选择要处理的曲线：" }, filter);
            return selRes.Status == PromptStatus.OK ? selRes.Value.GetObjectIds() : null;
        }

        private (Dictionary<int, Curve>, Dictionary<int, List<double>>, List<ObjectId>) ProcessObjects(
            ObjectId[] selectedObjects, Transaction trans)
        {
            var curveObjects = new Dictionary<int, Curve>();
            var curveIntersections = new Dictionary<int, List<double>>();
            var objectsToDelete = new List<ObjectId>();
            int curveIndex = 0;

            foreach (var objId in selectedObjects)
            {
                var ent = trans.GetObject(objId, OpenMode.ForRead) as Entity;

                if (ent is Polyline pline)
                {
                    foreach (var seg in _segmentService.ExplodePolyline(pline))
                    {
                        curveObjects[curveIndex] = seg;
                        curveIntersections[curveIndex] = new List<double>();
                        curveIndex++;
                    }
                    objectsToDelete.Add(objId);
                }
                else if (ent is Polyline2d pline2d)
                {
                    foreach (var seg in _segmentService.ExplodePolyline2d(pline2d, trans))
                    {
                        curveObjects[curveIndex] = seg;
                        curveIntersections[curveIndex] = new List<double>();
                        curveIndex++;
                    }
                    objectsToDelete.Add(objId);
                }
                else if (ent is Curve curve)
                {
                    curveObjects[curveIndex] = curve;
                    curveIntersections[curveIndex] = new List<double>();
                    curveIndex++;
                    objectsToDelete.Add(objId);
                }
            }

            return (curveObjects, curveIntersections, objectsToDelete);
        }

        private (Dictionary<(long, long), List<int>>, double) BuildSpatialIndex(Dictionary<int, Curve> curveObjects)
        {
            double gridSize = CalculateOptimalGridSize(curveObjects.Values);

            if (curveObjects.Count < SMALL_DATASET_THRESHOLD)
            {
                return (new Dictionary<(long, long), List<int>> { [(0, 0)] = Enumerable.Range(0, curveObjects.Count).ToList() }, gridSize);
            }

            var index = _spatialIndexService.BuildIndex(
                curveObjects.Values.ToList(),
                curve =>
                {
                    var ext = curve.GeometricExtents;
                    return (ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
                },
                gridSize);

            return (index, gridSize);
        }

        private void CalculateIntersections(Dictionary<int, Curve> curveObjects,
            Dictionary<int, List<double>> curveIntersections,
            Dictionary<(long, long), List<int>> spatialIndex, double gridSize)
        {
            var curveIds = curveObjects.Keys.ToList();
            bool useSimpleMode = curveObjects.Count < SMALL_DATASET_THRESHOLD;

            for (int i = 0; i < curveIds.Count; i++)
            {
                int id1 = curveIds[i];
                Curve curve1 = curveObjects[id1];

                var nearbyIndices = useSimpleMode
                    ? new HashSet<int>(curveIds)
                    : _spatialIndexService.GetNearbyIndices(
                        (curve1.GeometricExtents.MinPoint.X, curve1.GeometricExtents.MinPoint.Y,
                         curve1.GeometricExtents.MaxPoint.X, curve1.GeometricExtents.MaxPoint.Y),
                        spatialIndex, gridSize, gridSize);

                foreach (int id2 in nearbyIndices)
                {
                    if (id2 <= id1) continue;

                    var points = _segmentService.CalculateIntersections(curve1, curveObjects[id2]);
                    if (points.Count == 0) continue;

                    foreach (Point3d pt in points)
                    {
                        try
                        {
                            var closestPt1 = curve1.GetClosestPointTo(pt, false);
                            curveIntersections[id1].Add(curve1.GetParameterAtPoint(closestPt1));
                        }
                        catch { }

                        try
                        {
                            var closestPt2 = curveObjects[id2].GetClosestPointTo(pt, false);
                            curveIntersections[id2].Add(curveObjects[id2].GetParameterAtPoint(closestPt2));
                        }
                        catch { }
                    }
                }
            }
        }

        private List<Curve> SplitCurves(Dictionary<int, Curve> curveObjects,
            Dictionary<int, List<double>> curveIntersections)
        {
            var newCurves = new List<Curve>();

            foreach (var item in curveIntersections)
            {
                var curve = curveObjects[item.Key];

                if (item.Value.Count > 0)
                {
                    if (curve is Circle || curve is Ellipse)
                        SplitClosedCurve(curve, item.Value, newCurves);
                    else
                        SplitOpenCurve(curve, item.Key, item.Value, newCurves);
                }
                else
                {
                    newCurves.Add(curve.Clone() as Curve);
                }
            }

            return newCurves;
        }

        private void SplitClosedCurve(Curve curve, List<double> parameters, List<Curve> newCurves)
        {
            try
            {
                var sortedParams = parameters.Distinct()
                    .Where(p => p >= curve.StartParam - PARAM_TOLERANCE && p <= curve.EndParam + PARAM_TOLERANCE)
                    .Select(p => Math.Max(curve.StartParam, Math.Min(curve.EndParam, p)))
                    .OrderBy(p => p).ToList();

                if (sortedParams.Count < 2)
                {
                    newCurves.Add(curve.Clone() as Curve);
                    return;
                }

                var splitPoints = new Point3dCollection();
                Point3d? lastPoint = null;

                foreach (double param in sortedParams)
                {
                    try
                    {
                        var pt = curve.GetPointAtParameter(param);
                        if (lastPoint.HasValue && pt.DistanceTo(lastPoint.Value) < POINT_DUPLICATE_TOLERANCE)
                            continue;
                        splitPoints.Add(pt);
                        lastPoint = pt;
                    }
                    catch { }
                }

                if (splitPoints.Count >= 2)
                {
                    var segments = curve.GetSplitCurves(splitPoints);
                    foreach (DBObject obj in segments)
                        if (obj is Curve seg) newCurves.Add(seg);
                }
                else
                {
                    newCurves.Add(curve.Clone() as Curve);
                }
            }
            catch
            {
                newCurves.Add(curve.Clone() as Curve);
            }
        }

        private void SplitOpenCurve(Curve curve, int curveId, List<double> parameters, List<Curve> newCurves)
        {
            try
            {
                var sortedParams = parameters.Distinct()
                    .Where(p => p >= curve.StartParam - PARAM_TOLERANCE && p <= curve.EndParam + PARAM_TOLERANCE)
                    .Select(p => Math.Max(curve.StartParam, Math.Min(curve.EndParam, p)))
                    .OrderBy(p => p).ToList();

                if (sortedParams.Count == 0)
                {
                    newCurves.Add(curve.Clone() as Curve);
                    return;
                }

                var data = _intersectionService.CreateIntersectionData(curveId, curve.StartParam, curve.EndParam);
                data.Parameters = sortedParams;

                var splitParams = _intersectionService.CalculateSplitParameters(data);
                var segments = _intersectionService.GenerateParameterSegments(splitParams);

                var curves = new List<Curve>();
                foreach (var seg in segments)
                {
                    try
                    {
                        var newSeg = _segmentService.SplitCurveSegment(curve, seg.StartParam, seg.EndParam);
                        if (newSeg != null) curves.Add(newSeg);
                    }
                    catch { }
                }

                if (curves.Count > 0)
                    newCurves.AddRange(curves);
                else
                    newCurves.Add(curve.Clone() as Curve);
            }
            catch
            {
                newCurves.Add(curve.Clone() as Curve);
            }
        }

        private double CalculateOptimalGridSize(IEnumerable<Curve> curves)
        {
            var curveList = curves.ToList();
            if (curveList.Count == 0) return 1000.0;

            double totalLength = 0;
            int count = 0;

            foreach (var curve in curveList)
            {
                try
                {
                    double length = curve.EndPoint.DistanceTo(curve.StartPoint);
                    if (length > 0)
                    {
                        totalLength += length;
                        count++;
                    }
                }
                catch { }
            }

            if (count == 0) return 1000.0;

            double avgLength = totalLength / count;
            return Math.Max(500, Math.Min(5000, avgLength * 4.0));
        }
    }
}
