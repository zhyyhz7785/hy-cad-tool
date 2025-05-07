//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//namespace HyCADTool.HelpClass
//{
//    public static class GeometryExtensions
//    {
//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
//        {
//            // 计算两墙体之间的夹角（单位：度）
//            double angle = CalculateAngleBetweenWalls(q1, q2);
//            // 定义交点 p1，假设 q1 的终点与 q2 的起点重合
//            Point3d p1 = q1.EndPoint;  // 交点，连接处的基础点
//            // 计算 q1 的方向向量和法向量（向外侧偏移）
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;           // q1 的方向向量，从起点指向终点
//            Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q1 的法向量，顺时针旋转90度（向外侧）
//            Point3d p2 = p1 + normal1 * thickness1;                // q1 在交点处的外侧点，向法向量方向偏移墙厚
//            // 计算 q2 的方向向量和法向量（向外侧偏移）
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;           // q2 的方向向量，从起点指向终点
//            Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q2 的法向量，顺时针旋转90度（向外侧）
//            Point3d p4 = p1 + normal2 * thickness2;                // q2 在交点处的外侧点，向法向量方向偏移墙厚
//            // 计算外侧平行线的交点 p3
//            Point3dCollection intersectionPoints = new Point3dCollection(); // 存储交点的集合
//            Line parallel1 = new Line(p2, p2 + dir1);              // q1 外侧的平行线，从 p2 沿 q1 方向延伸
//            Line parallel2 = new Line(p4, p4 + dir2);              // q2 外侧的平行线，从 p4 沿 q2 方向延伸
//            parallel1.IntersectWith(parallel2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero); // 计算两平行线的交点
//            Point3d p3 = intersectionPoints.Count > 0 ? intersectionPoints[0] : p2; // 如果有交点，取第一个；否则默认使用 p2
//            // 根据夹角范围处理连接样式
//            if (angle >= 0 && angle <= 180) // 情况 1：夹角在 0°~180°（外侧弯折）
//            {
//                if (thickness1 == thickness2) // 子情况 1.1：墙厚相同
//                {
//                    // 创建 L 形连接区域
//                    Polyline lShape = new Polyline();
//                    lShape.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0); // 交点
//                    lShape.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0); // q1 外侧点
//                    lShape.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0); // 外侧平行线交点
//                    lShape.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0); // q2 外侧点
//                    lShape.Closed = true;                                    // 确保闭合
//                    // 将 L 形区域与 q1 和 q2 的缓冲区域合并
//                    qb1 = UnionPolylines(qb1, lShape); // 更新 q1 的缓冲区域
//                    qb2 = UnionPolylines(qb2, lShape); // 更新 q2 的缓冲区域
//                }
//                else // 子情况 1.2：墙厚不同
//                {
//                    // 创建三角形连接区域
//                    Polyline triangle = new Polyline();
//                    triangle.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0); // 交点
//                    triangle.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0); // q1 外侧点
//                    triangle.AddVertexAt(2, new Point2d(p4.X, p4.Y), 0, 0, 0); // q2 外侧点
//                    triangle.Closed = true;                                    // 确保闭合
//                    // 将三角形区域与 q1 和 q2 的缓冲区域合并
//                    qb1 = UnionPolylines(qb1, triangle); // 更新 q1 的缓冲区域
//                    qb2 = UnionPolylines(qb2, triangle); // 更新 q2 的缓冲区域
//                }
//            }
//            else  // 情况 2：夹角在 0°~-180°（内侧弯折）
//            {
//                // 调整 q1 和 q2 的缓冲区域，将外侧点移动到交点 p3
//                MovePointInPolyline(qb1, p2, p3); // 将 q1 的外侧点 p2 移动到 p3
//                MovePointInPolyline(qb2, p4, p3); // 将 q2 的外侧点 p4 移动到 p3
//            }
//        }
//        public static Polyline CreateWallBuffer(Line wall, double thickness)
//        {
//            Point3d start = wall.StartPoint;
//            Point3d end = wall.EndPoint;
//            Vector3d direction = end - start;
//            Vector3d normal = direction.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            Point3d p1 = start + normal * thickness;  // 外侧
//            Point3d p2 = start;                       // 内侧
//            Point3d p3 = end;                         // 内侧
//            Point3d p4 = end + normal * thickness;    // 外侧
//            Polyline buffer = new Polyline();
//            buffer.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
//            buffer.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
//            buffer.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
//            buffer.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
//            buffer.Closed = true;
//            return buffer;
//        }
//        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
//        {
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            double angle = dir1.GetAngleTo(dir2);  // 弧度
//            return angle * (180 / Math.PI);        // 转换为度
//        }
//        public static Polyline UnionPolylines(Polyline poly1, Polyline poly2)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//                // 创建 Region1
//                var regions1 = Region.CreateFromCurves(new DBObjectCollection { poly1 });
//                var regions2 = Region.CreateFromCurves(new DBObjectCollection { poly2 });
//                if (regions1.Count == 0 || regions2.Count == 0)
//                {
//                    throw new InvalidOperationException("无法从 Polyline 创建 Region，可能存在几何错误。");
//                }
//                using (var region1 = regions1[0] as Region)
//                using (var region2 = regions2[0] as Region)
//                {
//                    if (region1 == null || region2 == null)
//                    {
//                        throw new InvalidOperationException("创建的 Region 无效。");
//                    }
//                    // 执行布尔并集操作
//                    region1.BooleanOperation(BooleanOperationType.BoolUnite, region2);
//                    // 转为 Polyline（请确保您已有实现 GetBoundaryPolyline 方法）
//                    Polyline result = GetBoundaryPolyline(region1);
//                    btr.AppendEntity(result);
//                    tr.AddNewlyCreatedDBObject(result, true);
//                    // 可选：释放 region1 和 region2（已由 using 自动处理）
//                    tr.Commit();
//                    return result;
//                }
//            }
//        }
//        public static void MovePointInPolyline(Polyline poly, Point3d fromPoint, Point3d toPoint)
//        {
//            for (int i = 0; i < poly.NumberOfVertices; i++)
//            {
//                if (poly.GetPoint3dAt(i).IsEqualTo(fromPoint, new Tolerance(0.001, 0.001)))
//                {
//                    poly.SetPointAt(i, new Point2d(toPoint.X, toPoint.Y));
//                    break;
//                }
//            }
//        }
//        public static Polyline GetBoundaryPolyline(Region region)
//        {
//            Polyline poly = new Polyline();
//            DBObjectCollection curves = new DBObjectCollection();
//            region.Explode(curves);
//            int vertexIndex = 0;
//            foreach (DBObject obj in curves)
//            {
//                if (obj is Line line)
//                {
//                    poly.AddVertexAt(vertexIndex++, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
//                }
//                // 如果需要处理弧线或其他曲线类型，可以在此扩展
//            }
//            poly.Closed = true;
//            return poly;
//        }
//    }
//}