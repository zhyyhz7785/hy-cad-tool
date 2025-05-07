//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Tools
//{
//    public static partial class EtGpt
//    {
//        public static void CreateEnvelope()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            // 提示用户选择多边形
//            PromptSelectionResult selRes = ed.GetSelection();
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("选择失败或用户取消了操作。\n");
//                return;
//            }
//            SelectionSet selSet = selRes.Value;
//            List<Point3d> allPoints = new List<Point3d>();
//            // 遍历选定的多边形，获取所有顶点
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                foreach (SelectedObject selObj in selSet)
//                {
//                    if (selObj != null)
//                    {
//                        Polyline pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
//                        if (pline != null)
//                        {
//                            int numVertices = pline.NumberOfVertices;
//                            for (int i = 0; i < numVertices; i++)
//                            {
//                                allPoints.Add(pline.GetPoint3dAt(i));
//                            }
//                        }
//                    }
//                }
//                // 计算凸包
//                List<Point3d> hull = GetConvexHull(allPoints);
//                // 创建包络多边形
//                Polyline envelope = new Polyline();
//                for (int i = 0; i < hull.Count; i++)
//                {
//                    envelope.AddVertexAt(i, new Point2d(hull[i].X, hull[i].Y), 0, 0, 0);
//                }
//                envelope.Closed = true;
//                // 将包络多边形添加到图形数据库
//                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                btr.AppendEntity(envelope);
//                tr.AddNewlyCreatedDBObject(envelope, true);
//                // 提交事务
//                tr.Commit();
//            }
//            ed.WriteMessage("包络多边形已创建。\n");
//        }
//        private static List<Point3d> GetConvexHull(List<Point3d> points)
//        {
//            // 按 Y 坐标排序，如果 Y 坐标相同则按 X 坐标排序
//            points = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
//            Point3d p0 = points[0];
//            points.RemoveAt(0);
//            // 按极角排序
//            points = points.OrderBy(p => Math.Atan2(p.Y - p0.Y, p.X - p0.X)).ToList();
//            // 初始化凸包栈
//            Stack<Point3d> hull = new Stack<Point3d>();
//            hull.Push(p0);
//            hull.Push(points[0]);
//            for (int i = 1; i < points.Count; i++)
//            {
//                Point3d top = hull.Pop();
//                while (hull.Count > 0 && CrossProduct(hull.Peek(), top, points[i]) <= 0)
//                {
//                    top = hull.Pop();
//                }
//                hull.Push(top);
//                hull.Push(points[i]);
//            }
//            return hull.ToList();
//        }
//        private static double CrossProduct(Point3d o, Point3d a, Point3d b)
//        {
//            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
//        }
//    }
//}
