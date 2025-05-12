//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using Clipper2Lib;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Command
//{
//    public static partial class HyCommand
//    {
//        // 定义一个只比较二维的点比较器
//        private class Point2dEqualityComparer : IEqualityComparer<(double, double, double)>
//        {
//            private readonly double _tolerance;
//            public Point2dEqualityComparer(double tolerance = 0.0001)
//            {
//                _tolerance = tolerance;
//            }
//            public bool Equals((double, double, double) p1, (double, double, double) p2)
//            {
//                // 只比较 X 和 Y，忽略 Z
//                return Math.Abs(p1.Item1 - p2.Item1) <= _tolerance &&
//                       Math.Abs(p1.Item2 - p2.Item2) <= _tolerance;
//            }
//            public int GetHashCode((double, double, double) point)
//            {
//                // 只使用 X 和 Y 生成哈希码
//                int x = (int)Math.Round(point.Item1 / _tolerance);
//                int y = (int)Math.Round(point.Item2 / _tolerance);
//                return HashCode.Combine(x, y); // 使用 2 参数重载
//            }
//        }
//        [CommandMethod("SortPolylinePoints")]
//        public static void SortPolylinePoints()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            PromptSelectionOptions selOpts = new PromptSelectionOptions();
//            selOpts.MessageForAdding = "请选择多段线: ";
//            TypedValue[] filter = new TypedValue[] { new TypedValue(0, "LWPOLYLINE") };
//            SelectionFilter selFilter = new SelectionFilter(filter);
//            PromptSelectionResult selRes = ed.GetSelection(selOpts, selFilter);
//            if (selRes.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n未选择任何对象。");
//                return;
//            }
//            // 使用只比较二维的HashSet
//            HashSet<(double, double, double)> uniquePoints = new HashSet<(double, double, double)>(
//                new Point2dEqualityComparer(0.0001)); // 可调整容差值
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                foreach (ObjectId id in selRes.Value.GetObjectIds())
//                {
//                    Polyline pline = tr.GetObject(id, OpenMode.ForRead) as Polyline;
//                    if (pline != null)
//                    {
//                        for (int i = 0; i < pline.NumberOfVertices; i++)
//                        {
//                            Point3d pt = pline.GetPoint3dAt(i);
//                            uniquePoints.Add((pt.X, pt.Y, pt.Z));
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//            List<Point3d> sortedPoints = uniquePoints
//                .Select(p => new Point3d(p.Item1, p.Item2, p.Item3))
//                .OrderBy(p => p.X)
//                .ThenBy(p => p.Y)
//                .ToList();
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                foreach (Point3d pt in sortedPoints)
//                {
//                    DBPoint dbPt = new DBPoint(pt);
//                    btr.AppendEntity(dbPt);
//                    tr.AddNewlyCreatedDBObject(dbPt, true);
//                }
//                tr.Commit();
//            }
//            ed.WriteMessage($"\n已创建 {sortedPoints.Count} 个排序后的点（基于二维去重）。");
//        }
//    }
//}