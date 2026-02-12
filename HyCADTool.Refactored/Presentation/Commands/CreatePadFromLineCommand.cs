using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 根据选择的直线创建垫层矩形（HyDcL_Line）
    /// </summary>
    public class CreatePadFromLineCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            double d = 100.0;

            try
            {
                // 选择直线
                var selOpts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择一条直线以创建垫层: ",
                    SingleOnly = true
                };
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "LINE")
                });
                var selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 图层已在 PluginInitializer 统一创建

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var line = tr.GetObject(selRes.Value.GetObjectIds()[0], OpenMode.ForRead) as Line;
                    if (line == null) return;

                    var lineVec = line.EndPoint - line.StartPoint;
                    var padVec = lineVec.RotateBy(-Math.PI / 2, Vector3d.ZAxis).GetNormal() * d;
                    var extVec = lineVec.GetNormal() * d;

                    Point3d p1 = line.StartPoint - extVec;
                    Point3d p2 = line.EndPoint + extVec;
                    Point3d p3 = p2 + padVec;
                    Point3d p4 = p1 + padVec;

                    var pad = new Polyline();
                    pad.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
                    pad.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
                    pad.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
                    pad.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
                    pad.Closed = true;
                    pad.Layer = "00_hy_垫层";

                    ms.AppendEntity(pad);
                    tr.AddNewlyCreatedDBObject(pad, true);
                    tr.Commit();
                }

                ed.WriteMessage($"\n垫层创建完成，厚度: {d}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n创建失败: {ex.Message}");
            }
        }
    }
}
