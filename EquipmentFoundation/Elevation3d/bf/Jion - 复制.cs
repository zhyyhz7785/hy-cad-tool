//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//namespace HyCADTool.HelpClass
//{
//    public static class GeometryExtensions
//    {
//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
//        {
//            // 计算两墙体之间的夹角（单位：度，顺时针为正，逆时针为负）
//            double angle = CalculateAngleBetweenWalls(q1, q2);
//            // 定义关键点 (假设 q1 的终点与 q2 的起点重合)
//            Point3d p14 = q1.EndPoint;  // q1 终点 (交点)
//            Point3d p11 = q1.StartPoint; // q1 起点
//            // q1 的方向向量和法向量（顺时针方向向右侧偏移）
//            Vector3d dir1 = p14 - p11;           // q1 的方向向量
//            Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q1 法向量（右侧）
//            Point3d p12 = p11 + normal1 * thickness1; // q1 起点外侧点
//            Point3d p13 = p14 + normal1 * thickness1; // q1 终点外侧点
//            // q2 的关键点
//            Point3d p21 = q2.StartPoint; // q2 起点 (与 p14 重合)
//            Point3d p24 = q2.EndPoint;   // q2 终点
//            Vector3d dir2 = p24 - p21;           // q2 的方向向量
//            Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q2 法向量（右侧）
//            Point3d p22 = p21 + normal2 * thickness2; // q2 起点外侧点
//            Point3d p23 = p24 + normal2 * thickness2; // q2 终点外侧点
//            // 初始化 qb1 和 qb2 (buffer1 和 buffer2)
//            qb1 = CreateBufferPolyline(p11, p12, p13, p14);
//            qb2 = CreateBufferPolyline(p21, p22, p23, p24);
//            // 判断顺时针或逆时针
//            if (angle >= 0) // 顺时针方向
//            {
//                if (thickness1 == thickness2) // 墙厚相同
//                {
//                    // 类似于 Clipper2Lib 的 JoinType 处理
//                    Vector3d vec1 = normal1.Negate(); // q1 的反向法向量
//                    Vector3d vec2 = normal2;          // q2 的法向量
//                    double cosA = vec1.DotProduct(vec2) / (vec1.Length * vec2.Length);
//                    double sinA = vec1.CrossProduct(vec2).Length / (vec1.Length * vec2.Length);
//                    if (cosA > 0.999) // Miter (接近直线)
//                    {
//                        Point3d j1a, j1b;
//                        CalculateMiterPoints(p14, normal1, normal2, thickness1, out j1a, out j1b);
//                        Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                        qb1 = UnionPolylines(qb1, qj1);
//                        qb2 = UnionPolylines(qb2, qj1);
//                    }
//                    else if (cosA > -0.999) // Square
//                    {
//                        Point3d j1 = GetIntersectionPoint(p13, p12, p22, p23); // 延长线交点
//                        Polyline qj1 = CreateBufferPolyline(p14, p13, j1, p22);
//                        qb1 = UnionPolylines(qb1, qj1);
//                        qb2 = UnionPolylines(qb2, qj1);
//                    }
//                    else // Bevel
//                    {
//                        Polyline qj1 = CreateBufferPolyline(p14, p13, p22);
//                        qb1 = UnionPolylines(qb1, qj1);
//                        qb2 = UnionPolylines(qb2, qj1);
//                    }
//                }
//                else // 墙厚不同
//                {
//                    if (thickness1 > thickness2) // 厚墙在左侧 (q1)
//                    {
//                        Point3d? j2 = GetIntersectionPointNullable(p23, p22, p14, p13);
//                        if (j2.HasValue)
//                        {
//                            Polyline qj2 = CreateBufferPolyline(p14, j2.Value, p22);
//                            qb1 = UnionPolylines(qb1, qj2);
//                            qb2 = UnionPolylines(qb2, qj2);
//                        }
//                        else
//                        {
//                            // 同墙厚相同处理 (Bevel)
//                            Polyline qj1 = CreateBufferPolyline(p14, p13, p22);
//                            qb1 = UnionPolylines(qb1, qj1);
//                            qb2 = UnionPolylines(qb2, qj1);
//                        }
//                    }
//                    else // 厚墙在右侧 (q2)
//                    {
//                        Point3d? j3 = GetIntersectionPointNullable(p13, p12, p22, p21);
//                        if (j3.HasValue)
//                        {
//                            Polyline qj3 = CreateBufferPolyline(p14, p13, j3.Value);
//                            qb1 = UnionPolylines(qb1, qj3);
//                            qb2 = UnionPolylines(qb2, qj3);
//                        }
//                        else
//                        {
//                            // 同墙厚相同处理 (Bevel)
//                            Polyline qj1 = CreateBufferPolyline(p14, p13, p22);
//                            qb1 = UnionPolylines(qb1, qj1);
//                            qb2 = UnionPolylines(qb2, qj1);
//                        }
//                    }
//                }
//            }
//            else // 逆时针方向
//            {
//                if (thickness1 == thickness2) // 墙厚相同
//                {
//                    Vector3d vec1 = normal1.Negate(); // q1 的反向法向量
//                    Vector3d vec2 = normal2;          // q2 的法向量
//                    double cosA = vec1.DotProduct(vec2) / (vec1.Length * vec2.Length);
//                    if (cosA > 0.999) // Miter (接近直线)
//                    {
//                        Point3d j1a, j1b;
//                        CalculateMiterPoints(p14, normal1, normal2, thickness1, out j1a, out j1b);
//                        Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                        qb1 = UnionPolylines(qb1, qj1);
//                        qb2 = UnionPolylines(qb2, qj1);
//                    }
//                    else // Square
//                    {
//                        Point3d j1 = GetIntersectionPoint(p13, p12, p22, p23); // 延长线交点
//                        MovePointInPolyline(qb1, p13, j1);
//                        MovePointInPolyline(qb2, p22, j1);
//                    }
//                }
//                else // 墙厚不同
//                {
//                    if (thickness1 > thickness2) // 厚墙在左侧 (q1)
//                    {
//                        // 预留处理逻辑
//                    }
//                    else // 厚墙在右侧 (q2)
//                    {
//                        // 预留处理逻辑
//                    }
//                }
//            }
//        }
//        // 创建缓冲多段线
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
//        // 计算 Miter 连接点 (参考 Clipper2Lib)
//        private static void CalculateMiterPoints(Point3d basePoint, Vector3d normal1, Vector3d normal2, double thickness, out Point3d j1a, out Point3d j1b)
//        {
//            double q = thickness / (normal1.DotProduct(normal2) + 1);
//            j1a = basePoint + (normal1 + normal2) * q;
//            j1b = basePoint + (normal1 + normal2) * q; // 这里可以根据需要调整第二个点
//        }
//        // 获取两线段的交点
//        private static Point3d GetIntersectionPoint(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? intersections[0] : p1; // 默认返回 p1 如果无交点
//        }
//        // 获取两线段的交点（可为空）
//        private static Point3d? GetIntersectionPointNullable(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? (Point3d?)intersections[0] : null;
//        }
//        // 已有的方法保持不变
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
//                var regions1 = Region.CreateFromCurves(new DBObjectCollection { poly1 });
//                var regions2 = Region.CreateFromCurves(new DBObjectCollection { poly2 });
//                if (regions1.Count == 0 || regions2.Count == 0)
//                    throw new InvalidOperationException("无法从 Polyline 创建 Region，可能存在几何错误。");
//                using (var region1 = regions1[0] as Region)
//                using (var region2 = regions2[0] as Region)
//                {
//                    region1.BooleanOperation(BooleanOperationType.BoolUnite, region2);
//                    Polyline result = GetBoundaryPolyline(region1);
//                    btr.AppendEntity(result);
//                    tr.AddNewlyCreatedDBObject(result, true);
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
//            }
//            poly.Closed = true;
//            return poly;
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
//    }
//}