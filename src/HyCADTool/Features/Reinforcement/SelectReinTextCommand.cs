using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shell.Configuration.User;
using System.Text.RegularExpressions;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 选择 YJK 墙体配筋文字，按 H/V 方向分配图层，低于阈值的标记为手动配筋
    /// </summary>
    public class SelectReinTextCommand
    {
        private static string LayerH => UserLayerNameResolver.Get(LayerSemanticIds.HyRebarTextH, LayerBuiltinDefaults.HyRebarTextH);
        private static string LayerV => UserLayerNameResolver.Get(LayerSemanticIds.HyRebarTextV, LayerBuiltinDefaults.HyRebarTextV);
        private static string LayerManual => UserLayerNameResolver.Get(LayerSemanticIds.HyRebarManual, LayerBuiltinDefaults.HyRebarManual);

        private static readonly Regex FormatRegex = new Regex(@"^.(\d+(\.\d+)?)-(\d+(\.\d+)?)-(\d+(\.\d+)?)$");

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pdo = new PromptDoubleOptions("\n请输入配筋面积阈值")
            {
                DefaultValue = 7.7,
                UseDefaultValue = true
            };
            var pdr = ed.GetDouble(pdo);
            if (pdr.Status != PromptStatus.OK) return;
            double threshold = pdr.Value;

            var pso = new PromptSelectionOptions { MessageForAdding = "\n请选择要处理的文字对象（DBText 和 MText）：" };
            var psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (SelectedObject selObj in psr.Value)
                    {
                        if (selObj == null) continue;
                        var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        if (!TryExtractTextInfo(ent, out string textContent, out Point3d position,
                                out double height, out double rotation, out ObjectId textStyleId,
                                out double widthFactor, out double obliqueAngle, out Color color))
                            continue;

                        if (string.IsNullOrEmpty(textContent)) continue;
                        string firstChar = textContent.Substring(0, 1).ToUpper();
                        string layerName;
                        if (firstChar == "H")
                            layerName = LayerH;
                        else if (firstChar == "V")
                            layerName = LayerV;
                        else
                            continue;

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

                ed.WriteMessage("\n处理完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n处理失败: {ex.Message}");
            }
        }

        private static bool TryExtractTextInfo(Entity ent, out string textContent, out Point3d position,
            out double height, out double rotation, out ObjectId textStyleId,
            out double widthFactor, out double obliqueAngle, out Color color)
        {
            textContent = null;
            position = Point3d.Origin;
            height = 0;
            rotation = 0;
            textStyleId = ObjectId.Null;
            widthFactor = 1.0;
            obliqueAngle = 0.0;
            color = Color.FromColorIndex(ColorMethod.ByLayer, 256);

            if (ent is MText mText)
            {
                textContent = mText.Text.Replace("\\P", " ");
                position = mText.Location;
                height = mText.TextHeight;
                rotation = mText.Rotation;
                textStyleId = mText.TextStyleId;
                color = mText.Color;
                return true;
            }

            if (ent is DBText dt)
            {
                textContent = dt.TextString;
                position = dt.Position;
                height = dt.Height;
                rotation = dt.Rotation;
                textStyleId = dt.TextStyleId;
                widthFactor = dt.WidthFactor;
                obliqueAngle = dt.Oblique;
                color = dt.Color;
                return true;
            }

            return false;
        }
    }
}
