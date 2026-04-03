using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 沉降计算结果绘制命令：在 AutoCAD 中插入结果表格
    /// 计算本身在面板 ViewModel 中完成，此命令只负责绘制
    /// </summary>
    public class SettlementCalculationCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettlementPanelViewModel.Current;
            if (vm == null)
            {
                ed.WriteMessage("\n沉降面板未初始化。");
                return;
            }

            var result = vm.LastResult;
            if (result == null || !result.Success)
            {
                ed.WriteMessage("\n请先在面板中执行沉降计算。");
                return;
            }

            try
            {
                double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var table = SettlementTableService.CreateResultTable(result, db, scale);

                    var ptResult = ed.GetPoint("\n选择沉降计算表格插入点: ");
                    if (ptResult.Status == PromptStatus.OK)
                        table.Position = ptResult.Value;
                    else
                        table.Position = new Point3d(0, 0, 0);

                    ms.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                    tr.Commit();
                }

                ed.WriteMessage($"\n沉降计算表格已插入。最终沉降 = {result.FinalSettlement:F2} mm");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制表格失败: {ex.Message}");
            }
        }
    }
}
