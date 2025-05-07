using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("HYc2_block_color")]
        public static void ChangeBlockLayerAndColor()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // 提示用户选择参考图形
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择参考图形: ");
                peo.SetRejectMessage("\n请选择一个有效的图形!");
                peo.AddAllowedClass(typeof(Entity), false);
                PromptEntityResult per = ed.GetEntity(peo);

                if (per.Status != PromptStatus.OK) return;

                // 获取参考图形的图层和颜色
                string targetLayer;
                Color targetColor;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity refEntity = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                    if (refEntity == null) return;
                    targetLayer = refEntity.Layer;
                    targetColor = refEntity.Color; // 获取参考图形的颜色
                    tr.Commit();
                }

                // 提示用户选择图块
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n请选择要修改的图块: ";
                pso.AllowDuplicates = false;

                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };
                SelectionFilter sf = new SelectionFilter(filter);

                PromptSelectionResult psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK) return;

                // 处理选中的图块
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in psr.Value)
                    {
                        BlockReference blkRef = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blkRef == null) continue;

                        // 获取图块定义
                        BlockTableRecord btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (btr == null) continue;

                        // 遍历图块中的所有实体
                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                            if (ent != null)
                            {
                                // 修改图层和颜色
                                ent.Layer = targetLayer;
                                ent.Color = targetColor;
                            }
                        }

                        // 处理嵌套图块
                        ProcessNestedBlocks(tr, blkRef, targetLayer, targetColor);
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n图层和颜色修改完成!");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }

        // 处理嵌套图块的辅助方法
        private static void ProcessNestedBlocks(Transaction tr, BlockReference blkRef, string targetLayer, Color targetColor)
        {
            BlockTableRecord btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;

            foreach (ObjectId id in btr)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent is BlockReference nestedBlkRef)
                {
                    // 递归处理嵌套图块
                    ProcessNestedBlocks(tr, nestedBlkRef, targetLayer, targetColor);
                }
                else if (ent != null)
                {
                    ent.Layer = targetLayer;
                    ent.Color = targetColor;
                }
            }
        }
    }
}