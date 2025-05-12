using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using System;
namespace HyCADTool.HelpClass.CreatBase
{
    public enum JoinType
    {
        Miter,
        Square,
        Bevel,
        Round
    }
    public enum EndType
    {
        Polygon,
        Joined,
        Butt,
        Square,
        Round
    }
    public static class GeometryExtensions
    {
        public static JoinType JoinType { get; set; } = JoinType.Square;
        public static EndType EndType { get; set; } = EndType.Polygon;
        // 添加 MiterLimit 属性和 _mitLimSqr 字段
        private static double _mitLimSqr;
        private static double _miterLimit = 2.0; // 默认值参考 Clipper2Lib
        public static double MiterLimit
        {
            get => _miterLimit;
            set
            {
                _miterLimit = value;
                _mitLimSqr = (value <= 1 ? 2.0 : 2.0 / (value * value)); // 参考 Clipper2Lib 计算公式
            }
        }
        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
        {
            double angle = CalculateAngleBetweenWalls(q1, q2);
            bool isParallel = Math.Abs(angle) < Tolerance.Global.EqualVector || Math.Abs(angle - 180) < Tolerance.Global.EqualVector;
            if (isParallel)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                ed.WriteMessage($"\n检测到平行墙体 (角度: {angle:F2}度)，跳过连接处理\n");
                return;
            }
            Point3d p11 = q1.StartPoint;
            Point3d p14 = q1.EndPoint;
            Vector3d dir1 = p14 - p11;
            Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
            Point3d p12 = p11 + normal1 * thickness1;
            Point3d p13 = p14 + normal1 * thickness1;
            Point3d p21 = q2.StartPoint;
            Point3d p24 = q2.EndPoint;
            Vector3d dir2 = p24 - p21;
            Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
            Point3d p22 = p21 + normal2 * thickness2;
            Point3d p23 = p24 + normal2 * thickness2;
            Point3d? j1Clockwise = GetIntersectionPointNullable(p13, p13 + dir1, p22, p22 + dir2);
            //Point3d? j2 = GetIntersectionPointNullable(p23, p22, p14, p13);
            //Point3d? j3 = GetIntersectionPointNullable(p13, p12, p22, p21);
            Point3d? j2 = j1Clockwise;
            Point3d? j3 = j1Clockwise;
            Point3d? j1CounterClockwise = j1Clockwise;
            bool isClockwise = angle >= 0;
            if (isClockwise)
            {
                if (thickness1 == thickness2)
                {
                    HandleSameThicknessClockwise(p14, p13, p22, normal1, normal2, thickness1, j1Clockwise, ref qb1, ref qb2);
                }
                else
                {
                    HandleDifferentThicknessClockwise(thickness1, thickness2, p14, p13, p22, j2, j3, ref qb1, ref qb2);
                }
            }
            else
            {
                if (thickness1 == thickness2)
                {
                    HandleSameThicknessCounterClockwise(p14, p13, p22, normal1, normal2, thickness1, j1CounterClockwise, ref qb1, ref qb2);
                }
                else
                {
                    Polyline qj = CreateBufferPolyline(p14, p13, p22);
                    qb1 = UnionPolylines(qb1, qj);
                    qb2 = UnionPolylines(qb2, qj);
                }
            }
        }
        public static Polyline HandleEndpoint(WallData wall, bool isStart, EndType endType)
        {
            Point3d p1 = isStart ? wall.Edge.StartPoint : wall.Edge.EndPoint;
            Point3d p2 = isStart ? wall.Edge.EndPoint : wall.Edge.StartPoint;
            Vector3d dir = p2 - p1;
            Vector3d normal = dir.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
            double thickness = wall.Thickness;
            switch (endType)
            {
                case EndType.Butt:
                    // 不延伸，直接生成矩形缓冲区
                    return CreateWallBuffer(wall.Edge, thickness);
                case EndType.Square:
                    // 端点延伸厚度长度
                    double extendLength = thickness / 2; // 假设延伸厚度的一半
                    Point3d extendPoint = isStart ? p1 - dir.GetNormal() * extendLength : p2 + dir.GetNormal() * extendLength;
                    Point3d p11 = isStart ? extendPoint : p1;
                    Point3d p14 = isStart ? p2 : extendPoint;
                    Point3d p12 = p11 + normal * thickness;
                    Point3d p13 = p14 + normal * thickness;
                    return CreateBufferPolyline(p11, p12, p13, p14);
                case EndType.Polygon:
                default:
                    // 默认按矩形缓冲区生成，后续连接逻辑会处理
                    return CreateWallBuffer(wall.Edge, thickness);
            }
        }
        private static void HandleSameThicknessClockwise(Point3d p14, Point3d p13, Point3d p22,
            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
        {
            Polyline qj3 = null;
            switch (JoinType)
            {
                case JoinType.Miter:
                    Point3d j1a, j1b;
                    CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
                    // 检查斜接是否超出限制
                    double cosA = DotProduct(normal1, normal2) / (normal1.Length * normal2.Length);
                    if (cosA > _mitLimSqr - 1) // 如果未超出限制，使用 Miter
                    {
                        Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
                        // qb1 = UnionPolylines(qb1, qj1);
                        qb2 = UnionPolylines(qb2, qj1);
                    }
                    else // 超出限制，回退到 Square
                    {
                        if (j1.HasValue)
                        {
                            Polyline qj2 = CreateBufferPolyline(p14, p13, j1.Value, p22);
                            qb1 = UnionPolylines(qb1, qj2);
                            qb2 = UnionPolylines(qb2, qj2);
                        }
                        else
                        {
                            qj3 = CreateBufferPolyline(p14, p13, p22);
                            qb1 = UnionPolylines(qb1, qj3);
                            qb2 = UnionPolylines(qb2, qj3);
                        }
                    }
                    break;
                case JoinType.Square:
                    if (j1.HasValue)
                    {
                        Polyline qj2 = CreateBufferPolyline(p14, p13, j1.Value, p22);
                        qb1 = UnionPolylines(qb1, qj2);
                        qb2 = UnionPolylines(qb2, qj2);
                    }
                    else
                    {
                        qj3 = CreateBufferPolyline(p14, p13, p22);
                        qb1 = UnionPolylines(qb1, qj3);
                        qb2 = UnionPolylines(qb2, qj3);
                    }
                    break;
                case JoinType.Bevel:
                    qj3 = CreateBufferPolyline(p14, p13, p22);
                    qb1 = UnionPolylines(qb1, qj3);
                    qb2 = UnionPolylines(qb2, qj3);
                    break;
                case JoinType.Round:
                    // 预留 Round 处理
                    break;
            }
        }
        private static void HandleDifferentThicknessClockwise(double thickness1, double thickness2, Point3d p14,
            Point3d p13, Point3d p22, Point3d? j2, Point3d? j3, ref Polyline qb1, ref Polyline qb2)
        {
            if (thickness1 > thickness2)
            {
                Polyline qj = j2.HasValue ? CreateBufferPolyline(p14, p13, j2.Value, p22) : CreateBufferPolyline(p14, p13, p22);
                qb1 = UnionPolylines(qb1, qj);
                qb2 = UnionPolylines(qb2, qj);
            }
            else
            {
                Polyline qj = j3.HasValue ? CreateBufferPolyline(p14, p13, j3.Value, p22) : CreateBufferPolyline(p14, p13, p22);
                qb1 = UnionPolylines(qb1, qj);
                qb2 = UnionPolylines(qb2, qj);
            }
        }
        private static void HandleSameThicknessCounterClockwise(Point3d p14, Point3d p13, Point3d p22,
            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
        {
            switch (JoinType)
            {
                case JoinType.Miter:
                    Point3d j1a, j1b;
                    CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
                    double cosA = DotProduct(normal1, normal2) / (normal1.Length * normal2.Length);
                    if (cosA > _mitLimSqr - 1)
                    {
                        Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
                        qb1 = UnionPolylines(qb1, qj1);
                        qb2 = UnionPolylines(qb2, qj1);
                    }
                    else
                    {
                        if (j1.HasValue)
                        {
                            MovePointInPolyline(qb1, p13, j1.Value);
                            MovePointInPolyline(qb2, p22, j1.Value);
                        }
                    }
                    break;
                case JoinType.Square:
                    if (j1.HasValue)
                    {
                        MovePointInPolyline(qb1, p13, j1.Value);
                        MovePointInPolyline(qb2, p22, j1.Value);
                    }
                    break;
                case JoinType.Bevel:
                    // 预留 Bevel 处理
                    break;
                case JoinType.Round:
                    // 预留 Round 处理
                    break;
            }
        }
        private static Polyline CreateBufferPolyline(params Point3d[] points)
        {
            Polyline poly = new Polyline();
            for (int i = 0; i < points.Length; i++)
            {
                poly.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            poly.Closed = true;
            return poly;
        }
        private static void CalculateMiterPoints(Point3d basePoint, Vector3d normal1, Vector3d normal2, double thickness, out Point3d j1a, out Point3d j1b)
        {
            double q = thickness / (DotProduct(normal1, normal2) + 1);
            j1a = basePoint + (normal1 + normal2) * q;
            j1b = j1a; // 简单情况，j1b 与 j1a 相同
        }
        public static Point3d? GetIntersectionPointNullable(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
        {
            Point3dCollection intersections = new Point3dCollection();
            Line line1 = new Line(p1, p2);
            Line line2 = new Line(p3, p4);
            line1.IntersectWith(line2, Intersect.ExtendArgument, intersections, IntPtr.Zero, IntPtr.Zero);
            return intersections.Count > 0 ? (Point3d?)intersections[0] : null;
        }
        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
        {
            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
            double angle = dir1.GetAngleTo(dir2);
            return angle * (180 / Math.PI);
        }
        public static double DotProduct(Vector3d v1, Vector3d v2)
        {
            return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
        }
        //public static Polyline UnionPolylines(Polyline poly1, Polyline poly2)
        //{
        //    var doc = Application.DocumentManager.MdiActiveDocument;
        //    var db = doc.Database;
        //    using (var tr = db.TransactionManager.StartTransaction())
        //    {
        //        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        //        var regions1 = Region.CreateFromCurves(new DBObjectCollection { poly1 });
        //        var regions2 = Region.CreateFromCurves(new DBObjectCollection { poly2 });
        //        if (regions1.Count == 0 || regions2.Count == 0)
        //            throw new InvalidOperationException("无法创建 Region，可能存在几何错误。");
        //        using (var region1 = regions1[0] as Region)
        //        using (var region2 = regions2[0] as Region)
        //        {
        //            region1.BooleanOperation(BooleanOperationType.BoolUnite, region2);
        //            Polyline result = GetBoundaryPolyline(region1);
        //            btr.AppendEntity(result);
        //            tr.AddNewlyCreatedDBObject(result, true);
        //            tr.Commit();
        //            return result;
        //        }
        //    }
        //}
        public static void MovePointInPolyline(Polyline poly, Point3d fromPoint, Point3d toPoint)
        {
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                if (poly.GetPoint3dAt(i).IsEqualTo(fromPoint, new Tolerance(0.001, 0.001)))
                {
                    poly.SetPointAt(i, new Point2d(toPoint.X, toPoint.Y));
                    break;
                }
            }
        }
        public static Polyline GetBoundaryPolyline(Region region)
        {
            Polyline poly = new Polyline();
            DBObjectCollection curves = new DBObjectCollection();
            region.Explode(curves);
            int vertexIndex = 0;
            foreach (DBObject obj in curves)
            {
                if (obj is Line line)
                {
                    poly.AddVertexAt(vertexIndex++, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                }
            }
            poly.Closed = true;
            return poly;
        }
        public static Polyline CreateWallBuffer(Line wall, double thickness)
        {
            Point3d start = wall.StartPoint;
            Point3d end = wall.EndPoint;
            Vector3d direction = end - start;
            Vector3d normal = direction.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
            Point3d p1 = start + normal * thickness;  // 外侧
            Point3d p2 = start;                       // 内侧
            Point3d p3 = end;                         // 内侧
            Point3d p4 = end + normal * thickness;    // 外侧
            Polyline buffer = new Polyline();
            buffer.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            buffer.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
            buffer.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
            buffer.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
            buffer.Closed = true;
            return buffer;
        }
        //public static Polyline UnionPolylines(Polyline poly1, Polyline poly2)
        //{
        //    // 1. 将 AutoCAD Polyline 转换为 Clipper2Lib 的 Paths64
        //    Paths64 path1 = ConvertPolylineToPath(poly1);
        //    Paths64 path2 = ConvertPolylineToPath(poly2);
        //    // 2. 设置 Clipper2Lib 执行器
        //    Clipper64 clipper = new Clipper64();
        //    // 3. 添加主体和裁剪路径
        //    clipper.AddSubject(path1);
        //    clipper.AddClip(path2);
        //    // 4. 执行并集操作
        //    Paths64 solution = new Paths64();
        //    clipper.Execute(ClipType.Union, FillRule.NonZero, solution);
        //    // 5. 将结果转换回 AutoCAD Polyline
        //    var doc = Application.DocumentManager.MdiActiveDocument;
        //    var db = doc.Database;
        //    using (var tr = db.TransactionManager.StartTransaction())
        //    {
        //        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        //        Polyline result = ConvertPathToPolyline(solution[0]); // 假设取第一个结果路径
        //        btr.AppendEntity(result);
        //        tr.AddNewlyCreatedDBObject(result, true);
        //        tr.Commit();
        //        return result;
        //    }
        //}
        public static Polyline UnionPolylines(Polyline poly1, Polyline poly2)
        {
            // 1. 将 AutoCAD Polyline 转换为 Clipper2Lib 的 Paths64
            Paths64 path1 = ConvertPolylineToPath(poly1);
            Paths64 path2 = ConvertPolylineToPath(poly2);
            // 2. 设置 Clipper2Lib 执行器
            Clipper64 clipper = new Clipper64();
            // 3. 添加主体和裁剪路径
            clipper.AddSubject(path1);
            clipper.AddClip(path2);
            // 4. 执行并集操作
            Paths64 solution = new Paths64();
            clipper.Execute(ClipType.Union, FillRule.NonZero, solution);
            // 5. 将结果转换回 Polyline，不直接输入到 AutoCAD
            if (solution.Count > 0)
            {
                Polyline result = ConvertPathToPolyline(solution[0]); // 假设取第一个结果路径
                return result;
            }
            else
            {
                throw new InvalidOperationException("多边形合并失败，无法生成有效结果");
            }
        }
        private static Paths64 ConvertPolylineToPath(Polyline poly)
        {
            Paths64 path = new Paths64();
            Path64 points = new Path64();
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                Point3d pt = poly.GetPoint3dAt(i);
                long x = (long)(pt.X * 10000);
                long y = (long)(pt.Y * 10000);
                points.Add(new Point64(x, y));
            }
            // 使用 Count - 1 替代 ^1
            if (poly.Closed && points.Count > 0 && points[0] != points[points.Count - 1])
            {
                points.Add(points[0]);
            }
            path.Add(points);
            return path;
        }
        private static Polyline ConvertPathToPolyline(Path64 path)
        {
            Polyline poly = new Polyline();
            for (int i = 0; i < path.Count; i++)
            {
                Point64 pt = path[i];
                double x = pt.X / 10000.0;
                double y = pt.Y / 10000.0;
                poly.AddVertexAt(i, new Point2d(x, y), 0, 0, 0);
            }
            poly.Closed = true;
            return poly;
        }
    }
}