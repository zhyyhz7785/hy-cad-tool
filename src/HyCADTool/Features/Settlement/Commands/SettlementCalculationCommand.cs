using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Settlement;
using HyCADTool.Features.Settlement.ViewModels;
using HyCADTool.Features.Settlement.Views;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Settlement.Commands
{
    /// <summary>
    /// 沉降计算命令：打开独立窗口进行计算，关闭后可绘制结果表格
    /// </summary>
    public class SettlementCalculationCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var vm = SettlementPanelViewModel.Current;
            if (vm == null) vm = new SettlementPanelViewModel();

            var window = new SettlementWindow(vm);
            AcApp.ShowModalWindow(window);

            if (window.ShouldDrawTable && vm.LastResult != null && vm.LastResult.Success)
            {
                DrawResultTable(doc, db, ed, vm);
            }
        }

        /// <summary>
        /// 仅绘制结果表格（从面板按钮路由的 C1 调用）
        /// </summary>
        public void ExecuteDrawOnly()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettlementPanelViewModel.Current;
            if (vm?.LastResult == null || !vm.LastResult.Success)
            {
                ed.WriteMessage("\n请先在面板中执行沉降计算。");
                return;
            }

            DrawResultTable(doc, db, ed, vm);
        }

        private static void DrawResultTable(Document doc, Database db, Editor ed, SettlementPanelViewModel vm)
        {
            try
            {
                double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var table = SettlementTableService.CreateResultTable(vm.LastResult, db, scale);

                    var ptResult = ed.GetPoint("\n选择沉降计算表格插入点: ");
                    if (ptResult.Status == PromptStatus.OK)
                        table.Position = ptResult.Value;
                    else
                        table.Position = new Point3d(0, 0, 0);

                    ms.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                    tr.Commit();
                }

                ed.WriteMessage($"\n沉降计算表格已插入。最终沉降 = {vm.LastResult.FinalSettlement:F2} mm");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制表格失败: {ex.Message}");
            }
        }
    }
}
