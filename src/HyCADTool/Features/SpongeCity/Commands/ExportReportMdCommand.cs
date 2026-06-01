using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>hyscExM：导出海绵城市 md 计算书。</summary>
    public class ExportReportMdCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            var vm = SpongeCityPanelViewModel.Current;
            if (vm == null) { ed?.WriteMessage("\n[hyscExM] 海绵面板未打开。"); return; }

            try
            {
                vm.ExecuteCalculate();
                var path = PromptSavePath(vm.Input.ProjectName + "-海绵城市计算书.md");
                if (string.IsNullOrEmpty(path)) return;

                var md = SpongeReportGenerator.Generate(vm.Input, vm.Result);
                File.WriteAllText(path, md, new UTF8Encoding(true));
                ed?.WriteMessage($"\n[hyscExM] 已导出：{path}");
                vm.StatusMessage = "已导出 md 计算书：" + Path.GetFileName(path);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[hyscExM] 失败: {ex.Message}");
                vm.StatusMessage = "hyscExM 失败：" + ex.Message;
            }
        }

        private static string PromptSavePath(string defaultName)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Markdown (*.md)|*.md|All Files (*.*)|*.*",
                FileName = defaultName,
                Title = "导出海绵城市 md 计算书",
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
