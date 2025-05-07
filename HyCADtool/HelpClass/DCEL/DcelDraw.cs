using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Tools;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass.DCEL
{
    public static class DrawDCEL
    {
        public static void CreateDCELPolylinesFromCurves(List<Curve> inputCurves = null)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            List<Curve> curves = new List<Curve>();
            if (inputCurves != null && inputCurves.Count > 0)
            {
                // 如果提供了曲线列表，使用输入的曲线
                curves = inputCurves;
            }
            else
            {
                // 未提供曲线，提示用户选择曲线
                PromptSelectionOptions opts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择用于生成 DCEL 多段线的曲线："
                };
                // 设置过滤器，只选择曲线对象
                SelectionFilter filter = new SelectionFilter(new[]
                {
            new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE")
        });
                PromptSelectionResult res = ed.GetSelection(opts, filter);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }
                SelectionSet selSet = res.Value;
                // 获取选定的曲线
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj != null)
                        {
                            Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent is Curve curve)
                            {
                                curves.Add(curve);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            // 检查曲线列表是否为空
            if (curves == null || curves.Count == 0)
            {
                ed.WriteMessage("\n没有可用的曲线来生成 DCEL。");
                return;
            }
            // 利用曲线创建 DCEL
            DCEL dcel = DCELFactory.CreateFromCurves(curves);
            // 在 AutoCAD 中绘制由 DCEL 生成的区域
            DrawOuterFacesInAutoCAD(dcel);
            DrawInterFacesInAutoCAD(dcel);
        }
        private static void DrawOuterFacesInAutoCAD(DCEL dcel)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                // 检查并创建图层 "dcel"（如果不存在）
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has("dcelOuter"))
                {
                    lt.UpgradeOpen();
                    LayerTableRecord newLayer = new LayerTableRecord
                    {
                        Name = "dcelOuter",
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1) // 设置颜色
                    };
                    lt.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                // 用于跟踪已经绘制的半边
                HashSet<HalfEdge> drawnEdges = new HashSet<HalfEdge>();
                foreach (Face face in dcel.OuterFaces)
                {
                    //List<Point2d> vertices = new List<Point2d>();
                    var halfedges = face.Components;
                    var vertices = halfedges.Select(p => p.StartVertex.Position.Point3dTo2d()).ToList();
                    // 创建多段线并添加到图形中
                    if (vertices.Count > 0)
                    {
                        Polyline polyline = new Polyline(vertices.Count);
                        polyline.Layer = "dcelOuter"; // 设置到 "dcel" 图层
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }
                        polyline.Closed = true;
                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                }
                tr.Commit();
            }
        }
        private static void DrawInterFacesInAutoCAD(DCEL dcel)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                // 检查并创建图层 "dcel"（如果不存在）
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has("dcelInter"))
                {
                    lt.UpgradeOpen();
                    LayerTableRecord newLayer = new LayerTableRecord
                    {
                        Name = "dcelInter",
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2) // 设置颜色
                    };
                    lt.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                // 用于跟踪已经绘制的半边
                HashSet<HalfEdge> drawnEdges = new HashSet<HalfEdge>();
                foreach (Face face in dcel.InterFaces)
                {
                    //List<Point2d> vertices = new List<Point2d>();
                    var halfedges = face.Components;
                    var vertices = halfedges.Select(p => p.StartVertex.Position.Point3dTo2d()).ToList();
                    // 创建多段线并添加到图形中
                    if (vertices.Count > 0)
                    {
                        Polyline polyline = new Polyline(vertices.Count);
                        polyline.Layer = "dcelInter"; // 设置到 "dcel" 图层
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }
                        polyline.Closed = true;
                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                }
                tr.Commit();
            }
        }
    }
}
