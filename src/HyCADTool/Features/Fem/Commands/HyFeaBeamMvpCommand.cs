using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shell.Commands;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Fem.Commands
{
    /// <summary>HYFEA 梁元 MVP：选线 → 独立面板 → 均布荷载求解 → 回写。</summary>
    public sealed class HyFeaBeamMvpCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var peo = new PromptEntityOptions("\n选择代表梁的直线或多段线")
            {
                AllowNone = false,
            };
            peo.SetRejectMessage("\n仅支持 Line / LWPolyline");
            peo.AddAllowedClass(typeof(Line), false);
            peo.AddAllowedClass(typeof(Polyline), false);

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            ShowPanelCommand.ShowHyfeaBeamMvpPanel(per.ObjectId);
        }
    }
}