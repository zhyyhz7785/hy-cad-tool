//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//namespace HyCADTool.HelpClass
//{
//    // 定义连接类型
//    public enum JoinType
//    {
//        Miter,   // 斜接
//        Square,  // 方接
//        Bevel,   // 斜切
//        Round    // 圆接（预留）
//    }
//    // 定义端点类型
//    public enum EndType
//    {
//        Polygon, // 多边形
//        Joined,  // 连接
//        Butt,    // 平端
//        Square,  // 方端
//        Round    // 圆端（预留）
//    }
//    public static class GeometryExtensions
//    {
//        // 属性：连接类型和端点类型
//        public static JoinType JoinType { get; set; } = JoinType.Square;
//        public static EndType EndType { get; set; } = EndType.Polygon;
//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2)
//        {
//            // 计算两墙体之间的夹角（单位：度，顺时针为正，逆时针为负）
//            double angle = CalculateAngleBetweenWalls(q1, q2);
//            // 定义 q1 的关键点
//            Point3d p11 = q1.StartPoint; // q1 起点
//            Point3d p14 = q1.EndPoint;   // q1 终点（交点）
//            Vector3d dir1 = p14 - p11;   // q1 方向向量
//            Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q1 右侧法向量
//            Point3d p12 = p11 + normal1 * thickness1; // q1 起点外侧点
//            Point3d p13 = p14 + normal1 * thickness1; // q1 终点外侧点
//            // 定义 q2 的关键点
//            Point3d p21 = q2.StartPoint; // q2 起点（与 p14 重合）
//            Point3d p24 = q2.EndPoint;   // q2 终点
//            Vector3d dir2 = p24 - p21;   // q2 方向向量
//            Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis); // q2 右侧法向量
//            Point3d p22 = p21 + normal2 * thickness2; // q2 起点外侧点
//            Point3d p23 = p24 + normal2 * thickness2; // q2 终点外侧点
//            // 预先计算所有可能用到的交点
//            // 顺时针方向，墙厚相同时的交点 J1（Square 连接）
//            Point3d? j1Clockwise = GetIntersectionPointNullable(p13, p13 + dir1, p22, p22 + dir2);
//            // 顺时针方向，墙厚不同时的交点 J2 和 J3
//            Point3d? j2 = GetIntersectionPointNullable(p23, p22, p14, p13); // 厚墙在左侧（q1）
//            Point3d? j3 = GetIntersectionPointNullable(p13, p12, p22, p21); // 厚墙在右侧（q2）
//            // 逆时针方向，墙厚相同时的交点 J1（Square 连接）
//            // 注意：逆时针方向的 J1 可能需要根据实际几何调整，这里先假设与顺时针相同
//            Point3d? j1CounterClockwise = j1Clockwise; // 简化处理，后续可调整
//            // 初始化缓冲多段线
//            qb1 = CreateBufferPolyline(p11, p12, p13, p14);
//            qb2 = CreateBufferPolyline(p21, p22, p23, p24);
//            // 判断顺时针或逆时针
//            bool isClockwise = angle >= 0;
//            if (isClockwise) // 顺时针方向
//            {
//                if (thickness1 == thickness2)
//                {
//                    HandleSameThicknessClockwise(p14, p13, p22, normal1, normal2, thickness1, j1Clockwise, ref qb1, ref qb2);
//                }
//                else
//                {
//                    HandleDifferentThicknessClockwise(thickness1, thickness2, p14, p13, p22, j2, j3, ref qb1, ref qb2);
//                }
//            }
//            else // 逆时针方向
//            {
//                if (thickness1 == thickness2)
//                {
//                    HandleSameThicknessCounterClockwise(p14, p13, p22, normal1, normal2, thickness1, j1CounterClockwise, ref qb1, ref qb2);
//                }
//                else
//                {
//                    // 墙厚不同的逆时针情况，默认 Bevel 处理
//                    Polyline qj = CreateBufferPolyline(p14, p13, p22);
//                    qb1 = UnionPolylines(qb1, qj);
//                    qb2 = UnionPolylines(qb2, qj);
//                }
//            }
//        }
//        private static void HandleSameThicknessClockwise(Point3d p14, Point3d p13, Point3d p22,
//    Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            Polyline qj3 = null;  // 在 switch 前声明 qj3
//            switch (JoinType)
//            {
//                case JoinType.Miter:
//                    Point3d j1a, j1b;
//                    CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
//                    Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                    qb1 = UnionPolylines(qb1, qj1);
//                    qb2 = UnionPolylines(qb2, qj1);
//                    break;
//                case JoinType.Square:
//                    if (j1.HasValue)
//                    {
//                        Polyline qj2 = CreateBufferPolyline(p14, p13, j1.Value, p22);
//                        qb1 = UnionPolylines(qb1, qj2);
//                        qb2 = UnionPolylines(qb2, qj2);
//                    }
//                    else
//                    {
//                        qj3 = CreateBufferPolyline(p14, p13, p22);  // 赋值而非声明
//                        qb1 = UnionPolylines(qb1, qj3);
//                        qb2 = UnionPolylines(qb2, qj3);
//                    }
//                    break;
//                case JoinType.Bevel:
//                    qj3 = CreateBufferPolyline(p14, p13, p22);  // 赋值而非声明
//                    qb1 = UnionPolylines(qb1, qj3);
//                    qb2 = UnionPolylines(qb2, qj3);
//                    break;
//                case JoinType.Round:
//                    // 预留 Round 处理
//                    break;
//            }
//        }
//        private static void HandleDifferentThicknessClockwise(double thickness1, double thickness2, Point3d p14, Point3d p13, Point3d p22, Point3d? j2, Point3d? j3, ref Polyline qb1, ref Polyline qb2)
//        {
//            if (thickness1 > thickness2) // q1 墙厚较大
//            {
//                Polyline qj = j2.HasValue ? CreateBufferPolyline(p14, j2.Value, p22) : CreateBufferPolyline(p14, p13, p22);
//                qb1 = UnionPolylines(qb1, qj);
//                qb2 = UnionPolylines(qb2, qj);
//            }
//            else // q2 墙厚较大
//            {
//                Polyline qj = j3.HasValue ? CreateBufferPolyline(p14, p13, j3.Value) : CreateBufferPolyline(p14, p13, p22);
//                qb1 = UnionPolylines(qb1, qj);
//                qb2 = UnionPolylines(qb2, qj);
//            }
//        }
//        private static void HandleSameThicknessCounterClockwise(Point3d p14, Point3d p13, Point3d p22, Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            switch (JoinType)
//            {
//                case JoinType.Miter:
//                    Point3d j1a, j1b;
//                    CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
//                    Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                    qb1 = UnionPolylines(qb1, qj1);
//                    qb2 = UnionPolylines(qb2, qj1);
//                    break;
//                case JoinType.Square:
//                    if (j1.HasValue)
//                    {
//                        MovePointInPolyline(qb1, p13, j1.Value);
//                        MovePointInPolyline(qb2, p22, j1.Value);
//                    }
//                    break;
//                case JoinType.Bevel:
//                    // 预留 Bevel 处理
//                    break;
//                case JoinType.Round:
//                    // 预留 Round 处理
//                    break;
//            }
//        }
//        /// <summary>
//        /// 创建缓冲多段线
//        /// </summary>
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
//        /// <summary>
//        /// 计算 Miter 连接点，参考 Clipper2Lib
//        /// </summary>
//        private static void CalculateMiterPoints(Point3d basePoint, Vector3d normal1, Vector3d normal2, double thickness, out Point3d j1a, out Point3d j1b)
//        {
//            double q = thickness / (DotProduct(normal1, normal2) + 1);
//            j1a = basePoint + (normal1 + normal2) * q;
//            j1b = j1a; // 对于简单情况，j1b 可与 j1a 相同，复杂情况可调整
//        }
//        /// <summary>
//        /// 获取两线段交点
//        /// </summary>
//        private static Point3d GetIntersectionPoint(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? intersections[0] : p1;
//        }
//        public static Point3d? GetIntersectionPointNullable(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? (Point3d?)intersections[0] : null;
//        }
//        /// <summary>
//        /// 判断多边形方向（顺时针为 true，逆时针为 false）
//        /// </summary>
//        public static bool IsPositive(Polyline poly)
//        {
//            double area = 0;
//            for (int i = 0; i < poly.NumberOfVertices; i++)
//            {
//                Point2d p1 = poly.GetPoint2dAt(i);
//                Point2d p2 = poly.GetPoint2dAt((i + 1) % poly.NumberOfVertices);
//                area += (p2.X - p1.X) * (p2.Y + p1.Y);
//            }
//            return area > 0;
//        }
//        /// <summary>
//        /// 反转多边形顶点顺序
//        /// </summary>
//        public static Polyline ReversePath(Polyline poly)
//        {
//            Polyline reversed = new Polyline();
//            for (int i = poly.NumberOfVertices - 1; i >= 0; i--)
//            {
//                reversed.AddVertexAt(reversed.NumberOfVertices, poly.GetPoint2dAt(i), 0, 0, 0);
//            }
//            reversed.Closed = poly.Closed;
//            return reversed;
//        }
//        /// <summary>
//        /// 计算向量的点积
//        /// </summary>
//        public static double DotProduct(Vector3d v1, Vector3d v2)
//        {
//            return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
//        }
//        /// <summary>
//        /// 计算向量的叉积（2D）
//        /// </summary>
//        public static double CrossProduct(Vector3d v1, Vector3d v2)
//        {
//            return v1.X * v2.Y - v1.Y * v2.X;
//        }
//        /// <summary>
//        /// 计算两墙体之间的夹角（单位：度）
//        /// </summary>
//        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
//        {
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            double angle = dir1.GetAngleTo(dir2); // 弧度
//            return angle * (180 / Math.PI);       // 转换为度
//        }
//        /// <summary>
//        /// 合并两个多段线
//        /// </summary>
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
//                    throw new InvalidOperationException("无法创建 Region，可能存在几何错误。");
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
//        /// <summary>
//        /// 移动多段线中的点
//        /// </summary>
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
//        /// <summary>
//        /// 从 Region 获取边界多段线
//        /// </summary>
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