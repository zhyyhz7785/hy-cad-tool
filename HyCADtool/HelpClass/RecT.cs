using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using HyCADTool.Config;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static class RecT
    {
        public enum BoundStatus
        {
            In,
            Out,
            Intersection,
        }
        public enum RecPosition
        {
            MirrorX,
            MirrorY,
            MirrorXy,
            Distance,
            Rotation
        }
        public enum MovePrinciple
        {
            Deviation,
            Left,
            Right,
            Up,
            Down,
        }
        public enum MaxMinPoint
        {
            xmin,
            xmax,
            ymin,
            ymax
        }
        public enum MoveRecRangeStatus
        {
            Scale,
            EqualDistance,
            Square,
        }
        #region 主要方法      
        /// <summary>
        /// 必须保证 Entity是文字
        /// </summary>
        /// <param name="ents"></param>
        /// <returns></returns>
        public static void MoveIntersectionText(Entity[] ents)
        {
            var dic = ents.MoveIntersectionRecByEntities(out Dictionary<int, int[]> indexDic);
            for (int i = 0; i < dic.Count; i++)
            {
                var baseRec = dic.ToArray()[i].Key;
                var recs = dic.ToArray()[i].Value;
                var rec = new Rec(baseRec, recs);
                var vec = rec.MovePolyVecs;
                var rec1 = rec.ExtendBasePoly;
                rec1.ToSpace();
                var indexs = indexDic.ToArray()[i].Value;
                var moveRecs = ents.GetArrayByIndexs(indexs);
                moveRecs.MoveEnts(vec);
            }
        }
        public static Dictionary<Polyline, Polyline[]> MoveIntersectionRecByEntities
            (this Entity[] ents, out Dictionary<int, int[]> indexDic)
        {
            var recs = GetRecsByTexts(ents);
            var dic = MoveIntersectionRec(recs, out Dictionary<int, int[]> iDic);
            indexDic = iDic;
            return dic;
        }
        #endregion
        #region 辅助方法1
        public static Vector3d GetMoveDirection(this Polyline baseRec, Polyline moveRec,
          MovePrinciple principle)
        {
            var pa = baseRec.GetRecCenterPoint();
            var pb = moveRec.GetRecCenterPoint();
            var direction = new Vector3d(0, 0, 0);
            switch (principle)
            {
                case MovePrinciple.Deviation:
                    direction = (pb - pa).GetNormal();
                    break;
                case MovePrinciple.Left:
                    direction = new Vector3d(-1, 0, 0);
                    break;
                case MovePrinciple.Right:
                    direction = new Vector3d(1, 0, 0);
                    break;
                case MovePrinciple.Up:
                    direction = new Vector3d(0, 1, 0);
                    break;
                case MovePrinciple.Down:
                    direction = new Vector3d(0, -1, 0);
                    break;
            }
            return direction;
        }
        /// <summary>
        /// 单向放大边界
        /// </summary>
        /// <param name="baseRec"></param>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Polyline GetExtendRecByVec(this Polyline baseRec, Vector2d vec)
        {
            var p0 = baseRec.GetPoint2dAt(0);
            var p1 = baseRec.GetPoint2dAt(1);
            var p2 = baseRec.GetPoint2dAt(2);
            var p3 = baseRec.GetPoint2dAt(3);
            var hvec = p1 - p0;
            var vvec = p3 - p0;
            p1 = p1 + hvec.GetNormal() * (vec.DotProduct(hvec) / hvec.Length);
            p3 = p3 + vvec.GetNormal() * (vec.DotProduct(vvec) / vvec.Length);
            p2 = p2 + vec;
            var poly = new Polyline();
            poly.AddVertexAt(poly.NumberOfVertices, p0, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p3, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        public static Polyline GetExtendRecBothSideByVec(this Polyline baseRec, Vector2d vec)
        {
            var p0 = baseRec.GetPoint2dAt(0);
            var p1 = baseRec.GetPoint2dAt(1);
            var p2 = baseRec.GetPoint2dAt(2);
            var p3 = baseRec.GetPoint2dAt(3);
            var hvec = p1 - p0;
            var vvec = p3 - p0;
            p1 = p1 + hvec.GetNormal() * (vec.DotProduct(hvec) / hvec.Length);
            p3 = p3 + vvec.GetNormal() * (vec.DotProduct(vvec) / vvec.Length);
            p2 = p2 + vec;
            p0 = p0 - vec;
            p1 = p1 - vvec.GetNormal() * (vec.DotProduct(vvec) / vvec.Length);
            p3 = p3 - hvec.GetNormal() * (vec.DotProduct(hvec) / hvec.Length);
            var poly = new Polyline();
            poly.AddVertexAt(poly.NumberOfVertices, p0, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p3, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        public static Polyline GetExtendRecByXY(this Polyline baseRec, double x, double y)
        {
            var p0 = baseRec.GetPoint2dAt(0);
            var p1 = baseRec.GetPoint2dAt(1);
            var p2 = baseRec.GetPoint2dAt(2);
            var p3 = baseRec.GetPoint2dAt(3);
            var hvec = p1 - p0;
            var vvec = p3 - p0;
            p1 = p1 + hvec.GetNormal() * x;
            p3 = p3 + vvec.GetNormal() * y;
            p2 = p2 + hvec.GetNormal() * x + vvec.GetNormal() * y;
            var poly = new Polyline();
            poly.AddVertexAt(poly.NumberOfVertices, p0, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p3, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        public static Polyline GetExtendRecBothSideByXY(this Polyline baseRec, double x, double y)
        {
            var p0 = baseRec.GetPoint2dAt(0);
            var p1 = baseRec.GetPoint2dAt(1);
            var p2 = baseRec.GetPoint2dAt(2);
            var p3 = baseRec.GetPoint2dAt(3);
            var hvec = p1 - p0;
            var vvec = p3 - p0;
            p1 = p1 + hvec.GetNormal() * x - vvec.GetNormal() * y;
            p3 = p3 + vvec.GetNormal() * y - hvec.GetNormal() * x;
            p2 = p2 + hvec.GetNormal() * x + vvec.GetNormal() * y;
            p0 = p0 - hvec.GetNormal() * x - vvec.GetNormal() * y;
            var poly = new Polyline();
            poly.AddVertexAt(poly.NumberOfVertices, p0, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, p3, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        /// <summary>
        /// 双向放大边界
        /// </summary>
        /// <param name="baseRec"></param>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Polyline GetExtendRecMirror(this Polyline baseRec, Vector3d vec)
        {
            var pmin = baseRec.GetPoint3dAt(0);
            var pmax = baseRec.GetPoint3dAt(2);
            pmin = pmin - vec;
            pmax = pmax + vec;
            var poly = GetRecByPoints(pmin, pmax);
            return poly;
        }
        /// <summary>
        /// 从中间点重设边界  延伸出一个直角四边形
        /// </summary>
        /// <param name="baseRec"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static Polyline GetExtendSquare(this Polyline baseRec, Vector3d length)
        {
            var pmin = baseRec.GetPoint3dAt(0);
            var pmax = baseRec.GetPoint3dAt(2);
            var squareMinp = baseRec.GetRecCenterPoint() - length / 2;
            var squareMaxp = baseRec.GetRecCenterPoint() + length / 2;
            var poly = GetRecByPoints(squareMinp, squareMaxp);
            return poly;
        }
        #endregion
        #region 基础方法 获取交点
        public static Point3d[] GetIntersectionPointsByLine(this Line line, Line boundary)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var points = new Point3dCollection();
            line.IntersectWith(boundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            var list = new List<Point3d>();
            foreach (var point in points)
            {
                list.Add((Point3d)point);
            }
            return list.ToArray();
        }
        public static Point3dCollection GetIntersectionPointsByPolyBoundary(this Line line, Polyline boundary)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var points = new Point3dCollection();
            line.IntersectWith(boundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            return points;
        }
        public static Point3dCollection GetRecIntersection(this Polyline basePoly, Polyline moveRec)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var points = new Point3dCollection();
            basePoly.IntersectWith(moveRec, Intersect.ExtendArgument, points, IntPtr.Zero, IntPtr.Zero);
            return points;
        }
        /// <summary>
        /// 得到在图形内部，沿直线方向上的，与图形的交点
        /// 注意，直线必须在图形内部，不然出错
        /// </summary>
        /// <param name="line"></param>
        /// <param name="basePoly"></param>
        /// <returns></returns>
        public static Point3d? GetLineInterSectPointUseVec(this Line line, Polyline basePoly)
        {
            var points = new Point3dCollection();
            line.IntersectWith(basePoly, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);
            var pointsV = new List<Point3d>();
            var veca = line.Delta.GetNormal();
            if (points.Count > 0)
            {
                foreach (var point in points)
                {
                    var vecb = ((Point3d)point - line.EndPoint).GetNormal();
                    if (veca.IsEqualTo(vecb, BaseConfig.ToleranceVec))
                    {
                        pointsV.Add((Point3d)point);
                    }
                }
                var a = pointsV.FirstOrDefault();
                a.MakeMark();
                return pointsV.FirstOrDefault();
            }
            return null;
        }
        public static Point3d[] Point3dCollToArray(this Point3dCollection coll)
        {
            var list = new List<Point3d>();
            foreach (var point in coll)
            {
                list.Add((Point3d)point);
            }
            return list.ToArray();
        }
        /// <summary>
        /// Points => BoundaryLines
        /// </summary>
        /// <param name="boundary"></param>
        /// <param name="points"></param>
        /// <returns>直线数组</returns>
        public static Line[] GetIntersectionLineByPoints(this Polyline boundary, Point3dCollection points)
        {
            Line[] lines = new Line[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                double ParameterResult = boundary.GetParameterAtPoint(points[i]);
                int idxPt1 = Convert.ToInt32(Math.Floor(ParameterResult));
                int idxPt2 = idxPt1 + 1;
                //如果为闭合PolyLine，当到达EndSegment，idxPt2 = 0 也就是起点（没有下一个点了，会出错）
                if (idxPt1 == boundary.NumberOfVertices - 1)
                {
                    idxPt2 = 0;
                }
                lines[i] = new Line(boundary.GetPoint3dAt(idxPt1), boundary.GetPoint3dAt(idxPt2));
            }
            return lines;
        }


        public static Point3dCollection GetIntersectionPointsByPolyBoundaryClipper(this Line line, Polyline boundary)
        {
            var points = new Point3dCollection();

            // 转换为Clipper2的路径格式
            var linePath = new PathD(new[]
            {
            new PointD(line.StartPoint.X, line.StartPoint.Y),
            new PointD(line.EndPoint.X, line.EndPoint.Y)
        });

            var boundaryPath = new PathD();
            for (int i = 0; i < boundary.NumberOfVertices; i++)
            {
                var pt = boundary.GetPoint3dAt(i);
                boundaryPath.Add(new PointD(pt.X, pt.Y));
            }
            // 如果边界未闭合，可以添加首点以闭合
            if (!boundary.Closed)
            {
                var firstPt = boundary.GetPoint3dAt(0);
                boundaryPath.Add(new PointD(firstPt.X, firstPt.Y));
            }

            // 执行交集计算
            var solution = new PathsD();
            ClipperD clipper = new ClipperD();
            clipper.AddSubject(linePath);
            clipper.AddClip(boundaryPath);
            clipper.Execute(ClipType.Intersection, FillRule.NonZero, solution);

            // 收集交点
            foreach (var path in solution)
            {
                foreach (var pt in path)
                {
                    points.Add(new Point3d(pt.x, pt.y, 0));
                }
            }

            return points;
        }

        public static Point3dCollection GetIntersectionPointsByPolyBoundaryNTS(this Line line, Polyline boundary)
        {
            var points = new Point3dCollection();

            // 创建NTS几何对象
            var gf = NetTopologySuite.Geometries.GeometryFactory.Default;
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
            // 闭合边界
            boundaryCoords[boundary.NumberOfVertices] = boundaryCoords[0];
            var boundaryGeom = gf.CreatePolygon(boundaryCoords);

            // 计算交点
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

        public static double[] GetRangeNumbers(double end, int number, out double separation)
        {
            var start = 0.0;
            double[] outNumbers = new double[number];
            double divide = ((double)end - (double)start) / ((double)number - 1);
            separation = divide;
            outNumbers[0] = start;
            var temp = start;
            for (int i = 1; i < number - 1; i++)
            {
                temp = temp + divide;
                outNumbers[i] = temp;
            }
            outNumbers[number - 1] = end;
            return outNumbers;
        }
        #endregion
        #region 基础方法 得到边界
        public static Point3d GetMaxMinPoint(this Point3d[] points, MaxMinPoint value)
        {
            var tolerance = new Tolerance(Tolerance.Global.EqualVector, 1e-6);
            Point3d outValue = new Point3d();
            switch (value)
            {
                case MaxMinPoint.xmin:
                    outValue = points[0];
                    var pts = points.Distinct();
                    var d = pts.Count();
                    foreach (var point in pts)
                    {
                        if (outValue.X > point.X)
                        {
                            outValue = point;
                        }
                    }
                    break;
                case MaxMinPoint.xmax:
                    outValue = points[0];
                    int j = 0;
                    foreach (var point in points)
                    {
                        if (outValue.X < point.X)
                        {
                            outValue = point;
                        }
                        j++;
                    }
                    break;
                case MaxMinPoint.ymin:
                    outValue = points[0];
                    int k = 0;
                    foreach (var point in points)
                    {
                        if (outValue.Y > point.Y)
                        {
                            outValue = point;
                        }
                        k++;
                    }
                    break;
                case MaxMinPoint.ymax:
                    outValue = points[0];
                    int l = 0;
                    foreach (var point in points)
                    {
                        if (outValue.Y < point.Y)
                        {
                            outValue = point;
                        }
                        l++;
                    }
                    break;
            }
            return outValue;
        }
        public static double GetBoundXYFromEntities(this IEnumerable<Entity> ents, MaxMinPoint maxMinPoint)
        {
            Extents3d bound = new Extents3d();
            double d = 0;
            foreach (var ent in ents)
            {
                bound.AddExtents(ent.GeometricExtents);
            }
            switch (maxMinPoint)
            {
                case MaxMinPoint.xmin:
                    d = bound.MinPoint.X;
                    break;
                case MaxMinPoint.xmax:
                    d = bound.MaxPoint.X;
                    break;
                case MaxMinPoint.ymin:
                    d = bound.MinPoint.Y;
                    break;
                case MaxMinPoint.ymax:
                    d = bound.MaxPoint.Y;
                    break;
            }
            return d;
        }
        public static Polyline GetBoundFromEntity(this Entity ent)
        {
            Extents3d bound = new Extents3d();
            bound = ent.GeometricExtents;
            var poly = bound.GetBoundRecFromExtent3d();
            return poly;
        }
        public static Polyline GetBoundFromEntities(this IEnumerable<Entity> ents)
        {
            Extents3d bound = new Extents3d();
            foreach (var ent in ents)
            {
                bound.AddExtents(ent.GeometricExtents);
            }
            var poly = bound.GetBoundRecFromExtent3d();
            poly.ToSpace();
            return poly;
        }
        public static double[] GetBoundFromEntities(this IEnumerable<Point3d> points)
        {
            var xmin = points.OrderBy(p => p.X).ToArray()[0].X;
            var xmax = points.OrderBy(p => p.X).ToArray()[points.Count() - 1].X;
            var ymin = points.OrderBy(p => p.Y).ToArray()[0].Y;
            var ymax = points.OrderBy(p => p.Y).ToArray()[points.Count() - 1].Y;
            return new double[] { xmin, xmax, ymin, ymax };
        }
        public static double GetBoundXYFromEntities(this IEnumerable<Point3d> points, MaxMinPoint maxMinPoint)
        {
            double d = 0;
            var xmin = points.OrderBy(p => p.X).ToArray()[0].X;
            var xmax = points.OrderBy(p => p.X).ToArray()[points.Count() - 1].X;
            var ymin = points.OrderBy(p => p.Y).ToArray()[0].Y;
            var ymax = points.OrderBy(p => p.Y).ToArray()[points.Count() - 1].Y;
            switch (maxMinPoint)
            {
                case MaxMinPoint.xmin:
                    d = xmin;
                    break;
                case MaxMinPoint.xmax:
                    d = xmax;
                    break;
                case MaxMinPoint.ymin:
                    d = ymin;
                    break;
                case MaxMinPoint.ymax:
                    d = ymax;
                    break;
            }
            return d;
        }
        /// <summary>
        /// 先比较点的x值，再比较点的y值，返回最小或最大
        /// </summary>
        /// <param name="points">参与比较的点</param>
        /// <param name="IsMin">默认最小，false为最大值</param>
        /// <returns></returns>
        public static Point3d GetMinxPoint(this IEnumerable<Point3d> points, bool IsMin = true)
        {
            points = points.OrderBy(x => x.X).ThenBy(x => x.Y);
            if (IsMin == false)
            {
                return points.LastOrDefault();
            }
            else
            {
                return points.FirstOrDefault();
            }
        }
        /// <summary>
        /// 给边界画一个方框
        /// </summary>
        /// <param name="bound"></param>
        /// <returns></returns>
        public static Polyline GetBoundRecFromExtent3d(this Extents3d bound)
        {
            Point3d point1 = bound.MinPoint;
            Point3d point2 = bound.MaxPoint;
            Point2d point2d1 = new Point2d(point1.X, point1.Y);
            Point2d point2d2 = new Point2d(point2.X, point2.Y);
            Polyline poly = new Polyline(4);
            Point2d pt1 = point2d1;
            Point2d pt2 = point2d1 + new Vector2d(point2d2.X - point2d1.X, 0);
            Point2d pt3 = point2d2;
            Point2d pt4 = point2d2 - new Vector2d(point2d2.X - point2d1.X, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt3, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt4, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        public static Polyline GetRecByPoints(this Point3d min, Point3d max)
        {
            Point3d point1 = min;
            Point3d point2 = max;
            Point2d point2d1 = new Point2d(point1.X, point1.Y);
            Point2d point2d2 = new Point2d(point2.X, point2.Y);
            Polyline poly = new Polyline(4);
            Point2d pt1 = point2d1;
            Point2d pt2 = point2d1 + new Vector2d(point2d2.X - point2d1.X, 0);
            Point2d pt3 = point2d2;
            Point2d pt4 = point2d2 - new Vector2d(point2d2.X - point2d1.X, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt1, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt2, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt3, 0, 0, 0);
            poly.AddVertexAt(poly.NumberOfVertices, pt4, 0, 0, 0);
            poly.Closed = true;
            return poly;
        }
        public static LineSegment3d[] GetCenterLineFromRec(this Polyline rec)
        {
            var p0 = rec.GetPoint3dAt(0);
            var p1 = rec.GetPoint3dAt(1);
            var p2 = rec.GetPoint3dAt(2);
            var p3 = rec.GetPoint3dAt(3);
            var m1 = new LineSegment3d(p0, p3).MidPoint;
            var m2 = new LineSegment3d(p1, p2).MidPoint;
            var axisM = new LineSegment3d(m1, m2);
            var n1 = new LineSegment3d(p0, p1).MidPoint;
            var n2 = new LineSegment3d(p2, p3).MidPoint;
            var axisN = new LineSegment3d(n1, n2);
            return new LineSegment3d[] { axisM, axisN };
        }
        public static LineSegment3d[] GetBoundLineFromRec(this Polyline rec)
        {
            var segs = new LineSegment3d[4];
            for (int i = 0; i < 3; i++)
            {
                var pa = rec.GetPoint3dAt(i);
                var pb = rec.GetPoint3dAt(i + 1);
                segs[i] = new LineSegment3d(pa, pb);
            }
            segs[3] = new LineSegment3d(rec.GetPoint3dAt(3), rec.GetPoint3dAt(0));
            return segs;
        }
        public static Point3d GetRecCenterPoint(this Polyline rec)
        {
            var pointa = rec.GetPoint3dAt(0);
            var pointb = rec.GetPoint3dAt(2);
            var pointc = new LineSegment3d(pointa, pointb).MidPoint;
            return pointc;
        }
        public static Polyline ChangeCenter(this Polyline rec, Vector3d MoveVec)
        {
            var centerP = rec.GetRecCenterPoint() + MoveVec;
            var ap = ((Extents3d)rec.Bounds).MinPoint;
            var bp = ((Extents3d)rec.Bounds).MaxPoint;
            var vec = bp - ap;
            var minp = centerP - vec / 2;
            var maxp = centerP + vec / 2;
            var recOut = GetRecByPoints(minp, maxp);
            return recOut;
        }
        #endregion
        #region 基础方法 得到文字的边界      
        public static Polyline[] GetRecsByTexts(this Entity[] ents)
        {
            var polys = new List<Polyline>();
            foreach (var ent in ents)
            {
                if (ent is DBText)
                {
                    var tempPoly = ((DBText)ent).GetRecByText();
                    polys.Add(tempPoly);
                }
                if (ent is MText)
                {
                    var tempPoly = ((MText)ent).GetRecByText();
                    polys.Add(tempPoly);
                }
            }
            ;
            return polys.ToArray();
        }
        public static Polyline GetRecByText(this DBText text)
        {
            var rotation = text.Rotation;
            text.RotateEnt(text.Position, -rotation);
            var startp = ((Extents3d)text.Bounds).MinPoint;
            var endp = ((Extents3d)text.Bounds).MaxPoint;
            text.RotateEnt(text.Position, rotation);
            var points = new Point3d[] { startp, endp };
            var poly = startp.GetRecByPoints(endp);
            poly.RotateEnt(text.Position, rotation);
            return poly;
        }
        public static Polyline GetRecByText(this MText text)
        {
            var rotation = text.Rotation;
            text.RotateEnt(text.Location, -rotation);
            var startp = ((Extents3d)text.Bounds).MinPoint;
            startp.MakeMark("s1", 100, 150, 0, 0);
            var endp = ((Extents3d)text.Bounds).MaxPoint;
            var h = text.ActualWidth;
            var v = endp.Y - startp.Y;
            var endpN = startp + new Vector3d(h, v, 0);
            endp.MakeMark("s2", 100, 150, 0, 0);
            text.RotateEnt(text.Location, rotation);
            var points = new Point3d[] { startp, endpN };
            var poly = startp.GetRecByPoints(endpN);
            poly.RotateEnt(text.Location, rotation);
            return poly;
        }
        public static Polyline GetRecAndRotationByText(this DBText text, out double rotation, out Point3d roP)
        {
            rotation = text.Rotation;
            text.RotateEnt(text.Position, -rotation);
            var startp = ((Extents3d)text.Bounds).MinPoint;
            var endp = ((Extents3d)text.Bounds).MaxPoint;
            text.RotateEnt(text.Position, rotation);
            var points = new Point3d[] { startp, endp };
            var poly = startp.GetRecByPoints(endp);
            roP = text.Position;
            return poly;
        }
        public static Polyline GetRecAndRotationByText(this MText text, out double rotation, out Point3d rop)
        {
            rotation = text.Rotation;
            text.RotateEnt(text.Location, -rotation);
            var startp = ((Extents3d)text.Bounds).MinPoint;
            startp.MakeMark("s1", 100, 150, 0, 0);
            var endp = ((Extents3d)text.Bounds).MaxPoint;
            var h = text.ActualWidth;
            var v = endp.Y - startp.Y;
            var endpN = startp + new Vector3d(h, v, 0);
            endp.MakeMark("s2", 100, 150, 0, 0);
            text.RotateEnt(text.Location, rotation);
            var points = new Point3d[] { startp, endpN };
            var poly = startp.GetRecByPoints(endpN);
            rop = text.Location;
            return poly;
        }
        #endregion
        #region 基础方法 得到文字的 BaseRec MoveRec
        public static Dictionary<Polyline, Polyline[]> MoveIntersectionRec
            (this Polyline[] recs, out Dictionary<int, int[]> indexDic)
        {
            var rowColValues = recs.GetRecsBoundStatus();
            recs.ToSpace();
            var dic = recs.GetRecIntersectionDic(rowColValues);
            indexDic = dic;
            var dicOut = new Dictionary<Polyline, Polyline[]>();
            foreach (var kv in dic)
            {
                var baseRec = recs[kv.Key];
                var moveRecs = new List<Polyline>();
                foreach (var n in kv.Value)
                {
                    var moveRec = recs[n];
                    moveRecs.Add(moveRec);
                }
                dicOut.Add(baseRec, moveRecs.ToArray());
            }
            return dicOut;
        }
        /// <summary>
        /// 得到所有 Rec 和其它Rec的相交状态
        /// </summary>
        /// <param name="recs"></param>
        /// <returns></returns>
        public static RowColValue<BoundStatus>[] GetRecsBoundStatus(this Polyline[] recs)
        {
            var rowColVs = new List<RowColValue<BoundStatus>>();
            for (int i = 0; i < recs.Count(); i++)
            {
                var baseRec = recs[i];
                for (int j = 0; j <= i; j++)
                {
                    if (i != j)
                    {
                        var state = baseRec.GetRecBoundState(recs[j]);
                        var rowColV = new RowColValue<BoundStatus>(i, j, state);
                        rowColVs.Add(rowColV);
                    }
                }
            }
            return rowColVs.ToArray();
        }
        public static Dictionary<int, int[]> GetRecIntersectionDic
            (this Polyline[] recs, RowColValue<BoundStatus>[] statuses)
        {
            var dic = new Dictionary<int, int[]>();
            for (int i = 0; i < recs.Length; i++)
            {
                var row = i;
                var cols = statuses.Where(x => x.Row == row)
                     .Where(x => x.Value == BoundStatus.In || x.Value == BoundStatus.Intersection)
                     .Select(x => x.Col).ToArray();
                dic.Add(row, cols);
            }
            dic = dic.OrderByDescending(x => x.Value.Count()).ToDictionary(x => x.Key, x => x.Value);
            return dic;
        }
        #endregion
        #region 边界状态
        //public static BoundStatus GetRecBoundState(this Polyline baseRec, Polyline moveRec)
        //{
        //    var points = GetRecIntersection(baseRec, moveRec);
        //    var n = points.Count;
        //    var baseMinP = ((Extents3d)baseRec.Bounds).MinPoint;
        //    var minP = ((Extents3d)moveRec.Bounds).MinPoint;
        //    var baseMaxP = ((Extents3d)baseRec.Bounds).MaxPoint;
        //    var maxP = ((Extents3d)moveRec.Bounds).MaxPoint;
        //    if (n == 0)
        //    {
        //        if (minP.X >= baseMinP.X && minP.X <= baseMaxP.X
        //           && maxP.X >= baseMinP.X && maxP.X <= baseMaxP.X
        //           && minP.Y >= baseMinP.Y && minP.Y <= baseMaxP.Y
        //           && maxP.Y >= baseMinP.Y && maxP.Y <= baseMaxP.Y
        //           )
        //        {
        //            return BoundStatus.In;
        //        }
        //        return BoundStatus.Out;
        //    }
        //    else
        //    {
        //        return BoundStatus.Intersection;
        //    }
        //}
        public static BoundStatus GetRecBoundState(this Polyline baseRec, Polyline MoveRec)
        {
            var lines = new Line[4]
            {
                new Line(MoveRec.GetPoint3dAt(0),MoveRec.GetPoint3dAt(1)),
                new Line(MoveRec.GetPoint3dAt(1),MoveRec.GetPoint3dAt(2)),
                new Line(MoveRec.GetPoint3dAt(2),MoveRec.GetPoint3dAt(3)),
                new Line(MoveRec.GetPoint3dAt(3),MoveRec.GetPoint3dAt(0)),
            };
            var state = new BoundStatus();
            foreach (var line in lines)
            {
                state = line.GetLineBoundState(baseRec);
                if (state == BoundStatus.Intersection)
                {
                    return BoundStatus.Intersection;
                }
            }
            return state;
        }
        public static BoundStatus GetLineBoundState(this Line line, Polyline basePoly)
        {
            var pointsNoExtend = new Point3dCollection();
            line.IntersectWith(basePoly, Intersect.ExtendArgument, pointsNoExtend, IntPtr.Zero, IntPtr.Zero);
            var pointsV = new List<Point3d>();
            var veca = line.Delta.GetNormal();
            if (pointsNoExtend.Count == 0)
            {
                var pointsExtend = new Point3dCollection();
                line.IntersectWith(basePoly, Intersect.ExtendThis, pointsExtend, IntPtr.Zero, IntPtr.Zero);
                if (pointsExtend.Count == 0)
                {
                    return BoundStatus.Out;
                }
                else if (pointsExtend.Count == 1)
                {
                    return BoundStatus.Intersection;
                }
                else
                {
                    var a = pointsExtend[0];
                    var b = pointsExtend[pointsExtend.Count - 1];
                    var lineInter = new Line(a, b);
                    var pNear1 = lineInter.GetClosestPointTo(line.StartPoint, false);
                    var pNear2 = lineInter.GetClosestPointTo(line.EndPoint, false);
                    var b1 = pNear1.DistanceTo(line.StartPoint) < Tolerance.Global.EqualPoint;
                    var b2 = pNear2.DistanceTo(line.EndPoint) < Tolerance.Global.EqualPoint;
                    if (b1 && b2)
                    {
                        return BoundStatus.In;
                    }
                    else
                    {
                        return BoundStatus.Out;
                    }
                }
            }
            else
            {
                return BoundStatus.Intersection;
            }
        }
        public static double GetRecIntersectionArea(this Polyline baseRec, Polyline moveRec)
        {
            var polya = new Polyline();
            var points = GetRecIntersection(baseRec, moveRec);
            if (points.Count == 0 && points.Count == 1)
            {
                return 0;
            }
            else if (points.Count == 2)
            {
                return Math.Abs((points[0].X - points[1].X) * (points[0].Y - points[1].Y));
            }
            else if (points.Count == 3)
            {
                polya.AddVertexAt(polya.NumberOfVertices, points[0].Point3dTo2d(), 0, 0, 0);
                polya.AddVertexAt(polya.NumberOfVertices, points[1].Point3dTo2d(), 0, 0, 0);
                polya.AddVertexAt(polya.NumberOfVertices, points[2].Point3dTo2d(), 0, 0, 0);
                return polya.Area;
            }
            else if (points.Count == 4)
            {
                polya.AddVertexAt(polya.NumberOfVertices, points[0].Point3dTo2d(), 0, 0, 0);
                polya.AddVertexAt(polya.NumberOfVertices, points[1].Point3dTo2d(), 0, 0, 0);
                polya.AddVertexAt(polya.NumberOfVertices, points[2].Point3dTo2d(), 0, 0, 0);
                polya.AddVertexAt(polya.NumberOfVertices, points[3].Point3dTo2d(), 0, 0, 0);
                return polya.Area;
            }
            return 0;
        }
        #endregion
        public static Polyline ChangeRecPosition(this Polyline basePoly, Polyline moveRec,
            LineSegment3d segx, LineSegment3d segy, Vector3d range, double angle)
        {
            var state = basePoly.GetRecBoundState(moveRec);
            var positions = moveRec.GetRecPositions(segx, segy, range, angle);
            var polyOut = new Polyline();
            if (state == BoundStatus.Intersection)
            {
                foreach (var position in positions)
                {
                    var stateIn = position.Value.GetRecBoundState(moveRec);
                    if (stateIn == BoundStatus.Out && stateIn == BoundStatus.In)
                    {
                        polyOut = position.Value;
                        break;
                    }
                }
            }
            return polyOut;
        }
        public static Polyline GetRecGivenPosition(this Polyline baseRec, RecPosition position, Vector3d range, double angle)
        {
            var segx = baseRec.GetCenterLineFromRec()[0];
            var segy = baseRec.GetCenterLineFromRec()[1];
            var pointc = baseRec.GetRecCenterPoint();
            var mirrorX = baseRec.MirrorEnt(segx.StartPoint, segx.EndPoint);
            var mirrorY = baseRec.MirrorEnt(segy.StartPoint, segy.EndPoint);
            var mirrorXy = mirrorX.MirrorEnt(segy.StartPoint, segy.EndPoint);
            var distance = baseRec.MoveEnt(range);
            var rotation = baseRec.RotateEnt(pointc, angle);
            var poly = new Polyline();
            switch (position)
            {
                case RecPosition.MirrorX:
                    poly = mirrorX;
                    break;
                case RecPosition.MirrorY:
                    poly = mirrorY;
                    break;
                case RecPosition.MirrorXy:
                    poly = mirrorXy;
                    break;
                case RecPosition.Distance:
                    poly = distance;
                    break;
                case RecPosition.Rotation:
                    poly = rotation;
                    break;
            }
            return poly;
        }
        public static Dictionary<RecPosition, Polyline> GetRecPositions
            (this Polyline baseRec, LineSegment3d segx, LineSegment3d segy, Vector3d range, double angle = 0)
        {
            var pointc = baseRec.GetRecCenterPoint();
            var mirrorX = baseRec.MirrorEnt(segx.StartPoint, segx.EndPoint);
            var mirrorY = baseRec.MirrorEnt(segy.StartPoint, segy.EndPoint);
            var mirrorXy = mirrorX.MirrorEnt(segy.StartPoint, segy.EndPoint);
            var distance = baseRec.MoveEnt(range);
            var rotation = baseRec.RotateEnt(pointc, angle);
            var dic = new Dictionary<RecPosition, Polyline>();
            dic.Add(RecPosition.MirrorX, mirrorX);
            dic.Add(RecPosition.MirrorY, mirrorY);
            dic.Add(RecPosition.MirrorXy, mirrorXy);
            dic.Add(RecPosition.Distance, distance);
            dic.Add(RecPosition.Rotation, rotation);
            return dic;
        }
    }
}
