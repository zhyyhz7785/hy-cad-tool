using System;
using System.IO;
using System.Windows.Forms;
using HyCADTool.Features.SpongeCity.Domain.Services;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>
    /// hyscExC：导出 7 Sheet 海绵城市计算表 .xlsx（对齐 gen_sponge_housing_calc.py 行号契约）。
    /// </summary>
    public class ExportCalcExcelCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            var vm = SpongeCityPanelViewModel.Current;
            if (vm == null) { ed?.WriteMessage("\n[hyscExC] 海绵面板未打开。"); return; }

            try
            {
                vm.ExecuteCalculate();
                var path = PromptSavePath(vm.Input.ProjectName + "-海绵城市计算表.xlsx");
                if (string.IsNullOrEmpty(path)) return;

                SpongeCalcWorkbookBuilder.Build(vm.Input, vm.Result, path);
                ed?.WriteMessage($"\n[hyscExC] 已导出：{path}");
                vm.StatusMessage = "已导出计算表：" + Path.GetFileName(path);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[hyscExC] 失败: {ex.Message}");
                vm.StatusMessage = "hyscExC 失败：" + ex.Message;
            }
        }

        private static string PromptSavePath(string defaultName)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = defaultName,
                Title = "导出海绵城市计算表",
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
