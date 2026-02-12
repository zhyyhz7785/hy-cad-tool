using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 选择参考图形，将图块内实体的图层和颜色修改为参考图形的图层和颜色（HYc2_block_color）
    /// </summary>
    public class BlockColorCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 选择参考实体
                var peo = new PromptEntityOptions("\n请选择参考图形: ");
                peo.SetRejectMessage("\n请选择一个有效的图形!");
                peo.AddAllowedClass(typeof(Entity), false);
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                string targetLayer;
                Color targetColor;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var refEnt = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                    if (refEnt == null) return;
                    targetLayer = refEnt.Layer;
                    targetColor = refEnt.Color;
                    tr.Commit();
                }

                // 选择图块
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择要修改的图块: ",
                    AllowDuplicates = false
                };
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                });
                var psr = ed.GetSelection(pso, filter);
                if (psr.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in psr.Value)
                    {
                        var blkRef = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blkRef == null) continue;

                        ProcessBlock(tr, blkRef, targetLayer, targetColor);
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n图层和颜色修改完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n修改失败: {ex.Message}");
            }
        }

        private void ProcessBlock(Transaction tr, BlockReference blkRef, string layer, Color color)
        {
            var btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;

            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null) continue;

                if (ent is BlockReference nested)
                    ProcessBlock(tr, nested, layer, color);

                ent.Layer = layer;
                ent.Color = color;
            }
        }
    }
}
