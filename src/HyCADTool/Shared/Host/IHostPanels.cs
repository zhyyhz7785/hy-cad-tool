using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.Host
{
    /// <summary>
    /// 稳定的宿主面板入口。Features 打开 / 关闭面板只走本接口，
    /// 不再引用 PanelManager 或 ShowPanelCommand。
    /// </summary>
    public interface IHostPanels
    {
        void ShowHyfeaBeamMvpPanel(ObjectId sourceEntityId);
        void ShowHyobHistoryPanel();
        void ShowAlignmentWorkbench(Guid? alignmentId = null, int? piIndex = null);
        void ShowRoadProjectTree();
        void HideCrossSectionPanel();
        void ShowCrossSectionPanel(object existingLayout, Guid? existingTemplateId);
    }
}
