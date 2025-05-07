//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.HelpClass.CreatBase;
//using System;
//namespace HyCADTool.Test
//{
//    public class WallConnectionTest
//    {
//        [Autodesk.AutoCAD.Runtime.CommandMethod("TestWallConnection")]
//        public void TestHandleWallConnection()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            try
//            {
//                // 提示用户选择第一条直线
//                PromptEntityOptions peo1 = new PromptEntityOptions("\n请选择第一条墙体直线: ");
//                peo1.SetRejectMessage("\n必须选择一条直线！");
//                peo1.AddAllowedClass(typeof(Line), true);
//                PromptEntityResult per1 = ed.GetEntity(peo1);
//                if (per1.Status != PromptStatus.OK) return;
//                // 提示用户选择第二条直线
//                PromptEntityOptions peo2 = new PromptEntityOptions("\n请选择第二条墙体直线: ");
//                peo2.SetRejectMessage("\n必须选择一条直线！");
//                peo2.AddAllowedClass(typeof(Line), true);
//                PromptEntityResult per2 = ed.GetEntity(peo2);
//                if (per2.Status != PromptStatus.OK) return;
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    Line q1 = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Line;
//                    Line q2 = tr.GetObject(per2.ObjectId, OpenMode.ForRead) as Line;
//                    if (q1 == null || q2 == null)
//                    {
//                        ed.WriteMessage("\n选择的对象不是直线！");
//                        return;
//                    }
//                    if (!q1.EndPoint.IsEqualTo(q2.StartPoint, new Tolerance(0.001, 0.001)))
//                    {
//                        ed.WriteMessage("\n两条直线的连接点不重合！请确保第一条线的终点与第二条线的起点相连。");
//                        return;
//                    }
//                    double thickness1 = 300; // 第一条墙的厚度
//                    double thickness2 = 300; // 第二条墙的厚度
//                    // 生成初始缓冲区
//                    Polyline qb1 = GeometryExtensions.CreateWallBuffer(q1, thickness1);
//                    Polyline qb2 = GeometryExtensions.CreateWallBuffer(q2, thickness2);
//                    // 处理墙体连接并输出计算信息
//                    HandleWallConnectionWithDebug(q1, q2, thickness1, thickness2, ref qb1, ref qb2, ed);
//                    // 将结果添加到绘图空间
//                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//                    if (!qb1.IsWriteEnabled)
//                    {
//                        btr.AppendEntity(qb1);
//                        tr.AddNewlyCreatedDBObject(qb1, true);
//                    }
//                    if (!qb2.IsWriteEnabled)
//                    {
//                        btr.AppendEntity(qb2);
//                        tr.AddNewlyCreatedDBObject(qb2, true);
//                    }
//                    tr.Commit();
//                    ed.WriteMessage("\n墙体连接缓冲区生成成功！");
//                }
//            }
//            catch (Exception ex)
//            {
//                ed.WriteMessage($"\n发生错误: {ex.Message}");
//            }
//        }
//        // 修改后的 HandleWallConnection 方法，带调试输出
//        private static void HandleWallConnectionWithDebug(Line q1, Line q2, double thickness1, double thickness2, ref Polyline qb1, ref Polyline qb2, Editor ed)
//        {
//            // 计算夹角
//            double angle = GeometryExtensions.CalculateAngleBetweenWalls(q1, q2);
//            ed.WriteMessage($"\n夹角 (度): {angle:F3}");
//            // 交点 p1
//            Point3d p1 = q1.EndPoint;
//            ed.WriteMessage($"\n交点 p1: ({p1.X:F3}, {p1.Y:F3}, {p1.Z:F3})");
//            // q1 的方向向量和法向量
//            Vector3d dir1 = q1.EndPoint - q1.StartPoint;
//            Vector3d normal1 = dir1.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            ed.WriteMessage($"\nq1 方向向量: ({dir1.X:F3}, {dir1.Y:F3}, {dir1.Z:F3})");
//            ed.WriteMessage($"q1 法向量: ({normal1.X:F3}, {normal1.Y:F3}, {normal1.Z:F3})");
//            // q1 外侧点 p2
//            Point3d p2 = p1 + normal1 * thickness1;
//            ed.WriteMessage($"q1 外侧点 p2: ({p2.X:F3}, {p2.Y:F3}, {p2.Z:F3})");
//            // q2 的方向向量和法向量
//            Vector3d dir2 = q2.EndPoint - q2.StartPoint;
//            Vector3d normal2 = dir2.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis);
//            ed.WriteMessage($"\nq2 方向向量: ({dir2.X:F3}, {dir2.Y:F3}, {dir2.Z:F3})");
//            ed.WriteMessage($"q2 法向量: ({normal2.X:F3}, {normal2.Y:F3}, {normal2.Z:F3})");
//            // q2 外侧点 p4
//            Point3d p4 = p1 + normal2 * thickness2;
//            ed.WriteMessage($"q2 外侧点 p4: ({p4.X:F3}, {p4.Y:F3}, {p4.Z:F3})");
//            // 计算外侧平行线的交点 p3
//            Point3dCollection intersectionPoints = new Point3dCollection();
//            Line parallel1 = new Line(p2, p2 + dir1);
//            Line parallel2 = new Line(p4, p4 + dir2);
//            parallel1.IntersectWith(parallel2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
//            Point3d p3 = intersectionPoints.Count > 0 ? intersectionPoints[0] : p2;
//            ed.WriteMessage($"外侧交点 p3: ({p3.X:F3}, {p3.Y:F3}, {p3.Z:F3})");
//            // 根据夹角处理连接样式
//            if (angle >= 0 && angle <= 180) // 外侧弯折
//            {
//                if (thickness1 == thickness2) // 墙厚相同
//                {
//                    ed.WriteMessage("\n处理方式: 墙厚相同，外侧 L 形连接");
//                    Polyline lShape = new Polyline();
//                    lShape.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
//                    lShape.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
//                    lShape.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
//                    lShape.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
//                    lShape.Closed = true;
//                    qb1 = GeometryExtensions.UnionPolylines(qb1, lShape);
//                    qb2 = GeometryExtensions.UnionPolylines(qb2, lShape);
//                }
//                else // 墙厚不同
//                {
//                    ed.WriteMessage("\n处理方式: 墙厚不同，外侧三角形连接");
//                    Polyline triangle = new Polyline();
//                    triangle.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
//                    triangle.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
//                    triangle.AddVertexAt(2, new Point2d(p4.X, p4.Y), 0, 0, 0);
//                    triangle.Closed = true;
//                    qb1 = GeometryExtensions.UnionPolylines(qb1, triangle);
//                    qb2 = GeometryExtensions.UnionPolylines(qb2, triangle);
//                }
//            }
//            else // 内侧弯折
//            {
//                ed.WriteMessage("\n处理方式: 内侧弯折，调整外侧点到 p3");
//                GeometryExtensions.MovePointInPolyline(qb1, p2, p3);
//                GeometryExtensions.MovePointInPolyline(qb2, p4, p3);
//            }
//        }
//    }
//}