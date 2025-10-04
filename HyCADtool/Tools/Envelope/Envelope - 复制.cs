//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Tools
//{
//    public static partial class ZTools
//    {
//        public static void CreateConvexHull()
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
//                // 在 AutoCAD 中绘制凸包并添加序列号
//                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                Polyline hullPolyline = new Polyline();
//                for (int i = 0; i < hull.Count; i++)
//                {
//                    Point3d point = hull[i];
//                    hullPolyline.AddVertexAt(i, new Point2d(point.X, point.Y), 0, 0, 0);
//                    // 添加序列号文本
//                    AddText(btr, tr, point, $"凸包 {i + 1}", 50);
//                }
//                hullPolyline.Closed = true;
//                hullPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 红色
//                btr.AppendEntity(hullPolyline);
//                tr.AddNewlyCreatedDBObject(hullPolyline, true);
//                // 提交事务
//                tr.Commit();
//            }
//            ed.WriteMessage("凸包已创建并添加序列号。\n");
//        }
//        private static List<Point3d> GetConvexHull(List<Point3d> points)
//        {
//            // 移除重复点
//            points = points.Distinct().ToList();
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
//            // 获取当前文档和数据库
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 获取块表记录
//                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                for (int i = 1; i < points.Count; i++)
//                {
//                    Point3d current = points[i];
//                    while (hull.Count > 1 && CrossProduct(NextToTop(hull), hull.Peek(), current) < 0)
//                    {
//                        // 如果是顺时针或共线，弹出栈顶元素
//                        Point3d removedPoint = hull.Pop();
//                        // 添加剔除点的文字标注
//                        AddText(btr, tr, removedPoint, $"剔除 {i + 1}", 20);
//                    }
//                    // 将当前点压入栈
//                    hull.Push(current);
//                    Point3d point3D = new Point3d(current.X, current.Y + 50, 0);
//                    // 添加当前点的文字标注
//                    AddText(btr, tr, point3D, $"序列号 {i + 1}", 20);
//                }
//                tr.Commit();
//            }
//            // 确保按逆时针顺序输出点
//            return hull.Reverse().ToList();
//        }
//        private static Point3d NextToTop(Stack<Point3d> stack)
//        {
//            Point3d top = stack.Pop();
//            Point3d nextToTop = stack.Peek();
//            stack.Push(top);
//            return nextToTop;
//        }
//        // 计算向量叉积的函数
//        private static double CrossProduct(Point3d a, Point3d b, Point3d c)
//        {
//            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
//        }
//        private static void AddText(BlockTableRecord btr, Transaction tr, Point3d position, string textString, double height)
//        {
//            DBText text = new DBText
//            {
//                Position = new Point3d(position.X, position.Y, position.Z),
//                Height = height,
//                TextString = textString,
//                Color = Color.FromColorIndex(ColorMethod.ByAci, 1) // 红色
//            };
//            btr.AppendEntity(text);
//            tr.AddNewlyCreatedDBObject(text, true);
//        }
//    }
//}
