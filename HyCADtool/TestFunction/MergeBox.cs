using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class TestFunction
    {
        public static void MergeBox()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 选择边数为4的Polyline对象
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请选择边数为4的多边形(Polyline):";
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择.");
                    return;
                }
                SelectionSet ss = psr.Value;
                List<Extents3d> extentsList = new List<Extents3d>();
                foreach (SelectedObject selObj in ss)
                {
                    Polyline selectedPolyline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (selectedPolyline == null || selectedPolyline.NumberOfVertices != 4)
                    {
                        ed.WriteMessage("\n选择的对象无效或不是4边形.");
                        continue;
                    }
                    // 获取Polyline的GeometricExtents
                    Extents3d extents = selectedPolyline.GeometricExtents;
                    extentsList.Add(extents);
                }
                if (extentsList.Count == 0)
                {
                    ed.WriteMessage("\n未选择有效的4边形多边形.");
                    return;
                }
                // 合并相邻或相交的Extents3d
                List<Extents3d> mergedExtents = MergeExtents(extentsList, 300);
                // 创建新图层
                LayerTable layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                string layerName = "MergeBox";
                if (!layerTable.Has(layerName))
                {
                    layerTable.UpgradeOpen();
                    LayerTableRecord newLayer = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 4) // 设置颜色为4（青色）
                    };
                    layerTable.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                // 设置当前图层为新图层
                db.Clayer = layerTable[layerName];
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (var extents in mergedExtents)
                {
                    Point3d minPoint = extents.MinPoint;
                    Point3d maxPoint = extents.MaxPoint;
                    // 创建表示GeometricExtents的多边形
                    Polyline extentPolyline = new Polyline();
                    extentPolyline.AddVertexAt(0, new Point2d(minPoint.X, minPoint.Y), 0, 0, 0); // 左下角
                    extentPolyline.AddVertexAt(1, new Point2d(maxPoint.X, minPoint.Y), 0, 0, 0); // 右下角
                    extentPolyline.AddVertexAt(2, new Point2d(maxPoint.X, maxPoint.Y), 0, 0, 0); // 右上角
                    extentPolyline.AddVertexAt(3, new Point2d(minPoint.X, maxPoint.Y), 0, 0, 0); // 左上角
                    extentPolyline.Closed = true;
                    // 将多边形添加到模型空间
                    btr.AppendEntity(extentPolyline);
                    tr.AddNewlyCreatedDBObject(extentPolyline, true);
                }
                tr.Commit();
            }
        }
        private static List<Extents3d> MergeExtents(List<Extents3d> extentsList, double tolerance)
        {
            List<Extents3d> mergedList = new List<Extents3d>();
            while (extentsList.Any())
            {
                Extents3d current = extentsList[0];
                extentsList.RemoveAt(0);
                bool merged = false;
                for (int i = 0; i < mergedList.Count; i++)
                {
                    Extents3d existing = mergedList[i];
                    if (AreExtentsCloseOrIntersect(current, existing, tolerance))
                    {
                        mergedList[i] = UnionExtents(current, existing);
                        merged = true;
                        break;
                    }
                }
                if (!merged)
                {
                    mergedList.Add(current);
                }
            }
            return mergedList;
        }
        private static bool AreExtentsCloseOrIntersect(Extents3d extents1, Extents3d extents2, double tolerance)
        {
            return AreExtentsIntersect(extents1, extents2) ||
                   AreExtentsWithinTolerance(extents1, extents2, tolerance);
        }
        private static bool AreExtentsIntersect(Extents3d extents1, Extents3d extents2)
        {
            return extents1.MinPoint.X <= extents2.MaxPoint.X && extents1.MaxPoint.X >= extents2.MinPoint.X &&
                   extents1.MinPoint.Y <= extents2.MaxPoint.Y && extents1.MaxPoint.Y >= extents2.MinPoint.Y;
        }
        private static bool AreExtentsWithinTolerance(Extents3d extents1, Extents3d extents2, double tolerance)
        {
            return (Math.Abs(extents1.MaxPoint.X - extents2.MinPoint.X) <= tolerance || Math.Abs(extents2.MaxPoint.X - extents1.MinPoint.X) <= tolerance) &&
                   (Math.Abs(extents1.MaxPoint.Y - extents2.MinPoint.Y) <= tolerance || Math.Abs(extents2.MaxPoint.Y - extents1.MinPoint.Y) <= tolerance);
        }
        private static Extents3d UnionExtents(Extents3d extents1, Extents3d extents2)
        {
            Point3d minPoint = new Point3d(
                Math.Min(extents1.MinPoint.X, extents2.MinPoint.X),
                Math.Min(extents1.MinPoint.Y, extents2.MinPoint.Y),
                Math.Min(extents1.MinPoint.Z, extents2.MinPoint.Z));
            Point3d maxPoint = new Point3d(
                Math.Max(extents1.MaxPoint.X, extents2.MaxPoint.X),
                Math.Max(extents1.MaxPoint.Y, extents2.MaxPoint.Y),
                Math.Max(extents1.MaxPoint.Z, extents2.MaxPoint.Z));
            return new Extents3d(minPoint, maxPoint);
        }
    }
}
