using System;
using System.IO;
using System.Windows.Forms;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>hyscExB：导出海绵城市概算+预算 .xlsx（对齐 gen_sponge_housing_budget.py）。</summary>
    public class ExportBudgetExcelCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            var vm = SpongeCityPanelViewModel.Current;
            if (vm == null) { ed?.WriteMessage("\n[hyscExB] 海绵面板未打开。"); return; }

            try
            {
                vm.ExecuteCalculate();
                var path = PromptSavePath(vm.Input.ProjectName + "-海绵城市预算表.xlsx");
                if (string.IsNullOrEmpty(path)) return;

                SpongeBudgetWorkbookBuilder.Build(vm.Input, vm.Result, path);
                ed?.WriteMessage($"\n[hyscExB] 已导出：{path}");
                vm.StatusMessage = "已导出预算表：" + Path.GetFileName(path);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[hyscExB] 失败: {ex.Message}");
                vm.StatusMessage = "hyscExB 失败：" + ex.Message;
            }
        }

        private static string PromptSavePath(string defaultName)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = defaultName,
                Title = "导出海绵城市预算表",
                OverwritePrompt = true,
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    return dlg.FileName;
                return null;
            }
        }
    }
}
