using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Entities;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 地脚螺栓创建命令（对应旧命令 hyab）
    /// 流程：输入型号 → 选择圆 → 替换为带螺栓数据的新圆
    /// </summary>
    public class AnchorBoltCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 输入螺栓型号
                var modelOpts = new PromptStringOptions("\n请输入地脚螺栓型号 (1-9) [默认1]: ")
                {
                    AllowSpaces = false,
                    DefaultValue = "1"
                };
                PromptResult modelRes = ed.GetString(modelOpts);
                if (modelRes.Status != PromptStatus.OK) return;

                AnchorBolt bolt;
                try { bolt = AnchorBoltFactory.CreateBolt(modelRes.StringResult); }
                catch (System.ArgumentException ex) { ed.WriteMessage($"\n错误: {ex.Message}"); return; }

                // 选择圆
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择多个圆: " };
                var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "CIRCLE") });
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                string jsonData = JsonConvert.SerializeObject(bolt);
                string layerName = $"00_Hy_螺栓_{bolt.Model}";

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 确保图层
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    if (!lt.Has(layerName))
                    {
                        var ltr = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 159)
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        var oldCircle = tr.GetObject(objId, OpenMode.ForRead) as Circle;
                        if (oldCircle == null) continue;

                        var newCircle = new Circle
                        {
                            Center = oldCircle.Center,
                            Radius = bolt.D / 2.0,
                            Layer = layerName
                        };
                        btr.AppendEntity(newCircle);
                        tr.AddNewlyCreatedDBObject(newCircle, true);

                        // 写入螺栓数据到扩展字典
                        Infrastructure.AutoCAD.Services.ExtensionDictionaryService.WriteAnchorBolt(tr, newCircle, bolt);

                        oldCircle.UpgradeOpen();
                        oldCircle.Erase();
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\n已转换 {selRes.Value.Count} 个圆为型号 {bolt.Model} 的地脚螺栓。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
