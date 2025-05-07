//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using Clipper2Lib;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace HyCADTool.HelpClass.CreatBase
//{
//    public static class GeometryExtensions
//    {
//        public static Clipper2Lib.JoinType JoinType { get; set; } = Clipper2Lib.JoinType.Miter;
//        public static Clipper2Lib.EndType EndType { get; set; } = Clipper2Lib.EndType.Polygon;

//        private static double _miterLimit = 2.0;
//        public static double MiterLimit
//        {
//            get => _miterLimit;
//            set => _miterLimit = value;
//        }

//        private const double ScaleFactor = 100000.0;

//        private static Path64 PolylineToPath64(Polyline poly)
//        {
//            Path64 path = new Path64();
//            for (int i = 0; i < poly.NumberOfVertices; i++)
//            {
//                Point3d point = poly.GetPoint3dAt(i);
//                path.Add(new Point64((long)(point.X * ScaleFactor), (long)(point.Y * ScaleFactor)));
//            }

//            if (poly.Closed && path[0] != path[path.Count - 1])
//                path.Add(path[0]);

//            return path;
//        }

//        private static Polyline Path64ToPolyline(Path64 path)
//        {
//            Polyline poly = new Polyline();
//            for (int i = 0; i < path.Count; i++)
//            {
//                var point = path[i];
//                poly.AddVertexAt(i, new Point2d(point.X / ScaleFactor, point.Y / ScaleFactor), 0, 0, 0);
//            }
//            poly.Closed = true;
//            return poly;
//        }

//        private static Path64 LineToPath64(Line line, double thickness)
//        {
//            Vector3d direction = line.EndPoint - line.StartPoint;
//            Vector3d normal = direction.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);

//            Point3d p1 = line.StartPoint;
//            Point3d p2 = line.StartPoint + normal * thickness;
//            Point3d p3 = line.EndPoint + normal * thickness;
//            Point3d p4 = line.EndPoint;

//            return new Path64 {
//                new Point64((long)(p1.X * ScaleFactor), (long)(p1.Y * ScaleFactor)),
//                new Point64((long)(p2.X * ScaleFactor), (long)(p2.Y * ScaleFactor)),
//                new Point64((long)(p3.X * ScaleFactor), (long)(p3.Y * ScaleFactor)),
//                new Point64((long)(p4.X * ScaleFactor), (long)(p4.Y * ScaleFactor)),
//                new Point64((long)(p1.X * ScaleFactor), (long)(p1.Y * ScaleFactor))
//            };
//        }

//        public static Polyline UnionPolylines(Polyline poly1, Polyline poly2)
//        {
//            Clipper64 clipper = new Clipper64();
//            clipper.AddSubject(PolylineToPath64(poly1));
//            clipper.AddClip(PolylineToPath64(poly2));

//            Paths64 solution = new Paths64();
//            if (clipper.Execute(ClipType.Union, FillRule.NonZero, solution) && solution.Count > 0)
//            {
//                Path64 largestPath = solution.OrderByDescending(p => Clipper.Area(p)).First();
//                return Path64ToPolyline(largestPath);
//            }

//            return poly1;
//        }

//        public static Polyline CreateWallBuffer(Line wall, double thickness)
//        {
//            return Path64ToPolyline(LineToPath64(wall, thickness));
//        }

//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
//        {
//            double angle = CalculateAngleBetweenWalls(q1, q2);
//            bool isParallel = Math.Abs(angle) < Tolerance.Global.EqualVector || Math.Abs(angle - 180) < Tolerance.Global.EqualVector;

//            if (isParallel)
//            {
//                Application.DocumentManager.MdiActiveDocument.Editor
//                    .WriteMessage($"\n检测到平行墙体 (角度: {angle:F2}度)，跳过连接处理\n");
//                return;
//            }

//            Paths64 paths = new Paths64 { LineToPath64(q1, thickness1), LineToPath64(q2, thickness2) };

//            Clipper64 clipper = new Clipper64();
//            clipper.AddSubject(paths);

//            Paths64 unionResult = new Paths64();
//            if (clipper.Execute(ClipType.Union, FillRule.NonZero, unionResult) && unionResult.Count > 0)
//            {
//                Path64 resultPath = unionResult.OrderByDescending(p => Clipper.Area(p)).First();
//                qb1 = qb2 = Path64ToPolyline(resultPath);
//            }
//        }

//        public static Polyline HandleEndpoint(WallData wall, EndType endType)
//        {
//            Path64 wallPath = LineToPath64(wall.Edge, wall.Thickness);

//            ClipperOffset offset = new ClipperOffset(MiterLimit);
//            Paths64 solution = new Paths64();

//            offset.AddPath(wallPath, JoinType, endType);
//            offset.Execute(wall.Thickness / 2 * ScaleFactor,solution);

//            if (solution.Count > 0)
//            {
//                Path64 resultPath = solution.OrderByDescending(p => Clipper.Area(p)).First();
//                return Path64ToPolyline(resultPath);
//            }

//            return Path64ToPolyline(wallPath);
//        }

//        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
//        {
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            return dir1.GetAngleTo(dir2) * (180 / Math.PI);
//        }
//    }
//}