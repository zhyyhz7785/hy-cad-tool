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
        public static void JoinReinforcment()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 选择多条Polyline
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请选择多条Polyline:";
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
                List<Polyline> polylines = new List<Polyline>();
                foreach (SelectedObject selObj in ss)
                {
                    Polyline pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pline != null)
                    {
                        polylines.Add(pline);
                    }
                }
                // 用户输入容差值
                PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入容差值:");
                PromptDoubleResult pdr = ed.GetDouble(pdo);
                if (pdr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消输入.");
                    return;
                }
                double tolerance = pdr.Value;
                // 按起点排序
                polylines = polylines.OrderBy(p => p.StartPoint.X).ThenBy(p => p.StartPoint.Y).ToList();
                // 创建新图层
                LayerTable layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                string layerName = "MergeReinforcement";
                if (!layerTable.Has(layerName))
                {
                    layerTable.UpgradeOpen();
                    LayerTableRecord newLayer = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 5) // 设置颜色为5（蓝色）
                    };
                    layerTable.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                // 设置当前图层为新图层
                db.Clayer = layerTable[layerName];
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                while (polylines.Any())
                {
                    Polyline firstPolyline = polylines.First();
                    polylines.RemoveAt(0);
                    Point3d startPoint = firstPolyline.StartPoint;
                    Point3d endPoint = firstPolyline.EndPoint;
                    bool merged = true;
                    while (merged)
                    {
                        merged = false;
                        for (int i = 0; i < polylines.Count; i++)
                        {
                            Polyline pline = polylines[i];
                            if (Math.Abs(endPoint.X - pline.StartPoint.X) <= tolerance && Math.Abs(endPoint.Y - pline.StartPoint.Y) <= tolerance)
                            {
                                endPoint = new Point3d(pline.EndPoint.X, endPoint.Y, endPoint.Z);
                                polylines.RemoveAt(i);
                                merged = true;
                                break;
                            }
                        }
                    }
                    // 绘制合并后的Polyline
                    Polyline mergedPolyline = new Polyline();
                    mergedPolyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
                    mergedPolyline.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
                    mergedPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                    btr.AppendEntity(mergedPolyline);
                    tr.AddNewlyCreatedDBObject(mergedPolyline, true);
                }
                tr.Commit();
            }
        }
    }
}
