//using System;
//using System.Linq;
//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD.Runtime;
//namespace HyCADTool
//{
//    public static partial class TestFunction
//    {
//        public static void HighlightGeometricExtentsAndDrawReinforcement()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                // 选择多个Polyline对象作为网格
//                PromptSelectionOptions pso = new PromptSelectionOptions();
//                pso.MessageForAdding = "\n请选择一个或多个网格(Polyline):";
//                TypedValue[] filter = new TypedValue[]
//                {
//                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
//                };
//                SelectionFilter sf = new SelectionFilter(filter);
//                PromptSelectionResult psr = ed.GetSelection(pso, sf);
//                if (psr.Status != PromptStatus.OK)
//                {
//                    ed.WriteMessage("\n取消选择.");
//                    return;
//                }
//                SelectionSet ss = psr.Value;
//                foreach (SelectedObject selObj in ss)
//                {
//                    Polyline selectedPolyline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
//                    if (selectedPolyline == null)
//                    {
//                        ed.WriteMessage("\n选择的对象无效.");
//                        continue;
//                    }
//                    // 获取Polyline的GeometricExtents
//                    Extents3d extents = selectedPolyline.GeometricExtents;
//                    Point3d minPoint = extents.MinPoint;
//                    Point3d maxPoint = extents.MaxPoint;
//                    // 创建表示GeometricExtents的矩形框
//                    Polyline extentPolyline = new Polyline();
//                    extentPolyline.AddVertexAt(0, new Point2d(minPoint.X, minPoint.Y), 0, 0, 0); // 左下角
//                    extentPolyline.AddVertexAt(1, new Point2d(maxPoint.X, minPoint.Y), 0, 0, 0); // 右下角
//                    extentPolyline.AddVertexAt(2, new Point2d(maxPoint.X, maxPoint.Y), 0, 0, 0); // 右上角
//                    extentPolyline.AddVertexAt(3, new Point2d(minPoint.X, maxPoint.Y), 0, 0, 0); // 左上角
//                    extentPolyline.Closed = true;
//                    extentPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // 设置颜色为黄色（ACI颜色索引2）
//                    // 绘制表示GeometricExtents的矩形框
//                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                    btr.AppendEntity(extentPolyline);
//                    tr.AddNewlyCreatedDBObject(extentPolyline, true);
//                    // 计算平行移动后的红色钢筋线的起点和终点
//                    Point3d startPoint = new Point3d(minPoint.X, minPoint.Y + 150, 0);
//                    Point3d endPoint = new Point3d(maxPoint.X, minPoint.Y + 150, 0);
//                    // 绘制红色钢筋线
//                    Polyline redReinforcement = new Polyline();
//                    redReinforcement.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
//                    redReinforcement.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
//                    redReinforcement.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 设置颜色为红色（ACI颜色索引1）
//                    btr.AppendEntity(redReinforcement);
//                    tr.AddNewlyCreatedDBObject(redReinforcement, true);
//                }
//                tr.Commit();
//            }
//        }
//    }
//}
