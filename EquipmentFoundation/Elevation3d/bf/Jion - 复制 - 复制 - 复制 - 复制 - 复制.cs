//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Tools;
//using NetTopologySuite.Operation.Buffer;
//using System;
//using System.Collections.Generic;
//using System.Windows.Controls;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public enum JoinType
//    {
//        Miter,
//        Square,
//        Bevel,
//        Round,
//        NullUnion // 新增选项，不进行任何处理
//    }
//    public enum EndType
//    {
//        Butt,    // 端点不做任何处理
//        Square,  // 端点延长墙体厚度长度
//        Polygon  // 默认，按闭环多边形连接逻辑处理
//    }
//    public static class GeometryExtensions
//    {
//        public static JoinType JoinType { get; set; } = JoinType.Miter;
//      //  public static EndType EndType { get; set; } = EndType.Polygon;
//        private static double _mitLimSqr;
//        private static double _miterLimit = 2.0; // 默认值参考 Clipper2Lib
//        public static double MiterLimit
//        {
//            get => _miterLimit;
//            set
//            {
//                _miterLimit = value;
//                _mitLimSqr = (value <= 1 ? 2.0 : 2.0 / (value * value)); // 参考 Clipper2Lib 计算公式
//            }
//        }
//        /**
// * 处理墙体连接的方法
// * 根据两墙体的几何关系和属性，调整它们的缓冲区多段线以实现正确的连接
// * @param q1 前一个墙体的边线
// * @param q2 当前墙体的边线
// * @param thickness1 前一个墙体的厚度
// * @param thickness2 当前墙体的厚度
// * @param qb1 前一个墙体的缓冲区多段线（可能被修改）
// * @param qb2 当前墙体的缓冲区多段线（可能被修改）
// * @param currentWall 当前墙体数据
// * @param allWalls 所有墙体列表
// * @param currentIndex 当前墙体索引
// * @param skipCurrentEdge 输出参数，指示是否应该跳过当前边的处理
// */
//        /**
// * 处理墙体连接的方法
// * 根据两墙体的几何关系和属性，调整它们的缓冲区多段线以实现正确的连接
// * @param q1 前一个墙体的边线
// * @param q2 当前墙体的边线
// * @param thickness1 前一个墙体的厚度
// * @param thickness2 当前墙体的厚度
// * @param qb1 前一个墙体的缓冲区多段线（可能被修改）
// * @param qb2 当前墙体的缓冲区多段线（可能被修改）
// * @param currentWall 当前墙体数据
// * @param allWalls 所有墙体列表
// * @param currentIndex 当前墙体索引
// * @param skipCurrentEdge 输出参数，指示是否应该跳过当前边的处理
// */
//        public static void HandleWallConnection(Line q1, Line q2, double thickness1, double thickness2,
//    ref Polyline qb1, ref Polyline qb2, WallData currentWall, List<WallData> allWalls, int currentIndex)
//        {
//            Point3d p11, p12, p13, p14;
//            Point3d p21, p22, p23, p24;
//            Vector3d normal1, normal2;
//            // 计算 q1 和 q2 的方向向量
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            // 计算夹角（弧度）
//            double angle = dir1.GetAngleTo(dir2);
//            // 判断是否共线（夹角接近 0 或 π 弧度，容差为 1e-6）
//            bool isCollinear = Math.Abs(angle) < 1e-6 || Math.Abs(angle - Math.PI) < 1e-6;
//            // 调试信息
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ed = doc.Editor;
//            ed.WriteMessage($"\nIsCollinear: {isCollinear}, CoincidentEdge: {currentWall.Boundary.CoincidentEdge != null}\n");
//            // 当共线且 CoincidentEdge 不为 null 时，生成缓冲区但不进行连接处理
//            if (isCollinear && currentWall.Boundary.CoincidentEdge != null)
//            {
//                ed.WriteMessage("\nGenerating buffers without junction for collinear walls.\n");
//                // 计算第一个墙体的四个顶点坐标
//                p11 = q1.StartPoint;
//                p14 = q1.EndPoint;
//                normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//                p12 = p11 + normal1 * thickness1;
//                p13 = p14 + normal1 * thickness1;
//                // 计算第二个墙体的四个顶点坐标
//                p21 = q2.StartPoint;
//                p24 = q2.EndPoint;
//                normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//                p22 = p21 + normal2 * thickness2;
//                p23 = p24 + normal2 * thickness2;
//                // 创建缓冲区多段线
//                qb1 = CreateBufferPolyline(p11, p12, p13, p14);
//                qb2 = CreateBufferPolyline(p21, p22, p23, p24);
//                return; // 生成缓冲区后返回，不进行连接处理
//            }
//            // 不共线或 CoincidentEdge 为 null，继续原有逻辑
//            double connectionAngle = CalculateAngleBetweenWalls(q1, q2);
//            // 计算第一个墙体的四个顶点坐标
//            p11 = q1.StartPoint;
//            p14 = q1.EndPoint;
//            normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            p12 = p11 + normal1 * thickness1;
//            p13 = p14 + normal1 * thickness1;
//            // 计算第二个墙体的四个顶点坐标
//            p21 = q2.StartPoint;
//            p24 = q2.EndPoint;
//            normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            p22 = p21 + normal2 * thickness2;
//            p23 = p24 + normal2 * thickness2;
//            // 计算交点（示例，具体实现可能需要调整）
//            Point3d? j1Clockwise = GetIntersectionPointNullable(p13, p13 + dir1, p22, p22 + dir2);
//            // 创建缓冲区多段线
//            qb1 = CreateBufferPolyline(p11, p12, p13, p14);
//            qb2 = CreateBufferPolyline(p21, p22, p23, p24);
//            // 根据角度判断是顺时针还是逆时针连接
//            bool isClockwise = connectionAngle >= 0;
//            // 根据连接方向和墙体厚度选择不同的处理方法
//            if (isClockwise)
//            {
//                if (thickness1 == thickness2)
//                {
//                    HandleSameThicknessClockwise(p14, p13, p22, normal1, normal2, thickness1, j1Clockwise, ref qb1, ref qb2);
//                }
//                else
//                {
//                    HandleDifferentThicknessClockwise(thickness1, thickness2, p14, p13, p22, j1Clockwise, null, ref qb1, ref qb2);
//                }
//            }
//            else
//            {
//                if (thickness1 == thickness2)
//                {
//                    HandleSameThicknessCounterClockwise(p14, p13, p22, normal1, normal2, thickness1, j1Clockwise, ref qb1, ref qb2);
//                }
//                else
//                {
//                    HandleDifferentThicknessCounterClockwise(p14, p13, p22, ref qb1, ref qb2);
//                }
//            }
//        }
//        // 共线墙体的处理方法（预留）
//        private static void HandleCollinearWalls(Line q1, Line q2, double thickness1, double thickness2,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            // TODO: 实现共线墙体的具体处理逻辑
//            // 例如：合并墙体缓冲区、处理重叠部分等
//            // 当前仅抛出异常，待后续实现
//            //throw new NotImplementedException("共线墙体的处理逻辑尚未实现");
//            return;
//        }
//        /**
//         * 只生成缓冲区而不处理连接的辅助方法
//         */
//        private static void GenerateBufferWithoutJunction(Line edge, double thickness, out Polyline buffer)
//        {
//            // 计算墙体的四个顶点坐标
//            Point3d p1 = edge.StartPoint;
//            Point3d p4 = edge.EndPoint;
//            Vector3d dir = p4 - p1;
//            Vector3d normal = dir.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            Point3d p2 = p1 + normal * thickness;
//            Point3d p3 = p4 + normal * thickness;
//            // 创建缓冲区多段线
//            buffer = CreateBufferPolyline(p1, p2, p3, p4);
//        }
//        private static void HandleSameThicknessClockwise(Point3d p14, Point3d p13, Point3d p22,
//            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            switch (JoinType)
//            {
//                case JoinType.Miter:
//                    DoMiterClockwise(p14, p13, p22, normal1, normal2, thickness, j1, ref qb1, ref qb2);
//                    break;
//                case JoinType.Square:
//                    DoSquareClockwise(p14, p13, p22, j1, ref qb1, ref qb2);
//                    break;
//                case JoinType.Bevel:
//                    DoBevelClockwise(p14, p13, p22, ref qb1, ref qb2);
//                    break;
//                case JoinType.Round:
//                    DoRoundClockwise(p14, p13, p22, thickness, ref qb1, ref qb2); // 预留
//                    break;
//                case JoinType.NullUnion:
//                    // 不做任何处理
//                    break;
//            }
//        }
//        private static void HandleDifferentThicknessClockwise(double thickness1, double thickness2, Point3d p14,
//            Point3d p13, Point3d p22, Point3d? j2, Point3d? j3, ref Polyline qb1, ref Polyline qb2)
//        {
//            if (JoinType == JoinType.NullUnion)
//            {
//                return; // 不做任何处理
//            }
//            if (thickness1 > thickness2)
//            {
//                DoThickLeftClockwise(p14, p13, p22, j2, ref qb1, ref qb2);
//            }
//            else
//            {
//                DoThickRightClockwise(p14, p13, p22, j3, ref qb1, ref qb2);
//            }
//        }
//        private static void HandleSameThicknessCounterClockwise(Point3d p14, Point3d p13, Point3d p22,
//            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            switch (JoinType)
//            {
//                case JoinType.Miter:
//                    DoMiterCounterClockwise(p14, p13, p22, normal1, normal2, thickness, j1, ref qb1, ref qb2);
//                    break;
//                case JoinType.Square:
//                    DoSquareCounterClockwise(p14, p13, p22, j1, ref qb1, ref qb2);
//                    break;
//                case JoinType.Bevel:
//                    DoBevelCounterClockwise(p14, p13, p22, ref qb1, ref qb2); // 预留
//                    break;
//                case JoinType.Round:
//                    DoRoundCounterClockwise(p14, p13, p22, thickness, ref qb1, ref qb2); // 预留
//                    break;
//                case JoinType.NullUnion:
//                    // 不做任何处理
//                    break;
//            }
//        }
//        private static void HandleDifferentThicknessCounterClockwise(Point3d p14, Point3d p13, Point3d p22,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            if (JoinType == JoinType.NullUnion)
//            {
//                return; // 不做任何处理
//            }
//            DoBevelCounterClockwise(p14, p13, p22, ref qb1, ref qb2); // 默认 Bevel 处理
//        }
//        // 顺时针 Miter
//        private static void DoMiterClockwise(Point3d p14, Point3d p13, Point3d p22,
//            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            Point3d j1a, j1b;
//            CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
//            double cosA = DotProduct(normal1, normal2) / (normal1.Length * normal2.Length);
//            if (cosA > _mitLimSqr - 1) // 未超出限制
//            {
//                Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                // qb1 不合并 qj1，仅 qb2 合并
//                qb2 = UnionPolylines(qb2, qj1);
//            }
//            else // 超出限制，回退到 Square
//            {
//                DoSquareClockwise(p14, p13, p22, j1, ref qb1, ref qb2);
//            }
//        }
//        // 顺时针 Square
//        private static void DoSquareClockwise(Point3d p14, Point3d p13, Point3d p22, Point3d? j1,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            if (j1.HasValue)
//            {
//                Polyline qj2 = CreateBufferPolyline(p14, p13, j1.Value, p22);
//                qb1 = UnionPolylines(qb1, qj2);
//                qb2 = UnionPolylines(qb2, qj2);
//            }
//            else
//            {
//                DoBevelClockwise(p14, p13, p22, ref qb1, ref qb2);
//            }
//        }
//        // 顺时针 Bevel
//        private static void DoBevelClockwise(Point3d p14, Point3d p13, Point3d p22,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            Polyline qj3 = CreateBufferPolyline(p14, p13, p22);
//            qb1 = UnionPolylines(qb1, qj3);
//            qb2 = UnionPolylines(qb2, qj3);
//        }
//        // 顺时针 Round（预留）
//        private static void DoRoundClockwise(Point3d p14, Point3d p13, Point3d p22, double thickness,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            // 预留实现
//        }
//        // 顺时针 厚墙在左侧
//        private static void DoThickLeftClockwise(Point3d p14, Point3d p13, Point3d p22, Point3d? j2,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            Polyline qj = j2.HasValue ? CreateBufferPolyline(p14, p13, j2.Value, p22) : CreateBufferPolyline(p14, p13, p22);
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            var ed = doc.Editor;
//            ed.WriteMessage($"\nasdfadsfadf{j2.HasValue}\n");
//            // qb1 = UnionPolylines(qb1, qj);
//            qb2 = UnionPolylines(qb2, qj);
//        }
//        // 顺时针 厚墙在右侧
//        private static void DoThickRightClockwise(Point3d p14, Point3d p13, Point3d p22, Point3d? j3,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            Polyline qj = j3.HasValue ? CreateBufferPolyline(p14, p13, j3.Value, p22) : CreateBufferPolyline(p14, p13, p22);
//            qb1 = UnionPolylines(qb1, qj);
//            qb2 = UnionPolylines(qb2, qj);
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            var ed = doc.Editor;
//            ed.WriteMessage($"\n{j3.HasValue}\n");
//        }
//        // 逆时针 Miter
//        private static void DoMiterCounterClockwise(Point3d p14, Point3d p13, Point3d p22,
//            Vector3d normal1, Vector3d normal2, double thickness, Point3d? j1, ref Polyline qb1, ref Polyline qb2)
//        {
//            Point3d j1a, j1b;
//            CalculateMiterPoints(p14, normal1, normal2, thickness, out j1a, out j1b);
//            double cosA = DotProduct(normal1, normal2) / (normal1.Length * normal2.Length);
//            if (cosA > _mitLimSqr - 1)
//            {
//                Polyline qj1 = CreateBufferPolyline(p14, p13, j1a, j1b, p22);
//                qb1 = UnionPolylines(qb1, qj1);
//                qb2 = UnionPolylines(qb2, qj1);
//            }
//            else
//            {
//                DoSquareCounterClockwise(p14, p13, p22, j1, ref qb1, ref qb2);
//            }
//        }
//        // 逆时针 Square
//        private static void DoSquareCounterClockwise(Point3d p14, Point3d p13, Point3d p22, Point3d? j1,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            if (j1.HasValue)
//            {
//                MovePointInPolyline(qb1, p13, j1.Value);
//                MovePointInPolyline(qb2, p22, j1.Value);
//            }
//        }
//        // 逆时针 Bevel
//        private static void DoBevelCounterClockwise(Point3d p14, Point3d p13, Point3d p22,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            Polyline qj = CreateBufferPolyline(p14, p13, p22);
//            qb1 = UnionPolylines(qb1, qj);
//            qb2 = UnionPolylines(qb2, qj);
//        }
//        // 逆时针 Round（预留）
//        private static void DoRoundCounterClockwise(Point3d p14, Point3d p13, Point3d p22, double thickness,
//            ref Polyline qb1, ref Polyline qb2)
//        {
//            // 预留实现
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
//        private static void CalculateMiterPoints(Point3d basePoint, Vector3d normal1, Vector3d normal2, double thickness, out Point3d j1a, out Point3d j1b)
//        {
//            double q = thickness / (DotProduct(normal1, normal2) + 1);
//            j1a = basePoint + (normal1 + normal2) * q;
//            j1b = j1a; // 简单情况，j1b 与 j1a 相同
//        }
//        public static Point3d? GetIntersectionPointNullable(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
//        {
//            Point3dCollection intersections = new Point3dCollection();
//            Line line1 = new Line(p1, p2);
//            Line line2 = new Line(p3, p4);
//            line1.IntersectWith(line2, Intersect.ExtendBoth, intersections, IntPtr.Zero, IntPtr.Zero);
//            return intersections.Count > 0 ? (Point3d?)intersections[0] : null;
//        }
//        public static double CalculateAngleBetweenWalls(Line q1, Line q2)
//        {
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            double angle = dir1.GetAngleTo(dir2);
//            return angle * (180 / Math.PI);
//        }
//        public static double DotProduct(Vector3d v1, Vector3d v2)
//        {
//            return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
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
//                    throw new InvalidOperationException("无法创建 Region，可能存在几何错误。");
//                using (var region1 = regions1[0] as Region)
//                using (var region2 = regions2[0] as Region)
//                {
//                    region1.BooleanOperation(BooleanOperationType.BoolUnite, region2);
//                    Polyline result = GetBoundaryPolyline(region1);
//                    var id = EtGpt.CreateLayer("00_Hy_buffer_Ex", 140);
//                    result.LayerId = id;
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