using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Enums;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shell.Contracts;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shell.ViewModels;
using HyCADTool.Shell.Configuration;
using Autodesk.AutoCAD.EditorInput;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DimensionForReinforcement
{
    /// <summary>
    /// 配筋尺寸标注服务（迁移自旧代码 DimensionForReinforcement）
    /// 对多段线边界生成内外尺寸标注
    /// </summary>
    public class DimensionForReinforcementService
    {
        #region 配置参数

        private Polyline _boundary;
        private double _step = 20;
        private double _startStep = 10;
        private double _toleranceDouble;
        private double _dimensionDistanceOutside;
        private double _dimensionDistanceInside;
        private double _dimensionDistanceWithDim;
        private double _dimDistanceTolerance;
        private bool _isHasTwoDimOutside = true;

        // 边界极值点
        private Point3d _xMin, _xMax, _yMin, _yMax;

        // 割线生成的交线数据
        private Line[,] _lineIntersectionsUpDown;
        private Line[,] _lineIntersectionsLeftRight;

        #endregion

        #region 公共入口

        /// <summary>
        /// 对单个多段线生成尺寸标注并写入模型空间（单事务批量写入）。
        /// </summary>
        /// <returns>是否成功生成并写入至少一个标注</returns>
        public bool GenerateDimension(ObjectId boundaryId, bool writeSummary = true)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var boundary = tr.GetObject(boundaryId, OpenMode.ForRead) as Polyline;
                if (boundary == null)
                {
                    if (writeSummary)
                        ed.WriteMessage("\n未找到有效多段线。");
                    return false;
                }

                var output = new List<RotatedDimension>();
                if (!TryCollectDimensions(boundary, ed, output))
                    return false;

                TrySetCurrentDimensionLayer(ed);

                var btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var dim in output)
                {
                    btr.AppendEntity(dim);
                    tr.AddNewlyCreatedDBObject(dim, true);
                }

                tr.Commit();
                if (writeSummary && output.Count > 0)
                    ed.WriteMessage($"\n标注完成：{output.Count} 个尺寸。");
                return output.Count > 0;
            }
        }

        /// <summary>
        /// 对已在打开事务中的多段线生成尺寸标注（单事务批量写入）。
        /// </summary>
        public bool GenerateDimension(Polyline boundary)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var output = new List<RotatedDimension>();
                if (!TryCollectDimensions(boundary, ed, output))
                    return false;

                TrySetCurrentDimensionLayer(ed);

                var btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var dim in output)
                {
                    btr.AppendEntity(dim);
                    tr.AddNewlyCreatedDBObject(dim, true);
                }

                tr.Commit();
                if (output.Count > 0)
                    ed.WriteMessage($"\n标注完成：{output.Count} 个尺寸。");
                return output.Count > 0;
            }
        }

        private bool TryCollectDimensions(Polyline boundary, Editor ed, List<RotatedDimension> output)
        {
            if (!SetProperties(boundary, ed)) return false;

            var dimsLR = GenerateOutsideLeftRight(ed);
            if (dimsLR == null) return false;
            var dimsUD = GenerateOutsideUpDown(ed);
            if (dimsUD == null) return false;

            var dimInLR = GenerateInsideLeftRight();
            var dimInUD = GenerateInsideUpDown();

            dimInLR = DeleteNearbyParallelDim(dimInLR, _dimDistanceTolerance);
            dimInUD = DeleteNearbyParallelDim(dimInUD, _dimDistanceTolerance);

            var dimLeft = DimVsEqualLength(DeleteDimensionZero(dimsLR[0]));
            var dimRight = DimVsEqualLength(DeleteDimensionZero(dimsLR[1]));
            var dimUp = DimVsEqualLength(DeleteDimensionZero(dimsUD[0]));
            var dimDown = DimVsEqualLength(DeleteDimensionZero(dimsUD[1]));

            CollectDimensions(dimLeft, output);
            CollectDimensions(dimRight, output);
            CollectDimensions(dimUp, output);
            CollectDimensions(dimDown, output);
            CollectDimensions(dimInLR, output);
            CollectDimensions(dimInUD, output);
            return true;
        }

        private static void CollectDimensions(RotatedDimension[] dims, List<RotatedDimension> output)
        {
            if (dims == null) return;
            foreach (var d in dims)
            {
                if (d != null)
                    output.Add(d);
            }
        }

        #endregion

        #region 初始化

        private bool SetProperties(Polyline boundary, Editor ed)
        {
            if (boundary == null) return false;
            _boundary = boundary;

            // 从 SettingsPanelViewModel 读取参数（面板值 × Scale = 实际 mm）
            var vm = SettingsPanelViewModel.Current;
            double scale = ScaleResolver.GetScale();

            _dimensionDistanceOutside = (vm?.DimensionDistanceOutside ?? 14.0) * scale;
            _dimensionDistanceInside = (vm?.DimensionDistanceInside ?? 6.0) * scale;
            _dimensionDistanceWithDim = (vm?.DimensionDistanceWithDim ?? 6.0) * scale;
            _dimDistanceTolerance = (vm?.DimDistanceTolerance ?? 30.0) * scale;
            _toleranceDouble = 1e-2;
            _isHasTwoDimOutside = true;
            _step = 20;
            _startStep = 10;

            // 炸开多段线，获取极值点
            var lines = ExplodePolyline(boundary);
            try
            {
                if (lines.Length == 0)
                {
                    ed.WriteMessage("\n边界无有效直线段（弧段/退化多段线），无法标注。");
                    return false;
                }

                var points = GetAllLinePoints(lines);
                if (points.Length == 0)
                {
                    ed.WriteMessage("\n边界无有效顶点，无法标注。");
                    return false;
                }

                _xMin = GetExtreme(points, ExtremeSide.XMin);
                _xMax = GetExtreme(points, ExtremeSide.XMax);
                _yMin = GetExtreme(points, ExtremeSide.YMin);
                _yMax = GetExtreme(points, ExtremeSide.YMax);
            }
            finally
            {
                foreach (var line in lines)
                    line?.Dispose();
            }

            return true;
        }

        private static bool TrySetCurrentDimensionLayer(Editor ed)
        {
            try
            {
                var layerName = UserLayerNameResolver.Get(
                    LayerSemanticIds.CommonDimOuter, LayerBuiltinDefaults.CommonDimOuter);
                var layerService = ServiceLocator.Resolve<ILayerService>();
                layerService.SetCurrentLayer(layerName);
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n警告：标注图层设置失败（{ex.Message}），将使用当前图层。");
                return false;
            }
        }

        #endregion

        #region 外部标注生成

        /// <summary>返回 [leftDims, rightDims]</summary>
        private RotatedDimension[][] GenerateOutsideLeftRight(Editor ed)
        {
            // 1. 水平割线 → 上下扫描
            if (!GetAllSecantLinesHorizontal(ed)) return null;
            var cols = _lineIntersectionsUpDown.GetLength(1);
            if (cols < 2)
            {
                ed.WriteMessage("\n割线未产生有效交线列，跳过外部左右标注。");
                return new[] { new RotatedDimension[0], new RotatedDimension[0] };
            }

            // 2. 提取左/右边界线
            var boundLeftLines = GetColumn(_lineIntersectionsUpDown, 0);
            boundLeftLines = SortLineSEPointForDim(boundLeftLines);
            var boundRightLines = GetColumn(_lineIntersectionsUpDown, cols - 1);
            boundRightLines = SortLineSEPointForDim(boundRightLines);

            // 3. 生成标注点 → 标注
            var pointsLeft = GetDimensionPoints(boundLeftLines, DimensionFor.ForLeft);
            var dimsLeft = CreateOutsideDimensions(pointsLeft, DimensionFor.ForLeft);

            var pointsRight = GetDimensionPoints(boundRightLines, DimensionFor.ForRight);
            var dimsRight = CreateOutsideDimensions(pointsRight, DimensionFor.ForRight);

            return new[] { dimsLeft, dimsRight };
        }

        /// <summary>返回 [downDims, upDims]</summary>
        private RotatedDimension[][] GenerateOutsideUpDown(Editor ed)
        {
            // 1. 垂直割线 → 左右扫描
            if (!GetAllSecantLinesVertical(ed)) return null;
            var cols = _lineIntersectionsLeftRight.GetLength(1);
            if (cols < 2)
            {
                ed.WriteMessage("\n割线未产生有效交线列，跳过外部上下标注。");
                return new[] { new RotatedDimension[0], new RotatedDimension[0] };
            }

            // 2. 提取下/上边界线（都是取列，与旧代码 GetDimensionLeftRight/GetDimensionByCol 一致）
            var boundDownLines = GetColumn(_lineIntersectionsLeftRight, 0);
            boundDownLines = SortLineSEPointForDim(boundDownLines);
            var boundUpLines = GetColumn(_lineIntersectionsLeftRight, cols - 1);
            boundUpLines = SortLineSEPointForDim(boundUpLines);

            // 3. 生成标注
            var pointsDown = GetDimensionPoints(boundDownLines, DimensionFor.ForDown);
            var dimsDown = CreateOutsideDimensions(pointsDown, DimensionFor.ForDown);

            var pointsUp = GetDimensionPoints(boundUpLines, DimensionFor.ForUp);
            var dimsUp = CreateOutsideDimensions(pointsUp, DimensionFor.ForUp);

            return new[] { dimsDown, dimsUp };
        }

        private RotatedDimension[] CreateOutsideDimensions(Point3d[] points, DimensionFor direction)
        {
            if (points == null || points.Length < 2) return new RotatedDimension[0];

            var bounds = GetPointsBounds(points);
            double angle = 0;
            var dims = new List<RotatedDimension>();
            var dimLinePoint = new Point3d();
            var totalDimLinePoint = new Point3d();

            for (int i = 0; i < points.Length - 1; i++)
            {
                var basePoint = new Point3d();
                double distance;

                switch (direction)
                {
                    case DimensionFor.ForLeft:
                        basePoint = new Point3d(bounds[0], bounds[2], 0); // xmin, ymin
                        angle = Math.PI / 2;
                        distance = Math.Abs(basePoint.X - points[i].X);
                        dimLinePoint = points[i] - new Vector3d(distance + _dimensionDistanceOutside, 0, 0);
                        totalDimLinePoint = basePoint - new Vector3d(_dimensionDistanceWithDim + _dimensionDistanceOutside, 0, 0);
                        break;
                    case DimensionFor.ForRight:
                        basePoint = new Point3d(bounds[1], bounds[2], 0); // xmax, ymin
                        angle = Math.PI / 2;
                        distance = Math.Abs(basePoint.X - points[i].X);
                        dimLinePoint = points[i] + new Vector3d(distance + _dimensionDistanceOutside, 0, 0);
                        totalDimLinePoint = basePoint + new Vector3d(_dimensionDistanceWithDim + _dimensionDistanceOutside, 0, 0);
                        break;
                    case DimensionFor.ForUp:
                        basePoint = new Point3d(bounds[0], bounds[3], 0); // xmin, ymax
                        angle = 0;
                        distance = Math.Abs(basePoint.Y - points[i].Y);
                        dimLinePoint = points[i] + new Vector3d(0, distance + _dimensionDistanceOutside, 0);
                        totalDimLinePoint = basePoint + new Vector3d(0, _dimensionDistanceWithDim + _dimensionDistanceOutside, 0);
                        break;
                    case DimensionFor.ForDown:
                        basePoint = new Point3d(bounds[0], bounds[2], 0); // xmin, ymin
                        angle = 0;
                        distance = Math.Abs(basePoint.Y - points[i].Y);
                        dimLinePoint = points[i] - new Vector3d(0, distance + _dimensionDistanceOutside, 0);
                        totalDimLinePoint = basePoint - new Vector3d(0, _dimensionDistanceWithDim + _dimensionDistanceOutside, 0);
                        break;
                }

                dims.Add(new RotatedDimension
                {
                    XLine1Point = points[i],
                    XLine2Point = points[i + 1],
                    DimLinePoint = dimLinePoint,
                    Rotation = angle
                });
            }

            // 外部总标注
            if (_isHasTwoDimOutside && dims.Count > 0)
            {
                dims.Add(new RotatedDimension
                {
                    XLine1Point = dims.First().XLine1Point,
                    XLine2Point = dims.Last().XLine2Point,
                    DimLinePoint = totalDimLinePoint,
                    Rotation = angle
                });
            }

            return dims.ToArray();
        }

        #endregion

        #region 内部标注生成

        private RotatedDimension[] GenerateInsideLeftRight()
        {
            var cols = _lineIntersectionsUpDown.GetLength(1);
            if (cols < 3) return new RotatedDimension[0];

            var boundLinePri = GetColumn(_lineIntersectionsUpDown, 0);
            double priRightX = GetEntitiesBoundValue(boundLinePri, ExtremeSide.XMax);
            var dimIns = new List<RotatedDimension>();

            for (int i = 1; i < cols - 1; i++)
            {
                var boundLineMid = GetColumn(_lineIntersectionsUpDown, i);
                double midLeftX = GetEntitiesBoundValue(boundLineMid, ExtremeSide.XMin);
                double midRightX = GetEntitiesBoundValue(boundLineMid, ExtremeSide.XMax);

                var boundLineNext = GetColumn(_lineIntersectionsUpDown, i + 1);
                double nextLeftX = GetEntitiesBoundValue(boundLineNext, ExtremeSide.XMin);

                DimensionFor side = (midLeftX - priRightX > nextLeftX - midRightX)
                    ? DimensionFor.ForLeft
                    : DimensionFor.ForRight;

                var midDims = GetDimsByLines(boundLineMid, _dimensionDistanceInside, side, true);
                dimIns.AddRange(midDims);
                priRightX = midRightX;
            }

            return CleanInsideDims(dimIns);
        }

        private RotatedDimension[] GenerateInsideUpDown()
        {
            var cols = _lineIntersectionsLeftRight.GetLength(1);
            if (cols < 3) return new RotatedDimension[0];

            var boundLinePri = GetColumn(_lineIntersectionsLeftRight, 0);
            double priTopY = GetEntitiesBoundValue(boundLinePri, ExtremeSide.YMax);
            var dimIns = new List<RotatedDimension>();

            for (int i = 1; i < cols - 1; i++)
            {
                var boundLineMid = GetColumn(_lineIntersectionsLeftRight, i);
                double midBottomY = GetEntitiesBoundValue(boundLineMid, ExtremeSide.YMin);
                double midTopY = GetEntitiesBoundValue(boundLineMid, ExtremeSide.YMax);

                var boundLineNext = GetColumn(_lineIntersectionsLeftRight, i + 1);
                double nextBottomY = GetEntitiesBoundValue(boundLineNext, ExtremeSide.YMin);

                DimensionFor side = (midBottomY - priTopY > nextBottomY - midTopY)
                    ? DimensionFor.ForDown
                    : DimensionFor.ForUp;

                var midDims = GetDimsByLines(boundLineMid, _dimensionDistanceInside, side, true);
                dimIns.AddRange(midDims);
                priTopY = midTopY;
            }

            return CleanInsideDims(dimIns);
        }

        private RotatedDimension[] CleanInsideDims(List<RotatedDimension> dims)
        {
            var result = DeleteSamePointDim(dims);
            result = DeleteDimensionZero(result.ToArray()).ToList();
            result = result.GroupBy(x => x, new DimEqualityComparer())
                           .Select(g => g.Key)
                           .ToList();
            return result.ToArray();
        }

        #endregion

        #region 割线算法（水平 — 上下扫描）

        private bool GetAllSecantLinesHorizontal(Editor ed)
        {
            var lineIntersectionsList = new List<Line[]>();
            var secant = CreateHorizontalSecant(_yMin.Y + _startStep);

            while (secant.EndPoint.Y <= _yMax.Y)
            {
                var intersectionPts = GetIntersectionPointsNTS(secant, _boundary);
                var intersectionLines = GetIntersectionLines(_boundary, intersectionPts);
                intersectionLines = SortLinesByX(intersectionLines);
                lineIntersectionsList.Add(intersectionLines);

                NormalizeDirectionY(intersectionLines);

                var shortest = intersectionLines.OrderBy(l => l.EndPoint.Y).FirstOrDefault();
                var next = GetNextHorizontalSecant(shortest, secant, ed);
                secant.Dispose();
                if (next == null)
                    return false;
                secant = next;
            }

            secant.Dispose();
            _lineIntersectionsUpDown = To2DArray(lineIntersectionsList);
            return true;
        }

        private Line CreateHorizontalSecant(double y)
        {
            return new Line(new Point3d(_xMin.X, y, 0), new Point3d(_xMax.X, y, 0));
        }

        private Line GetNextHorizontalSecant(Line shortestIntersection, Line currentSecant, Editor ed)
        {
            if (currentSecant == null || _boundary == null)
                throw new System.ArgumentNullException("currentSecant or boundary cannot be null.");

            double y = currentSecant.EndPoint.Y;

            if (shortestIntersection == null)
            {
                return CreateHorizontalSecant(y + _step / 5.0);
            }

            int countPri = GetIntersectionCountAcad(currentSecant, _boundary);
            int countNext = countPri;
            Line next = null;
            int safetyCounter = 0;

            while (countPri == countNext && y <= shortestIntersection.EndPoint.Y && safetyCounter < 1000)
            {
                y += _step;
                next?.Dispose();
                next = CreateHorizontalSecant(y);
                try { countNext = GetIntersectionCountAcad(next, _boundary); }
                catch
                {
                    next?.Dispose();
                    ed.WriteMessage("\n水平割线求交失败，中止标注。");
                    return null;
                }
                safetyCounter++;
            }

            if (safetyCounter >= 1000)
            {
                next?.Dispose();
                y = Math.Max(_yMax.Y + _step, shortestIntersection.EndPoint.Y + _step);
                next = CreateHorizontalSecant(y);
            }

            return next;
        }

        #endregion

        #region 割线算法（垂直 — 左右扫描）

        private bool GetAllSecantLinesVertical(Editor ed)
        {
            var lineIntersectionsList = new List<Line[]>();
            var secant = CreateVerticalSecant(_xMin.X + _startStep);

            while (secant.EndPoint.X <= _xMax.X)
            {
                var intersectionPts = GetIntersectionPointsNTS(secant, _boundary);
                var intersectionLines = GetIntersectionLines(_boundary, intersectionPts);
                intersectionLines = SortLinesByY(intersectionLines);
                lineIntersectionsList.Add(intersectionLines);

                NormalizeDirectionX(intersectionLines);

                var shortest = intersectionLines.OrderBy(l => l.EndPoint.X).FirstOrDefault();
                var next = GetNextVerticalSecant(shortest, secant, ed);
                secant.Dispose();
                if (next == null)
                    return false;
                secant = next;
            }

            secant.Dispose();
            _lineIntersectionsLeftRight = To2DArray(lineIntersectionsList);
            return true;
        }

        private Line CreateVerticalSecant(double x)
        {
            return new Line(new Point3d(x, _yMin.Y, 0), new Point3d(x, _yMax.Y, 0));
        }

        private Line GetNextVerticalSecant(Line shortestIntersection, Line currentSecant, Editor ed)
        {
            if (currentSecant == null || _boundary == null)
                throw new System.ArgumentNullException("currentSecant or boundary cannot be null.");

            double x = currentSecant.EndPoint.X;

            if (shortestIntersection == null)
            {
                return CreateVerticalSecant(x + _step / 5.0);
            }

            int countPri = GetIntersectionCountAcad(currentSecant, _boundary);
            int countNext = countPri;
            Line next = null;
            int safetyCounter = 0;

            while (countPri == countNext && x <= shortestIntersection.EndPoint.X && safetyCounter < 1000)
            {
                x += _step;
                next?.Dispose();
                next = CreateVerticalSecant(x);
                try { countNext = GetIntersectionCountAcad(next, _boundary); }
                catch
                {
                    next?.Dispose();
                    ed.WriteMessage("\n垂直割线求交失败，中止标注。");
                    return null;
                }
                safetyCounter++;
            }

            if (safetyCounter >= 1000)
            {
                next?.Dispose();
                x = Math.Max(_xMax.X + _step, shortestIntersection.EndPoint.X + _step);
                next = CreateVerticalSecant(x);
            }

            return next;
        }

        #endregion

        #region 几何工具方法

        /// <summary>AutoCAD IntersectWith 求交点数量（用于割线推进判断，与旧代码一致）</summary>
        private int GetIntersectionCountAcad(Line line, Polyline boundary)
        {
            var points = new Point3dCollection();
            line.IntersectWith(boundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            return points.Count;
        }

        /// <summary>NTS 求线-多段线交点（用于获取实际交点坐标）</summary>
        private Point3dCollection GetIntersectionPointsNTS(Line line, Polyline boundary)
        {
            var points = new Point3dCollection();
            var gf = GeometryFactory.Default;

            var lineCoords = new Coordinate[]
            {
                new Coordinate(line.StartPoint.X, line.StartPoint.Y),
                new Coordinate(line.EndPoint.X, line.EndPoint.Y)
            };
            var lineGeom = gf.CreateLineString(lineCoords);

            var boundaryCoords = new Coordinate[boundary.NumberOfVertices + 1];
            for (int i = 0; i < boundary.NumberOfVertices; i++)
            {
                var pt = boundary.GetPoint3dAt(i);
                boundaryCoords[i] = new Coordinate(pt.X, pt.Y);
            }
            boundaryCoords[boundary.NumberOfVertices] = boundaryCoords[0];
            var boundaryGeom = gf.CreatePolygon(boundaryCoords);

            var intersection = lineGeom.Intersection(boundaryGeom);
            if (intersection != null)
            {
                foreach (var coord in intersection.Coordinates)
                {
                    points.Add(new Point3d(coord.X, coord.Y, 0));
                }
            }
            return points;
        }

        /// <summary>根据交点获取边界上的对应线段</summary>
        private Line[] GetIntersectionLines(Polyline boundary, Point3dCollection points)
        {
            var lines = new List<Line>();
            foreach (Point3d pt in points)
            {
                try
                {
                    // 先尝试用最近点来获取参数（避免 NTS 精度偏差导致的异常）
                    var closestPt = boundary.GetClosestPointTo(pt, false);
                    double param = boundary.GetParameterAtPoint(closestPt);
                    int idx1 = Convert.ToInt32(Math.Floor(param));
                    int idx2 = idx1 + 1;
                    if (idx1 >= boundary.NumberOfVertices - 1) idx2 = 0;
                    if (idx1 >= 0 && idx1 < boundary.NumberOfVertices &&
                        idx2 >= 0 && idx2 < boundary.NumberOfVertices)
                    {
                        lines.Add(new Line(boundary.GetPoint3dAt(idx1), boundary.GetPoint3dAt(idx2)));
                    }
                }
                catch (System.Exception) { /* 跳过无效点 */ }
            }
            return lines.ToArray();
        }

        private Line[] ExplodePolyline(Polyline pline)
        {
            var coll = new DBObjectCollection();
            pline.Explode(coll);
            var lines = new List<Line>();
            foreach (Entity ent in coll)
            {
                if (ent is Line l)
                    lines.Add(l);
                else
                    ent.Dispose();
            }
            return lines.ToArray();
        }

        private Point3d[] GetAllLinePoints(Line[] lines)
        {
            var pts = new List<Point3d>();
            foreach (var l in lines)
            {
                pts.Add(l.StartPoint);
                pts.Add(l.EndPoint);
            }
            return pts.ToArray();
        }

        /// <summary>获取点集边界 [xmin, xmax, ymin, ymax]</summary>
        private double[] GetPointsBounds(IEnumerable<Point3d> points)
        {
            var arr = points.ToArray();
            return new[]
            {
                arr.Min(p => p.X),
                arr.Max(p => p.X),
                arr.Min(p => p.Y),
                arr.Max(p => p.Y)
            };
        }

        private enum ExtremeSide { XMin, XMax, YMin, YMax }

        private Point3d GetExtreme(Point3d[] points, ExtremeSide side)
        {
            switch (side)
            {
                case ExtremeSide.XMin: return points.OrderBy(p => p.X).First();
                case ExtremeSide.XMax: return points.OrderByDescending(p => p.X).First();
                case ExtremeSide.YMin: return points.OrderBy(p => p.Y).First();
                case ExtremeSide.YMax: return points.OrderByDescending(p => p.Y).First();
                default: return points[0];
            }
        }

        private double GetEntitiesBoundValue(Line[] lines, ExtremeSide side)
        {
            var extents = new Extents3d();
            foreach (var l in lines)
            {
                if (l != null) extents.AddExtents(l.GeometricExtents);
            }
            switch (side)
            {
                case ExtremeSide.XMin: return extents.MinPoint.X;
                case ExtremeSide.XMax: return extents.MaxPoint.X;
                case ExtremeSide.YMin: return extents.MinPoint.Y;
                case ExtremeSide.YMax: return extents.MaxPoint.Y;
                default: return 0;
            }
        }

        #endregion

        #region 线段排序与方向

        private Line[] SortLinesByX(Line[] lines)
        {
            return lines.OrderBy(l => (l.StartPoint.X + l.EndPoint.X) / 2).ToArray();
        }

        private Line[] SortLinesByY(Line[] lines)
        {
            return lines.OrderBy(l => (l.StartPoint.Y + l.EndPoint.Y) / 2).ToArray();
        }

        private void NormalizeDirectionY(Line[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartPoint.Y > lines[i].EndPoint.Y)
                    lines[i] = new Line(lines[i].EndPoint, lines[i].StartPoint);
            }
        }

        private void NormalizeDirectionX(Line[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartPoint.X > lines[i].EndPoint.X)
                    lines[i] = new Line(lines[i].EndPoint, lines[i].StartPoint);
            }
        }

        /// <summary>调整线段起点终点满足标注排序（与旧代码 SortLineSEPointForDIm 一致）</summary>
        private Line[] SortLineSEPointForDim(Line[] lines)
        {
            var result = new List<Line>();
            foreach (var line in lines)
            {
                if (Math.Abs(line.StartPoint.X - line.EndPoint.X) < _toleranceDouble)
                {
                    // 近似竖线：确保起点 Y < 终点 Y
                    if (line.StartPoint.Y > line.EndPoint.Y)
                        result.Add(new Line(line.EndPoint, line.StartPoint));
                    else
                        result.Add(line);
                }
                else if (Math.Abs(line.StartPoint.X - line.EndPoint.X) > _toleranceDouble)
                {
                    // 非竖线：无条件翻转（旧代码原始逻辑）
                    result.Add(new Line(line.EndPoint, line.StartPoint));
                }
                else
                {
                    result.Add(line);
                }
            }
            return result.ToArray();
        }

        #endregion

        #region 二维数组工具

        private Line[] GetColumn(Line[,] array, int col)
        {
            int rows = array.GetLength(0);
            var result = new List<Line>();
            for (int r = 0; r < rows; r++)
            {
                if (array[r, col] != null) result.Add(array[r, col]);
            }
            return result.ToArray();
        }

        private Line[] GetRow(Line[,] array, int row)
        {
            int cols = array.GetLength(1);
            var result = new List<Line>();
            for (int c = 0; c < cols; c++)
            {
                if (array[row, c] != null) result.Add(array[row, c]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// 将不等长的锯齿数组转为二维数组（与旧代码 Fillarray + SortForDimension 一致）
        /// 短数组：前半段放前面，后半段放末尾，中间填 null
        /// </summary>
        private Line[,] To2DArray(List<Line[]> jagged)
        {
            if (jagged.Count == 0) return new Line[0, 0];
            int maxCols = jagged.Max(a => a.Length);
            var result = new Line[jagged.Count, maxCols];
            for (int r = 0; r < jagged.Count; r++)
            {
                var filled = FillArray(jagged[r], maxCols);
                for (int c = 0; c < maxCols; c++)
                {
                    result[r, c] = filled[c];
                }
            }
            return result;
        }

        /// <summary>
        /// 填充数组到指定长度（与旧代码 Fillarray 一致）
        /// 前半段放开头，后半段放末尾，中间留 null
        /// </summary>
        private Line[] FillArray(Line[] source, int targetLength)
        {
            int count = source.Length;
            var values = new Line[targetLength];
            if (count == targetLength)
            {
                Array.Copy(source, values, count);
                return values;
            }
            if (count % 2 == 0)
            {
                for (int i = 0; i < count / 2; i++)
                {
                    values[i] = source[i];
                    values[targetLength - 1 - i] = source[count - 1 - i];
                }
            }
            else
            {
                int i;
                for (i = 0; i < count / 2; i++)
                {
                    values[i] = source[i];
                    values[targetLength - 1 - i] = source[count - 1 - i];
                }
                values[i] = source[i];
            }
            return values;
        }

        #endregion

        #region 标注点提取与排序

        private Point3d[] GetDimensionPoints(Line[] lines, DimensionFor direction)
        {
            var pts = new List<Point3d>();
            foreach (var l in lines)
            {
                pts.Add(l.StartPoint);
                pts.Add(l.EndPoint);
            }
            var distinct = pts.Distinct().ToArray();

            switch (direction)
            {
                case DimensionFor.ForLeft:
                    return distinct.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
                case DimensionFor.ForRight:
                    return distinct.OrderBy(p => p.Y).ThenByDescending(p => p.X).ToArray();
                case DimensionFor.ForUp:
                    return distinct.OrderBy(p => p.X).ThenByDescending(p => p.Y).ToArray();
                case DimensionFor.ForDown:
                    return distinct.OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
                default:
                    return distinct;
            }
        }

        #endregion

        #region 标注创建（从线段）

        private RotatedDimension[] GetDimsByLines(Line[] lines, double distance, DimensionFor direction, bool isEqualLength)
        {
            var dims = new List<RotatedDimension>();
            foreach (var line in lines)
            {
                dims.Add(CreateDimFromTwoPoints(line.StartPoint, line.EndPoint, distance, direction, isEqualLength));
            }
            return dims.ToArray();
        }

        private RotatedDimension CreateDimFromTwoPoints(Point3d p1, Point3d p2, double offsetDist, DimensionFor direction, bool isEqualLength)
        {
            double angle = 0;
            var dimLinePoint = new Point3d();

            switch (direction)
            {
                case DimensionFor.ForLeft:
                    angle = Math.PI / 2;
                    dimLinePoint = p1.X < p2.X
                        ? p1 - new Vector3d(offsetDist, 0, 0)
                        : p2 - new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForRight:
                    angle = Math.PI / 2;
                    dimLinePoint = p1.X < p2.X
                        ? p2 + new Vector3d(offsetDist, 0, 0)
                        : p1 + new Vector3d(offsetDist, 0, 0);
                    break;
                case DimensionFor.ForUp:
                    angle = 0;
                    dimLinePoint = p1.Y < p2.Y
                        ? p2 + new Vector3d(0, offsetDist, 0)
                        : p1 + new Vector3d(0, offsetDist, 0);
                    break;
                case DimensionFor.ForDown:
                    angle = 0;
                    dimLinePoint = p1.Y < p2.Y
                        ? p1 - new Vector3d(0, offsetDist, 0)
                        : p2 - new Vector3d(0, offsetDist, 0);
                    break;
            }

            var dim = new RotatedDimension
            {
                XLine1Point = p1,
                XLine2Point = p2,
                DimLinePoint = dimLinePoint,
                Rotation = angle
            };

            if (isEqualLength) DimVEqualLength(dim);
            return dim;
        }

        #endregion

        #region 标注后处理

        private RotatedDimension[] DeleteDimensionZero(RotatedDimension[] dims)
        {
            return dims.Where(d => d.Measurement > 1.0).ToArray();
        }

        private List<RotatedDimension> DeleteSamePointDim(List<RotatedDimension> dims)
        {
            return dims.GroupBy(x => x, new DimEqualityComparer())
                       .Select(g => g.Key)
                       .ToList();
        }

        private RotatedDimension[] DeleteNearbyParallelDim(RotatedDimension[] dims, double distance)
        {
            var comparer = new DimParallelComparer(_toleranceDouble);
            var groups = dims.GroupBy(x => x, comparer);
            var result = dims.ToList();

            foreach (var group in groups)
            {
                var sameDims = group.ToList();
                for (int i = 0; i < sameDims.Count - 1; i++)
                {
                    double d = GetParallelDimDistance(sameDims[i], sameDims[i + 1]);
                    if (d < distance)
                    {
                        result.Remove(sameDims[i]);
                    }
                }
            }
            return result.ToArray();
        }

        private RotatedDimension[] DimVsEqualLength(RotatedDimension[] dims)
        {
            if (dims == null || dims.Length == 0) return dims;

            // 找竖线最短的标注
            double minLen = double.MaxValue;
            foreach (var dim in dims)
            {
                var lengths = DimVLength(dim);
                double shorter = Math.Min(lengths[0], lengths[1]);
                if (shorter < minLen) minLen = shorter;
            }

            var result = new List<RotatedDimension>();
            foreach (var dim in dims)
            {
                result.Add(ChangeDimVLineLength(dim, minLen, minLen));
            }
            return result.ToArray();
        }

        private void DimVEqualLength(RotatedDimension dim)
        {
            var dimPoints = DimVPoint(dim);
            var lengths = DimVLength(dim);
            double d = Math.Abs(lengths[1] - lengths[0]);

            if (lengths[0] > lengths[1])
            {
                var vec = dimPoints[0] - dim.XLine1Point;
                if (TryGetNormal(vec, out Vector3d n))
                    dim.XLine1Point = dim.XLine1Point + n * d;
            }
            else
            {
                var vec = dimPoints[1] - dim.XLine2Point;
                if (TryGetNormal(vec, out Vector3d n))
                    dim.XLine2Point = dim.XLine2Point + n * d;
            }
        }

        private RotatedDimension ChangeDimVLineLength(RotatedDimension dim, double len1, double len2)
        {
            var dimPoints = DimVPoint(dim);
            var vec1 = dim.XLine1Point - dimPoints[0];
            if (TryGetNormal(vec1, out Vector3d n1))
                dim.XLine1Point = dimPoints[0] + n1 * len1;

            var vec2 = dim.XLine2Point - dimPoints[1];
            if (TryGetNormal(vec2, out Vector3d n2))
                dim.XLine2Point = dimPoints[1] + n2 * len2;

            return dim;
        }

        private static bool TryGetNormal(Vector3d vec, out Vector3d normal)
        {
            if (vec.Length < 1e-6)
            {
                normal = Vector3d.XAxis;
                return false;
            }

            normal = vec.GetNormal();
            return true;
        }

        private double[] DimVLength(RotatedDimension dim)
        {
            var pts = DimVPoint(dim);
            return new[] { pts[0].DistanceTo(dim.XLine1Point), pts[1].DistanceTo(dim.XLine2Point) };
        }

        private Point3d[] DimVPoint(RotatedDimension dim)
        {
            var vecDim = Vector3d.XAxis.RotateBy(dim.Rotation, Vector3d.ZAxis);
            var xline = new Xline { BasePoint = dim.DimLinePoint, SecondPoint = dim.DimLinePoint + vecDim };
            var p1 = xline.GetClosestPointTo(dim.XLine1Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var p2 = xline.GetClosestPointTo(dim.XLine2Point, vecDim.RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            return new[] { p1, p2 };
        }

        private double GetParallelDimDistance(RotatedDimension dim1, RotatedDimension dim2)
        {
            var pts1 = DimVPoint(dim1).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            var pts2 = DimVPoint(dim2).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
            var seg1 = new LineSegment3d(pts1[0], pts1[1]);
            var seg2 = new LineSegment3d(pts2[0], pts2[1]);
            return seg1.GetDistanceTo(seg2);
        }

        #endregion

        #region 内部比较器

        private class DimEqualityComparer : IEqualityComparer<RotatedDimension>
        {
            private const double Tol = 1e-2;

            public bool Equals(RotatedDimension x, RotatedDimension y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                var a = Normalize(x);
                var b = Normalize(y);
                var tolerance = new Tolerance(Tol, Tol);
                return a.XLine1Point.IsEqualTo(b.XLine1Point, tolerance) &&
                       a.XLine2Point.IsEqualTo(b.XLine2Point, tolerance);
            }

            public int GetHashCode(RotatedDimension dim)
            {
                var n = Normalize(dim);
                return ($"{n.XLine1Point.X:F0}{n.XLine1Point.Y:F0}{n.XLine2Point.X:F0}{n.XLine2Point.Y:F0}").GetHashCode();
            }

            private static RotatedDimension Normalize(RotatedDimension dim)
            {
                var pts = new[] { dim.XLine1Point, dim.XLine2Point }
                    .OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
                return new RotatedDimension { XLine1Point = pts[0], XLine2Point = pts[1] };
            }
        }

        private class DimParallelComparer : IEqualityComparer<RotatedDimension>
        {
            private readonly double _tol;
            public DimParallelComparer(double tolerance) { _tol = tolerance; }

            public bool Equals(RotatedDimension x, RotatedDimension y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return Math.Abs(x.Rotation - y.Rotation) < _tol &&
                       Math.Abs(x.Measurement - y.Measurement) < _tol;
            }

            public int GetHashCode(RotatedDimension dim)
            {
                return ($"{dim.Rotation:F6}{dim.Measurement:F6}").GetHashCode();
            }
        }

        #endregion
    }
}
