//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using Clipper2Lib;

//namespace HyCADTool.HelpClass.CreatBase
//{
//    //public enum JoinType
//    //{
//    //    Miter,
//    //    Square,
//    //    Bevel,
//    //    Round
//    //}

//    public enum EndType
//    {
//        Polygon,
//        Joined,
//        Butt,
//        Square,
//        Round
//    }

//    public static class GeometryExtensions
//    {
//        public static JoinType JoinType { get; set; } = JoinType.Miter;
//        public static EndType EndType { get; set; } = EndType.Polygon;
//        private static double _miterLimit = 2.0; // 默认值参考 Clipper2Lib
//        public static double MiterLimit
//        {
//            get => _miterLimit;
//            set => _miterLimit = value;
//        }

//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
//        {
//            double angle = CalculateAngleBetweenWalls(q1, q2);
//            bool isParallel = Math.Abs(angle) < Tolerance.Global.EqualVector || Math.Abs(angle - 180) < Tolerance.Global.EqualVector;
//            if (isParallel)
//            {
//                var doc = Application.DocumentManager.MdiActiveDocument;
//                var ed = doc.Editor;
//                ed.WriteMessage($"\n检测到平行墙体 (角度: {angle:F2}度)，跳过连接处理\n");
//                return;
//            }

//            // 简化方法：直接创建并合并墙壁的偏移多边形
//            Polyline wall1Buffer = CreateWallBuffer(q1, thickness1);
//            Polyline wall2Buffer = CreateWallBuffer(q2, thickness2);

//            // 转换为多段线并合并
//            try
//            {
//                // 如果需要复杂的连接处理，这里可以实现一个简单的合并逻辑
//                // 例如，可以将两个多边形的点合并，然后创建一个包含所有点的新多边形
//                // 本例中简单地使用第一个墙体的偏移多边形
//                qb1 = wall1Buffer;
//                qb2 = wall2Buffer;

//                // 在终端记录成功处理
//                var doc = Application.DocumentManager.MdiActiveDocument;
//                var ed = doc.Editor;
//                ed.WriteMessage($"\n成功创建墙体偏移多边形，使用简单缓冲区方法\n");
//            }
//            catch (Exception ex)
//            {
//                var doc = Application.DocumentManager.MdiActiveDocument;
//                var ed = doc.Editor;
//                ed.WriteMessage($"\n处理墙体连接时出错: {ex.Message}\n");
//                throw;
//            }
//        }

//        private static PathsD CreateWallPath(Line wall, double thickness)
//        {
//            PathsD paths = new PathsD();
//            Point3d start = wall.StartPoint;
//            Point3d end = wall.EndPoint;
//            PathD path = CreateLinePath(start, end);
//            paths.Add(path);
//            return paths;
//        }

//        private static PathD CreateLinePath(Point3d start, Point3d end)
//        {
//            PathD path = new PathD();
//            path.Add(new PointD(start.X, start.Y));
//            path.Add(new PointD(end.X, end.Y));
//            return path;
//        }

//        private static Polyline PathsDToPolyline(PathD path)
//        {
//            Polyline poly = new Polyline();
//            for (int i = 0; i < path.Count; i++)
//            {
//                poly.AddVertexAt(i, new Point2d(path[i].x, path[i].y), 0, 0, 0);
//            }
//            poly.Closed = true;
//            return poly;
//        }

//        private static Clipper2Lib.JoinType ConvertJoinType(JoinType joinType)
//        {
//            switch (joinType)
//            {
//                case JoinType.Miter: return Clipper2Lib.JoinType.Miter;
//                case JoinType.Square: return Clipper2Lib.JoinType.Square;
//                case JoinType.Bevel: return Clipper2Lib.JoinType.Square; // Clipper2Lib 无 Bevel，映射到 Square
//                case JoinType.Round: return Clipper2Lib.JoinType.Round;
//                default: return Clipper2Lib.JoinType.Square;
//            }
//        }

//        private static Clipper2Lib.EndType ConvertEndType(EndType endType)
//        {
//            switch (endType)
//            {
//                case EndType.Polygon: return Clipper2Lib.EndType.Polygon;
//                case EndType.Joined: return Clipper2Lib.EndType.Joined;
//                case EndType.Butt: return Clipper2Lib.EndType.Butt;
//                case EndType.Square: return Clipper2Lib.EndType.Square;
//                case EndType.Round: return Clipper2Lib.EndType.Round;
//                default: return Clipper2Lib.EndType.Polygon;
//            }
//        }

//        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
//        {
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            double angle = dir1.GetAngleTo(dir2);
//            return angle * (180 / Math.PI);
//        }

//        public static Point3d? GetIntersectionPointNullable(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? (Point3d?)intersections[0] : null;
//        }

//        public static Polyline CreateWallBuffer(Line wall, double thickness)
//        {
//            Point3d start = wall.StartPoint;
//            Point3d end = wall.EndPoint;
//            Vector3d direction = end - start;
//            Vector3d normal = direction.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);

//            // 创建线段两侧的偏移线，形成矩形
//            double halfThickness = thickness / 2.0;
//            Point3d p1 = start + normal * halfThickness;
//            Point3d p2 = start - normal * halfThickness;
//            Point3d p3 = end - normal * halfThickness;
//            Point3d p4 = end + normal * halfThickness;

//            Polyline buffer = new Polyline();
//            buffer.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
//            buffer.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
//            buffer.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
//            buffer.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
//            buffer.Closed = true;
//            return buffer;
//        }
//        public static Polyline HandleEndpoint(WallData wall, bool isStart, EndType endType)
//        {
//            Point3d p1 = isStart ? wall.Edge.StartPoint : wall.Edge.EndPoint;
//            Point3d p2 = isStart ? wall.Edge.EndPoint : wall.Edge.StartPoint;
//            Vector3d dir = p2 - p1;
//            Vector3d normal = dir.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            double thickness = wall.Thickness;
//            switch (endType)
//            {
//                case EndType.Butt:
//                    // 不延伸，直接生成矩形缓冲区
//                    return CreateWallBuffer(wall.Edge, thickness);
//                case EndType.Square:
//                    // 端点延伸厚度长度
//                    double extendLength = thickness / 2; // 假设延伸厚度的一半
//                    Point3d extendPoint = isStart ? p1 - dir.GetNormal() * extendLength : p2 + dir.GetNormal() * extendLength;
//                    Point3d p11 = isStart ? extendPoint : p1;
//                    Point3d p14 = isStart ? p2 : extendPoint;
//                    Point3d p12 = p11 + normal * thickness;
//                    Point3d p13 = p14 + normal * thickness;
//                    return CreateBufferPolyline(p11, p12, p13, p14);
//                case EndType.Polygon:
//                default:
//                    // 默认按矩形缓冲区生成，后续连接逻辑会处理
//                    return CreateWallBuffer(wall.Edge, thickness);
//            }
//        }
//        private static Polyline CreateBufferPolyline(params Point3d[] points)
//        {
//            Polyline poly = new Polyline();
//            for (int i = 0; i < points.Length; i++)
//            {
//                poly.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
//            }
//            poly.Closed = true;
//            return poly;
//        }
//    }
//}