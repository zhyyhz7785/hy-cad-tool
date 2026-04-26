using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AnchorBoltEntity = HyCADTool.Features.EquipmentFoundation.Domain.Entities.AnchorBolt;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.EquipmentFoundation.Domain.Entities;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Features.AnchorBolt
{
    /// <summary>
    /// 切换螺栓显示方式命令（对应旧命令 hyabCD_ChangeDisPlay）
    /// 流程：选圆/块 → 圆转块 或 块转圆
    /// </summary>
    public class AnchorBoltToggleDisplayCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择圆或螺栓块进行切换显示: " };
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT"),
                    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_[1-9]")
                });
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                int count = 0;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        string layerName = ent.Layer;
                        const string dataKey = "AnchorBolt";

                        if (ent is Circle circle)
                        {
                            var bolt = ExtensionDictionaryService.Read<AnchorBoltEntity>(tr, circle, dataKey);
                            if (bolt == null) continue;

                            string blockName = $"螺栓{bolt.Model}";
                            if (!bt.Has(blockName)) { ed.WriteMessage($"\n缺少块定义 '{blockName}'"); continue; }

                            var br = new BlockReference(circle.Center, bt[blockName]) { Layer = layerName };
                            btr.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);
                            ExtensionDictionaryService.Write(tr, br, bolt, dataKey);

                            circle.UpgradeOpen();
                            circle.Erase();
                            count++;
                        }
                        else if (ent is BlockReference br)
                        {
                            var bolt = ExtensionDictionaryService.Read<AnchorBoltEntity>(tr, br, dataKey);
                            if (bolt == null) continue;

                            var newCircle = new Circle
                            {
                                Center = br.Position,
                                Radius = bolt.D / 2.0,
                                Layer = layerName
                            };
                            btr.AppendEntity(newCircle);
                            tr.AddNewlyCreatedDBObject(newCircle, true);
                            ExtensionDictionaryService.Write(tr, newCircle, bolt, dataKey);

                            br.UpgradeOpen();
                            br.Erase();
                            count++;
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\n已切换 {count} 个对象的显示方式。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
