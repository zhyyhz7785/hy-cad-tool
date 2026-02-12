using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Text.RegularExpressions;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 选择 YJK 墙体配筋文字，按 H/V 方向分配图层，低于阈值的标记为手动配筋
    /// </summary>
    public class SelectReinTextCommand
    {
        private const string LayerH = "HY_H向钢筋";
        private const string LayerV = "HY_V向钢筋";
        private const string LayerManual = "HY_手动配筋";
        private const short ColorH = 7;   // 白色
        private const short ColorV = 2;   // 黄色
        private const short ColorManual = 1; // 红色

        private static readonly Regex FormatRegex = new Regex(@"^.(\d+(\.\d+)?)-(\d+(\.\d+)?)-(\d+(\.\d+)?)$");

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 输入阈值
            var pdo = new PromptDoubleOptions("\n请输入配筋面积阈值")
            {
                DefaultValue = 7.7,
                UseDefaultValue = true
            };
            var pdr = ed.GetDouble(pdo);
            if (pdr.Status != PromptStatus.OK) return;
            double threshold = pdr.Value;

            // 选择文字
            var pso = new PromptSelectionOptions { MessageForAdding = "\n请选择要处理的文字对象（DBText 和 MText）：" };
            var psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                {
                    // 图层已在 PluginInitializer 统一创建

                    // 处理文字
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                        foreach (SelectedObject selObj in psr.Value)
                        {
                            if (selObj == null) continue;
                            var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                            if (ent == null) continue;

                            // 提取文字信息
                            string textContent;
                            Point3d position;
                            double height;
                            double rotation;
                            string layerName;
                            ObjectId textStyleId;
                            double widthFactor;
                            double obliqueAngle;
                            Color color;

                            if (ent is MText mText)
                            {
                                textContent = mText.Text.Replace("\\P", " ");
                                position = mText.Location;
                                height = mText.TextHeight;
                                rotation = mText.Rotation;
                                layerName = mText.Layer;
                                textStyleId = mText.TextStyleId;
                                widthFactor = 1.0;
                                obliqueAngle = 0.0;
                                color = mText.Color;

                                // 转换 MText → DBText
                                var dbText = new DBText
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
                                btr.AppendEntity(dbText);
                                tr.AddNewlyCreatedDBObject(dbText, true);
                                mText.Erase();
                                ent = dbText;
                            }
                            else if (ent is DBText dt)
                            {
                                textContent = dt.TextString;
                                position = dt.Position;
                                height = dt.Height;
                                rotation = dt.Rotation;
                                layerName = dt.Layer;
                                textStyleId = dt.TextStyleId;
                                widthFactor = dt.WidthFactor;
                                obliqueAngle = dt.Oblique;
                                color = dt.Color;
                            }
                            else continue;

                            // 按首字符分类
                            if (string.IsNullOrEmpty(textContent)) continue;
                            string firstChar = textContent.Substring(0, 1).ToUpper();
                            if (firstChar == "H")
                                layerName = LayerH;
                            else if (firstChar == "V")
                                layerName = LayerV;
                            else
                                continue;

                            // 检查是否低于阈值
                            var match = FormatRegex.Match(textContent);
                            if (match.Success)
                            {
                                try
                                {
                                    double n1 = double.Parse(match.Groups[1].Value);
                                    double n2 = double.Parse(match.Groups[3].Value);
                                    double n3 = double.Parse(match.Groups[5].Value);
                                    if (n1 < threshold && n2 < threshold && n3 < threshold)
                                        layerName = LayerManual;
                                }
                                catch
                                {
                                    ed.WriteMessage($"\n解析数字出错: {textContent}");
                                }
                            }

                            // 删除旧文字，创建新文字到目标图层
                            ent.Erase();
                            var newText = new DBText
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

                        tr.Commit();
                    }
                }

                ed.WriteMessage("\n处理完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n处理失败: {ex.Message}");
            }
        }

        // EnsureLayer 已移除 —— 图层在 PluginInitializer 统一创建
    }
}
