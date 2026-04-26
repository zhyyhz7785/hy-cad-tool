using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Elevation.Domain.Enums;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.Features.Elevation.Services;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Shared.AutoCAD.Services;
using System;

namespace HyCADTool.Features.Elevation
{
    /// <summary>
    /// 从文字创建标高符号命令（对应旧命令 hybgTCE_TextsCreatElevation）
    /// 流程：选择 DBText → 解析标高值 → 反算位置 → 生成标高符号
    /// </summary>
    public class TextsCreateElevationCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                // 只选文字
                var filter = new TypedValue[] { new TypedValue((int)DxfCode.Text, "*") };
                var selFilter = new SelectionFilter(filter);
                PromptSelectionResult psr = ed.GetSelection(selFilter);

                if (psr.Status != PromptStatus.OK || psr.Value == null)
                {
                    ed.WriteMessage("\n未成功选择 DBText 对象，命令中止。");
                    return;
                }

                var vm = SettingsPanelViewModel.Current;
                double scale = vm != null ? vm.Scale : 40.0;
                double d = 2.0;

                // 确保图层/文字样式
                var (textStyleId, layerId) = ElevationService.EnsureStylesCreated();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (SelectedObject sobj in psr.Value)
                    {
                        var text = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as DBText;
                        if (text == null) continue;

                        string elevationText = text.TextString.Trim().Replace("%%P", "");
                        double ElevationValue;
                        if (!double.TryParse(elevationText, out ElevationValue))
                        {
                            ed.WriteMessage($"\n跳过无效的标高值: {text.TextString}");
                            continue;
                        }

                        bool isBasePoint = Math.Abs(ElevationValue) <= 0.001;

                        // 反算 _currentPoint 使 Label 位置对齐原文字
                        Point3d calculatedPoint = ElevationService.CalculateCurrentPointFromText(
                            text.Position, scale, d, text.Rotation);

                        double angleDegrees = text.Rotation * 180.0 / Math.PI;
                        var symbol = new ElevationSymbolJig(
                            calculatedPoint, scale, d, ed, textStyleId, layerId,
                            ElevationSymbolState.Normal, angleDegrees);

                        symbol.UpdateSymbol(ElevationValue, isBasePoint);
                        symbol.AddToDatabase(tr, btr);
                    }

                    tr.Commit();
                }
                ed.WriteMessage("\n标高符号已生成。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
