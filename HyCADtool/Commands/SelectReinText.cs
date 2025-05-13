using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Text.RegularExpressions;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hysrt")]
        ///选择yjk墙体水平配筋 《输入值的处理
        public static void SelectReinText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // 默认输入数字为 7.7
            double defaultInput = 7.7;
            // 提示用户输入一个数字，默认为 7.7
            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入一个数字")
            {
                DefaultValue = defaultInput,
                UseDefaultValue = true
            };
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            if (pdr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n命令已取消。");
                return;
            }
            double userInput = pdr.Value;
            // 提示用户选择对象
            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\n请选择要处理的文字对象（DBText 和 MText）：";
            PromptSelectionResult psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何对象，命令已取消。");
                return;
            }
            // 正则表达式匹配模式，匹配以任意字符开头，后跟数字和 '-' 的格式
            Regex regex = new Regex(@"^.(\d+(\.\d+)?)-(\d+(\.\d+)?)-(\d+(\.\d+)?)$");
            // 图层名称和颜色定义
            string layerH = "HY_H向钢筋";
            short colorH = 7; // 白色
            string layerV = "HY_V向钢筋";
            short colorV = 2; // 黄色
            string layerManual = "HY_手动配筋";
            short colorManual = 1; // 红色
                                   // 创建或更新图层
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                CreateOrUpdateLayer(db, lt, layerH, colorH);
                CreateOrUpdateLayer(db, lt, layerV, colorV);
                CreateOrUpdateLayer(db, lt, layerManual, colorManual);
                tr.Commit();
            }
            // 开始处理文字对象
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取模型空间
                BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                foreach (SelectedObject selObj in psr.Value)
                {
                    if (selObj != null)
                    {
                        Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                        if (ent != null)
                        {
                            string textContent = "";
                            Point3d position = Point3d.Origin;
                            double height = 2.5; // 默认文字高度
                            double rotation = 0.0;
                            string layerName = "";
                            ObjectId textStyleId = ObjectId.Null;
                            double widthFactor = 1.0;
                            double obliqueAngle = 0.0;
                            Color color = ent.Color;
                            // 如果是 MText，转换为 DBText
                            if (ent is MText mText)
                            {
                                textContent = mText.Text.Replace("\\P", " "); // 替换换行符
                                position = mText.Location;
                                height = mText.TextHeight;
                                rotation = mText.Rotation;
                                layerName = mText.Layer;
                                textStyleId = mText.TextStyleId;
                                color = mText.Color;
                                // MText 没有 WidthFactor 和 ObliqueAngle 属性，设置默认值
                                widthFactor = 1.0;
                                obliqueAngle = 0.0;
                                // 创建新的 DBText
                                DBText dbText = new DBText
                                {
                                    TextString = textContent,
                                    Position = position,
                                    Height = height,
                                    Rotation = rotation,
                                    Layer = layerName,
                                    TextStyleId = textStyleId,
                                    WidthFactor = widthFactor,
                                    Oblique = obliqueAngle,
                                    Color = color
                                };
                                // 添加到模型空间
                                btr.AppendEntity(dbText);
                                tr.AddNewlyCreatedDBObject(dbText, true);
                                // 删除原有的 MText
                                mText.Erase();
                                // 将 ent 指向新的 DBText
                                ent = dbText;
                            }
                            else if (ent is DBText dbText)
                            {
                                textContent = dbText.TextString;
                                position = dbText.Position;
                                height = dbText.Height;
                                rotation = dbText.Rotation;
                                layerName = dbText.Layer;
                                textStyleId = dbText.TextStyleId;
                                widthFactor = dbText.WidthFactor;
                                obliqueAngle = dbText.Oblique;
                                color = dbText.Color;
                            }
                            else
                            {
                                // 非文字对象，跳过
                                continue;
                            }
                            // 根据文字开头字符分配到图层1或图层2
                            string firstChar = textContent.Substring(0, 1).ToUpper();
                            if (firstChar == "H")
                            {
                                layerName = layerH;
                            }
                            else if (firstChar == "V")
                            {
                                layerName = layerV;
                            }
                            else
                            {
                                // 非 H 或 V 开头，跳过
                                continue;
                            }
                            // 检查文字是否匹配指定格式
                            Match match = regex.Match(textContent);
                            if (match.Success)
                            {
                                try
                                {
                                    // 提取数字
                                    double num1 = double.Parse(match.Groups[1].Value);
                                    double num2 = double.Parse(match.Groups[3].Value);
                                    double num3 = double.Parse(match.Groups[5].Value);
                                    // 与用户输入的数字进行比较
                                    if (num1 < userInput && num2 < userInput && num3 < userInput)
                                    {
                                        // 任一数字小于用户输入的数字，分配到图层3
                                        layerName = layerManual;
                                    }
                                }
                                catch
                                {
                                    ed.WriteMessage($"\n解析数字时出错，文字内容：{textContent}");
                                }
                            }
                            // 删除原有文字
                            ent.Erase();
                            // 创建新的文字对象并添加到模型空间
                            DBText newText = new DBText
                            {
                                TextString = textContent,
                                Position = position,
                                Height = height,
                                Rotation = rotation,
                                Layer = layerName,
                                TextStyleId = textStyleId,
                                WidthFactor = widthFactor,
                                Oblique = obliqueAngle,
                                Color = color
                            };
                            btr.AppendEntity(newText);
                            tr.AddNewlyCreatedDBObject(newText, true);
                        }
                    }
                }
                tr.Commit();
            }
            ed.WriteMessage("\n处理完成。");
        }
        // 创建或更新图层的方法
        private static void CreateOrUpdateLayer(Database db, LayerTable lt, string layerName, short colorIndex)
        {
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };
                lt.Add(ltr);
                db.TransactionManager.TopTransaction.AddNewlyCreatedDBObject(ltr, true);
                lt.DowngradeOpen();
            }
            else
            {
                // 如果图层已存在，更新颜色
                LayerTableRecord ltr = db.TransactionManager.TopTransaction.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            }
        }
    }
}
