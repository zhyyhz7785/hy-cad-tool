using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.HelpClass.ElevationSymbol;
using System;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        // 定义 AutoCAD 命令 "hytcbg"，用于将选中的 DBText 对象转换为标高符号
        [CommandMethod("hybgTCE_TextsCreatElevation")]
        // 在 HyCommand 类中修改 TextsCreatElevation 方法的解析部分
        public static void TextsCreatElevation()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            try
            {
                TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Text, "*") };
                SelectionFilter selFilter = new SelectionFilter(filter);
                PromptSelectionResult psr = ed.GetSelection(selFilter);
                if (psr.Status != PromptStatus.OK || psr.Value == null)
                {
                    ed.WriteMessage("\n未成功选择 DBText 对象，命令中止。");
                    return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    foreach (SelectedObject sobj in psr.Value)
                    {
                        DBText text = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as DBText;
                        if (text != null)
                        {
                            Point3d textPosition = text.Position;
                            string elevationText = text.TextString.Trim();
                            double elevation;
                            string cleanedText = elevationText.Replace("%%P", "");
                            if (double.TryParse(cleanedText, out elevation))
                            {
                                const double tolerance = 0.001;
                                bool isBasePoint = Math.Abs(elevation) <= tolerance;
                                // 计算 _currentPoint，使 Label.Position 与 textPosition 重合
                                Point3d calculatedCurrentPoint = CalculateCurrentPoint(
                                    textPosition,
                                    BaseConfig.Scale,
                                    BaseConfig.ElevationLength,
                                    text.Rotation // 使用 DBText 的旋转角度（弧度）
                                );
                                // 使用计算得到的 _currentPoint 创建 ElevationSymbol
                                ElevationSymbol symbol = new ElevationSymbol(
                                    calculatedCurrentPoint, // 使用计算的基准点
                                    scale: BaseConfig.Scale,
                                    d: BaseConfig.ElevationLength,
                                    ed,
                                    angleDegrees: text.Rotation * 180.0 / Math.PI
                                );
                                symbol.UpdateSymbol(elevation, isBasePoint);
                                symbol.AddToDatabase(tr, btr);
                                // 保留原始文字
                                // text.UpgradeOpen();
                                // text.Erase();
                            }
                            else
                            {
                                ed.WriteMessage($"\n跳过无效的标高值: {elevationText}，请确保为数值格式。");
                            }
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage("\n标高符号已生成。");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}\n");
            }
        }
        /// <summary>
        /// 计算 _currentPoint，使得生成的 Label.Position 与指定 textPosition 重合。
        /// </summary>
        /// <param name="textPosition">目标 DBText 的位置</param>
        /// <param name="scale">比例因子</param>
        /// <param name="d">构造参数 D</param>
        /// <param name="angleRadians">旋转角度（弧度）</param>
        /// <returns>计算得到的 _currentPoint</returns>
        public static Point3d CalculateCurrentPoint(Point3d textPosition, double scale, double d, double angleRadians)
        {
            //Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            //Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            // 计算 sqrt2
            double sqrt2 = Math.Sqrt(2) / 2 * d;
            double r = sqrt2 * 1.82;
            // 逆向旋转：将 textPosition 旋转回未旋转的坐标系
            double cosTheta = r * Math.Cos(Math.PI / 6 + angleRadians); // 逆时针旋转角度取负
            double sinTheta = r * Math.Sin(Math.PI / 6 + angleRadians);
            // 计算偏移量（与 UpdateSymbol 中的 Label.Position 一致）
            double offsetX = 0.909585 * sqrt2 * scale;             // X 方向偏移
            double offsetY = -1.57695 * sqrt2 * scale;   // Y 方向偏移，基于你的调整
            //double offsetX = sqrt2 * scale   ;     
            //double offsetY = -1.573 * sqrt2 * scale ;
            //double offsetX = sinTheta;
            //double offsetY = -cosTheta;
            //double offsetX = 0;
            //double offsetY = -0;
            //ed.WriteMessage($"{angleRadians}");
            return new Point3d(textPosition.X + offsetX, textPosition.Y + offsetY, 0);
        }
    }
}