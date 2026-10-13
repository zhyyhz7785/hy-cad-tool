using HyCADTool.Features.DesignSpec.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Shared.Host;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// hymd：确保 HyMd 进程、绑定当前前台 HyMd 窗口、下发图纸上下文。贴图由 HyMd 发起。
    /// </summary>
    public class DesignSpecCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            HyMdCadBridge.Start();
            if (!HyMdCadBridge.EnsureProcess(ed))
                return;

            string dwgPath = null;
            try { dwgPath = doc.Name; } catch { }

            var settings = HyMdCadSettings.Load();
            double scale = settings.Scale > 0 ? settings.Scale : 1.0;
            string styleName = "0-hy-说明-T";
            try
            {
                var vm = CadHost.Settings;
                if (vm != null && !string.IsNullOrWhiteSpace(vm.StyleTName))
                    styleName = vm.StyleTName;
            }
            catch { }

            HyMdCadBridge.SendBindAndContext(dwgPath, scale, styleName);
            ed.WriteMessage("\n已绑定 HyMd。在带 CAD 标记的窗口中打开说明，点「贴到图纸」，再回图纸选左上角。");
        }
    }
}
