using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using Newtonsoft.Json;


namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        /*
         * 主要功能：
         * 该命令（"hyab"）用于将 AutoCAD 中的圆转换为带有地脚螺栓数据的圆。
         * 用户输入螺栓型号后，选择多个圆，程序会：
         * 1. 根据型号创建地脚螺栓对象
         * 2. 将螺栓数据序列化为 JSON 格式并附加到新圆的扩展字典
         * 3. 用新圆（半径基于螺栓直径）替换原始圆
         * 4. 将新圆置于指定图层
         * 
         * 逻辑流程：
         * 1. 获取用户输入的螺栓型号
         * 2. 创建螺栓对象并验证
         * 3. 获取用户选择的圆
         * 4. 为每个圆创建新圆并附加螺栓数据
         * 5. 删除原始圆并提交事务
         */

        [CommandMethod("hyab")] // AutoCAD 命令名 "hyab"
        public static void AttachAnchorBoltToCircles()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument; // 获取当前活动文档
            Database db = doc.Database; // 获取文档的数据库
            Editor ed = doc.Editor; // 获取编辑器对象用于用户交互

            try
            {
                // 提示用户输入地脚螺栓型号 (1-9)，默认值为 "1"
                PromptStringOptions modelOpts = new PromptStringOptions("\n请输入地脚螺栓型号 (1-9) [默认1]: ");
                modelOpts.AllowSpaces = false; // 不允许空格
                modelOpts.DefaultValue = "1"; // 默认型号
                PromptResult modelRes = ed.GetString(modelOpts); // 获取用户输入
                if (modelRes.Status != PromptStatus.OK) return; // 如果用户取消则退出

                string boltModel = modelRes.StringResult; // 获取输入的型号
                AnchorBolt bolt; // 声明螺栓对象
                try
                {
                    bolt = AnchorBoltFactory.CreateBolt(boltModel); // 通过工厂类创建螺栓对象
                }
                catch (System.ArgumentException ex)
                {
                    ed.WriteMessage($"\n错误: {ex.Message}"); // 如果型号无效，显示错误并退出
                    return;
                }

                // 提示用户选择多个圆
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择多个圆 (将被转换为带螺栓数据的圆): ";
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE") // 筛选器：仅选择圆
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult selRes = ed.GetSelection(selOpts, sf); // 获取选择结果
                if (selRes.Status != PromptStatus.OK) return; // 如果用户取消则退出

                // 将螺栓对象序列化为 JSON 字符串
                string jsonData = JsonConvert.SerializeObject(bolt);

                using (Transaction tr = db.TransactionManager.StartTransaction()) // 开启事务
                {
                    // 获取图层表
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    string layerName = $"00_Hy_螺栓_{boltModel}"; // 定义螺栓图层名

                    // 检查图层是否存在，不存在则创建
                    if (!lt.Has(layerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = layerName, // 设置图层名
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                                Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 159) // 设置颜色（索引 159）
                        };
                        lt.Add(ltr); // 添加新图层
                        tr.AddNewlyCreatedDBObject(ltr, true); // 提交到数据库
                    }

                    // 获取模型空间
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 遍历用户选择的每个圆
                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        Circle oldCircle = tr.GetObject(objId, OpenMode.ForRead) as Circle; // 获取原始圆
                        if (oldCircle == null) continue; // 如果不是圆则跳过

                        // 创建新圆，半径基于螺栓直径
                        Circle newCircle = new Circle
                        {
                            Center = oldCircle.Center, // 保留原始圆心
                            Radius = bolt.D / 2.0, // 使用螺栓直径 D 的一半作为半径
                            Layer = layerName // 设置图层
                        };
                        btr.AppendEntity(newCircle); // 将新圆添加到模型空间
                        tr.AddNewlyCreatedDBObject(newCircle, true); // 提交新圆

                        // 创建或获取新圆的扩展字典
                        DBDictionary extDict;
                        if (!newCircle.ExtensionDictionary.IsValid)
                        {
                            newCircle.CreateExtensionDictionary(); // 如果没有扩展字典则创建
                            extDict = tr.GetObject(newCircle.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
                        }
                        else
                        {
                            extDict = tr.GetObject(newCircle.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
                        }

                        // 将螺栓数据附加到扩展字典
                        using (Xrecord xRec = new Xrecord())
                        {
                            xRec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, jsonData)); // 存储 JSON 数据
                            extDict.SetAt("AnchorBolt", xRec); // 设置键名为 "AnchorBolt"
                            tr.AddNewlyCreatedDBObject(xRec, true); // 提交扩展记录
                        }

                        // 删除原始圆
                        oldCircle.UpgradeOpen(); // 提升权限以便修改
                        oldCircle.Erase(); // 删除
                    }

                    tr.Commit(); // 提交事务
                }

                // 输出成功消息
                ed.WriteMessage($"\n已成功为 {selRes.Value.Count} 个圆转换为型号为 {boltModel} 的地脚螺栓！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}"); // 捕获并显示任何异常
            }
        }
    }
}