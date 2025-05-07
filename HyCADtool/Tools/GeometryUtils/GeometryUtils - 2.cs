// 工具类文件：GeometryUtils.cs
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using System;
using System.Collections.Generic;

namespace HyCADTool.Tools
{
    public static partial class GeometryUtils
    {
        private const double Scale = 1000.0;

        public static Path64 ConvertToPath64(Polyline pline)
        {
            var path = new Path64();
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                var pt = pline.GetPoint2dAt(i);
                path.Add(new Point64((long)(pt.X * Scale), (long)(pt.Y * Scale)));
            }
            return path;
        }

        public static Polyline CreatePolylineFromPath64(Path64 path)
        {
            var pline = new Polyline();
            for (int i = 0; i < path.Count; i++)
            {
                var pt = path[i];
                pline.AddVertexAt(i, new Point2d(pt.X / Scale, pt.Y / Scale), 0, 0, 0);
            }
            pline.Closed = true;
            return pline;
        }

        public static Point3d GetCentroid(Polyline pline)
        {
            // 简化为平均值近似法（非真实质心）
            double x = 0, y = 0;
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                x += pline.GetPoint2dAt(i).X;
                y += pline.GetPoint2dAt(i).Y;
            }
            return new Point3d(x / pline.NumberOfVertices, y / pline.NumberOfVertices, 0);
        }

     

        public static Region CreateRegionFromPolyline(Polyline pline)
        {
            DBObjectCollection curves = new DBObjectCollection();

            curves.Add(pline.Clone() as DBObject);
            DBObjectCollection regions = Region.CreateFromCurves(curves);
            if (regions.Count == 0) return null;
            return regions[0] as Region;
        }
      

       

        public static Polyline ConvertCurveToPolyline(Line line)
        {
            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
            pl.Closed = false;
            return pl;
        }

        public static Polyline JoinLinesToPolyline(List<Line> lines)
        {
            var sorted = SortLinesByConnectivity(lines);
            if (sorted.Count < 3) return null;

            Polyline result = new Polyline();
            for (int i = 0; i < sorted.Count; i++)
            {
                result.AddVertexAt(i, new Point2d(sorted[i].StartPoint.X, sorted[i].StartPoint.Y), 0, 0, 0);
            }
            result.Closed = true;
            return result;
        }

        public static List<Line> SortLinesByConnectivity(List<Line> inputLines)
        {
            if (inputLines == null || inputLines.Count == 0)
                return new List<Line>();

            var used = new HashSet<int>();
            var ordered = new List<Line>();
            var current = inputLines[0];
            ordered.Add(current);
            used.Add(0);

            while (ordered.Count < inputLines.Count)
            {
                bool found = false;
                Point3d end = current.EndPoint;

                for (int i = 0; i < inputLines.Count; i++)
                {
                    if (used.Contains(i)) continue;

                    Line candidate = inputLines[i];
                    if (candidate.StartPoint.IsEqualTo(end, new Tolerance(1e-4, 1e-4)))
                    {
                        ordered.Add(candidate);
                        used.Add(i);
                        current = candidate;
                        found = true;
                        break;
                    }
                    else if (candidate.EndPoint.IsEqualTo(end, new Tolerance(1e-4, 1e-4)))
                    {
                        Line reversed = new Line(candidate.EndPoint, candidate.StartPoint);
                        ordered.Add(reversed);
                        used.Add(i);
                        current = reversed;
                        found = true;
                        break;
                    }
                }

                if (!found) break;
            }

            return ordered;
        }

        //public static bool TryAlignOneEdgeToAnother(Polyline pline1, Polyline pline2, double maxDistance)
        //{
        //    int count1 = pline1.NumberOfVertices;
        //    int count2 = pline2.NumberOfVertices;

        //    for (int i = 0; i < count1; i++)
        //    {
        //        Point2d p1Start = pline1.GetPoint2dAt(i);
        //        Point2d p1End = pline1.GetPoint2dAt((i + 1) % count1);
        //        LineSegment2d seg1 = new LineSegment2d(p1Start, p1End);
        //        Vector2d dir1 = seg1.Direction;

        //        Point3d mid = new Point3d((p1Start.X + p1End.X) / 2, (p1Start.Y + p1End.Y) / 2, 0);
        //        if (!IsPointInside(pline2, mid))
        //            continue;

        //        for (int j = 0; j < count2; j++)
        //        {
        //            Point2d p2Start = pline2.GetPoint2dAt(j);
        //            Point2d p2End = pline2.GetPoint2dAt((j + 1) % count2);
        //            LineSegment2d seg2 = new LineSegment2d(p2Start, p2End);
        //            Vector2d dir2 = seg2.Direction;

        //            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
        //            if (Math.Abs(cross) > 1e-6) continue;

        //            double dist = seg2.GetDistanceTo(seg1.MidPoint);
        //            if (dist > maxDistance) continue;

        //            Vector2d move = seg2.MidPoint - seg1.MidPoint;
        //            pline1.TransformBy(Matrix3d.Displacement(new Vector3d(move.X, move.Y, 0)));
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        //public static bool TryAlignOneEdgeToAnother(Polyline pline1, Polyline pline2, double maxDistance)
        //{
        //    int count1 = pline1.NumberOfVertices;
        //    int count2 = pline2.NumberOfVertices;

        //    for (int i = 0; i < count1; i++)
        //    {
        //        Point2d p1Start = pline1.GetPoint2dAt(i);
        //        Point2d p1End = pline1.GetPoint2dAt((i + 1) % count1);
        //        LineSegment2d seg1 = new LineSegment2d(p1Start, p1End);
        //        Vector2d dir1 = seg1.Direction;

        //        Point3d mid = new Point3d((p1Start.X + p1End.X) / 2, (p1Start.Y + p1End.Y) / 2, 0);
        //        if (!IsPointInside(pline2, mid))
        //            continue;

        //        for (int j = 0; j < count2; j++)
        //        {
        //            Point2d p2Start = pline2.GetPoint2dAt(j);
        //            Point2d p2End = pline2.GetPoint2dAt((j + 1) % count2);
        //            LineSegment2d seg2 = new LineSegment2d(p2Start, p2End);
        //            Vector2d dir2 = seg2.Direction;

        //            // 使用手动叉积判断平行
        //            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
        //            if (Math.Abs(cross) > 1e-6) continue;

        //            double dist = seg2.GetDistanceTo(seg1.MidPoint);
        //            if (dist > maxDistance) continue;

        //            Vector2d move = seg2.MidPoint - seg1.MidPoint;
        //            pline1.TransformBy(Matrix3d.Displacement(new Vector3d(move.X, move.Y, 0)));
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        //public static bool TryAlignOneEdgeToAnother(Polyline pline1, Polyline pline2, double maxDistance)
        //{
        //    if (pline1.NumberOfVertices < 2 || pline2.NumberOfVertices < 2)
        //        return false;

        //    // ===== 固定 pline1 的第0条边 =====
        //    Point2d p1Start = pline1.GetPoint2dAt(0);
        //    Point2d p1End = pline1.GetPoint2dAt(1);
        //    LineSegment2d fixedSeg = new LineSegment2d(p1Start, p1End);
        //    Vector2d dir1 = fixedSeg.Direction;

        //    int count2 = pline2.Closed ? pline2.NumberOfVertices : pline2.NumberOfVertices - 1;

        //    for (int j = 0; j < count2; j++)
        //    {
        //        Point2d p2Start = pline2.GetPoint2dAt(j);
        //        Point2d p2End = pline2.GetPoint2dAt((j + 1) % pline2.NumberOfVertices);
        //        LineSegment2d seg2 = new LineSegment2d(p2Start, p2End);
        //        Vector2d dir2 = seg2.Direction;

        //        // 判断是否平行（叉积接近零）
        //        double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
        //        if (Math.Abs(cross) > 1e-6) continue;

        //        // 判断两个边的距离
        //        double dist = seg2.GetDistanceTo(fixedSeg.MidPoint);
        //        if (dist > maxDistance) continue;

        //        // 计算移动向量：中点对齐
        //        Vector2d move = seg2.MidPoint - fixedSeg.MidPoint;
        //        pline1.TransformBy(Matrix3d.Displacement(new Vector3d(move.X, move.Y, 0)));
        //        return true;
        //    }

        //    return false;
        //}
        //public static bool TryAlignOneEdgeToAnother(
        //    Polyline source, Polyline target, double maxDistance,
        //    bool requireSameDirection = false, Vector2d? originalDirection = null)
        //{
        //    int count1 = source.NumberOfVertices;
        //    int count2 = target.NumberOfVertices;

        //    for (int i = 0; i < count1; i++)
        //    {
        //        Point2d p1Start = source.GetPoint2dAt(i);
        //        Point2d p1End = source.GetPoint2dAt((i + 1) % count1);
        //        Vector2d dir1 = (p1End - p1Start).GetNormal();

        //        for (int j = 0; j < count2; j++)
        //        {
        //            Point2d p2Start = target.GetPoint2dAt(j);
        //            Point2d p2End = target.GetPoint2dAt((j + 1) % count2);
        //            Vector2d dir2 = (p2End - p2Start).GetNormal();

        //            if (Math.Abs(dir1.GetAngleTo(dir2)) > Math.PI / 180 * 10) // > 10°
        //                continue;

        //            if (requireSameDirection && originalDirection.HasValue)
        //            {
        //                double angle1 = dir1.GetAngleTo(originalDirection.Value);
        //                if (angle1 > Math.PI / 6) // 允许夹角不大于 30°
        //                    continue;
        //            }

        //            Point3d mid1 = new Point3d((p1Start.X + p1End.X) / 2, (p1Start.Y + p1End.Y) / 2, 0);
        //            Point3d mid2 = new Point3d((p2Start.X + p2End.X) / 2, (p2Start.Y + p2End.Y) / 2, 0);
        //            Vector3d moveVec = mid2 - mid1;

        //            if (moveVec.Length < maxDistance)
        //            {
        //                source.TransformBy(Matrix3d.Displacement(moveVec));
        //                return true;
        //            }
        //        }
        //    }

        //    return false;
        //}
        //public static bool TryAlignOneEdgeToAnother(Polyline source, Polyline target, double maxDistance)
        //{
        //    int sc = source.NumberOfVertices;
        //    int tc = target.NumberOfVertices;

        //    for (int i = 0; i < sc; i++)
        //    {
        //        Point2d s1 = source.GetPoint2dAt(i);
        //        Point2d s2 = source.GetPoint2dAt((i + 1) % sc);
        //        Vector2d dirS = (s2 - s1).GetNormal();

        //        for (int j = 0; j < tc; j++)
        //        {
        //            Point2d t1 = target.GetPoint2dAt(j);
        //            Point2d t2 = target.GetPoint2dAt((j + 1) % tc);
        //            Vector2d dirT = (t2 - t1).GetNormal();

        //            double angle = dirS.GetAngleTo(dirT);
        //            bool isParallel = Math.Abs(angle) < 10 * Math.PI / 180 || Math.Abs(angle - Math.PI) < 10 * Math.PI / 180;
        //            if (!isParallel) continue;

        //            // 尝试中点对齐
        //            Point3d midS = new Point3d((s1.X + s2.X) / 2, (s1.Y + s2.Y) / 2, 0);
        //            Point3d midT = new Point3d((t1.X + t2.X) / 2, (t1.Y + t2.Y) / 2, 0);
        //            Vector3d moveVecMid = midT - midS;

        //            if (moveVecMid.Length < maxDistance)
        //            {
        //                source.TransformBy(Matrix3d.Displacement(moveVecMid));
        //                return true;
        //            }

        //            // 尝试端点吸附对齐（4 种情况）
        //            var pairs = new[]
        //            {
        //        (from: s1, to: t1),
        //        (from: s1, to: t2),
        //        (from: s2, to: t1),
        //        (from: s2, to: t2)
        //    };

        //            foreach (var pair in pairs)
        //            {
        //                Vector3d moveVec = new Point3d(pair.to.X, pair.to.Y, 0) - new Point3d(pair.from.X, pair.from.Y, 0);
        //                if (moveVec.Length < maxDistance)
        //                {
        //                    source.TransformBy(Matrix3d.Displacement(moveVec));
        //                    return true;
        //                }
        //            }
        //        }
        //    }

        //    return false;
        //}
        public static bool TryAlignOneEdgeToAnother(Polyline source, Polyline target, double maxDistance)
        {
            int sc = source.NumberOfVertices;
            int tc = target.NumberOfVertices;

            for (int i = 0; i < sc; i++)
            {
                Point2d s1 = source.GetPoint2dAt(i);
                Point2d s2 = source.GetPoint2dAt((i + 1) % sc);
                Vector2d dirS = (s2 - s1).GetNormal();

                for (int j = 0; j < tc; j++)
                {
                    Point2d t1 = target.GetPoint2dAt(j);
                    Point2d t2 = target.GetPoint2dAt((j + 1) % tc);
                    Vector2d dirT = (t2 - t1).GetNormal();

                    double angle = dirS.GetAngleTo(dirT);
                    bool isParallel = Math.Abs(angle) < 10 * Math.PI / 180 || Math.Abs(angle - Math.PI) < 10 * Math.PI / 180;
                    if (!isParallel) continue;

                    // 尝试中点对齐
                    Point3d midS = new Point3d((s1.X + s2.X) / 2, (s1.Y + s2.Y) / 2, 0);
                    Point3d midT = new Point3d((t1.X + t2.X) / 2, (t1.Y + t2.Y) / 2, 0);
                    Vector3d moveVecMid = midT - midS;

                    if (moveVecMid.Length < maxDistance)
                    {
                        source.TransformBy(Matrix3d.Displacement(moveVecMid));
                        return true;
                    }

                    // 尝试端点吸附对齐（4 种情况）
                    var pairs = new[]
                    {
                (from: s1, to: t1),
                (from: s1, to: t2),
                (from: s2, to: t1),
                (from: s2, to: t2)
            };

                    foreach (var pair in pairs)
                    {
                        Vector3d moveVec = new Point3d(pair.to.X, pair.to.Y, 0) - new Point3d(pair.from.X, pair.from.Y, 0);
                        if (moveVec.Length < maxDistance)
                        {
                            source.TransformBy(Matrix3d.Displacement(moveVec));
                            return true;
                        }
                    }
                }
            }

            return false;
        }


    }
}
