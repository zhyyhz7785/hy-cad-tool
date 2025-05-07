using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using HyCADTool.Utilities; // 确保引入ExtensionDictionaryUtils

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        /*
         * 主要功能：
         * 该命令（"hyc2ab"）用于在 AutoCAD 中切换地脚螺栓的显示方式。
         * 用户可以选择圆或块引用（BlockReference），程序会：
         * 1. 将带有螺栓数据的圆转换为对应的块引用（螺栓块）
         * 2. 将带有螺栓数据的块引用转换回圆
         * 3. 保留扩展字典中的螺栓数据（JSON 格式）
         */

        [CommandMethod("hyabCD_ChangeDisPlay")] // AutoCAD 命令名 "hyc2ab"
        public static void ToggleAnchorBoltDisplay()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // 提示用户选择圆或块
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择圆或螺栓块进行切换显示: ";

                // 设置筛选器：仅选择圆和块，且图层名匹配 "00_Hy_螺栓_[1-9]"
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT"),
                    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_[1-9]")
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult selRes = ed.GetSelection(selOpts, sf);
                if (selRes.Status != PromptStatus.OK) return;

                int successCount = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 遍历用户选择的对象
                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        string layerName = ent.Layer;
                        const string dataKey = "AnchorBolt"; // 数据键名常量

                        if (ent is Circle circle)
                        {
                            // 使用泛型方法读取螺栓数据
                            AnchorBolt bolt = ExtensionDictionaryUtils.ReadFromExtensionDictionary<AnchorBolt>(tr, circle, dataKey, ed);
                            if (bolt == null)
                            {
                                ed.WriteMessage($"\n警告: 圆对象缺少螺栓数据，跳过此对象。");
                                continue;
                            }

                            // 转换为块
                            string boltModel = bolt.Model;
                            string blockName = $"螺栓{boltModel}";

                            // 检查块定义是否存在
                            if (!bt.Has(blockName))
                            {
                                ed.WriteMessage($"\n错误: 图纸中缺少块定义 '{blockName}'，跳过此对象。");
                                continue;
                            }

                            // 创建块引用
                            BlockReference br = new BlockReference(circle.Center, bt[blockName])
                            {
                                Layer = layerName
                            };
                            btr.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);

                            // 使用泛型方法写入螺栓数据到块引用
                            ExtensionDictionaryUtils.WriteToExtensionDictionary(tr, br, bolt, dataKey, ed);

                            // 删除原圆
                            circle.UpgradeOpen();
                            circle.Erase();
                            successCount++;
                        }
                        else if (ent is BlockReference br)
                        {
                            // 使用泛型方法读取螺栓数据
                            AnchorBolt bolt = ExtensionDictionaryUtils.ReadFromExtensionDictionary<AnchorBolt>(tr, br, dataKey, ed);
                            if (bolt == null)
                            {
                                ed.WriteMessage($"\n警告: 块对象缺少螺栓数据，跳过此对象。");
                                continue;
                            }

                            // 验证块名称是否匹配
                            string boltModel = bolt.Model;
                            string blockName = $"螺栓{boltModel}";
                            if (br.Name != blockName)
                            {
                                ed.WriteMessage($"\n警告: 块 '{br.Name}' 与预期名称 '{blockName}' 不匹配，跳过此对象。");
                                continue;
                            }

                            // 创建新圆
                            Circle newCircle = new Circle
                            {
                                Center = br.Position,
                                Radius = bolt.D / 2.0,
                                Layer = layerName
                            };
                            btr.AppendEntity(newCircle);
                            tr.AddNewlyCreatedDBObject(newCircle, true);

                            // 使用泛型方法写入螺栓数据到新圆
                            ExtensionDictionaryUtils.WriteToExtensionDictionary(tr, newCircle, bolt, dataKey, ed);

                            // 删除原块
                            br.UpgradeOpen();
                            br.Erase();
                            successCount++;
                        }
                    }

                    tr.Commit();
                }

                // 输出成功消息
                ed.WriteMessage($"\n已成功切换 {successCount} 个对象的显示方式！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}