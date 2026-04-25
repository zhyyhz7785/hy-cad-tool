using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Entities;
using HyCADTool.Shared.AutoCAD.Services;
using AnchorBoltEntity = HyCADTool.Domain.Entities.AnchorBolt;

namespace HyCADTool.Features.AnchorBolt
{
    /// <summary>
    /// 螺栓矩形剖面命令（对应旧命令 hyabR_RectSection）
    /// 流程：选螺栓 → 读取数据 → 创建矩形多段线剖面 → 删除原对象
    /// </summary>
    public class AnchorBoltSectionCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择螺栓以绘制剖面图: " };
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT"),
                    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_[1-9]")
                });
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int count = 0;
                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        AnchorBoltEntity bolt = null;
                        Point3d center = Point3d.Origin;

                        if (ent is Circle circle)
                        {
                            bolt = ExtensionDictionaryService.ReadAnchorBolt(tr, circle);
                            center = circle.Center;
                        }
                        else if (ent is BlockReference br)
                        {
                            bolt = ExtensionDictionaryService.ReadAnchorBolt(tr, br);
                            center = br.Position;
                        }

                        if (bolt == null) continue;

                        double halfD = bolt.D / 2.0;
                        double height = bolt.H1 + bolt.H2;

                        var rect = new Polyline();
                        rect.AddVertexAt(0, new Point2d(center.X - halfD, center.Y), 0, 0, 0);
                        rect.AddVertexAt(1, new Point2d(center.X + halfD, center.Y), 0, 0, 0);
                        rect.AddVertexAt(2, new Point2d(center.X + halfD, center.Y - height), 0, 0, 0);
                        rect.AddVertexAt(3, new Point2d(center.X - halfD, center.Y - height), 0, 0, 0);
                        rect.Closed = true;
                        rect.Layer = ent.Layer;

                        btr.AppendEntity(rect);
                        tr.AddNewlyCreatedDBObject(rect, true);

                        ent.Erase();
                        count++;
                    }
                    tr.Commit();
                    ed.WriteMessage($"\n成功处理 {count} 个螺栓。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
