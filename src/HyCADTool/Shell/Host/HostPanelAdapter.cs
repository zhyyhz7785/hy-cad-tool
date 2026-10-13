using System;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Shell.Commands;
using HyCADTool.Shared.Host;

namespace HyCADTool.Shell
{
    /// <summary>
    /// <see cref="IHostPanels"/> 的 Shell 实现：转发到 ShowPanelCommand / PanelManager。
    /// </summary>
    public sealed class HostPanelAdapter : IHostPanels
    {
        public void ShowHyfeaBeamMvpPanel(ObjectId sourceEntityId)
            => ShowPanelCommand.ShowHyfeaBeamMvpPanel(sourceEntityId);

        public void ShowHyobHistoryPanel()
            => ShowPanelCommand.ShowHyobHistoryPanel();

        public void ShowAlignmentWorkbench(Guid? alignmentId = null, int? piIndex = null)
            => ResolvePanels()?.ShowAlignmentWorkbench(alignmentId, piIndex);

        public void ShowRoadProjectTree()
            => ResolvePanels()?.ShowRoadProjectTree();

        public void HideCrossSectionPanel()
            => ResolvePanels()?.HideCrossSectionPanel();

        public void ShowCrossSectionPanel(object existingLayout, Guid? existingTemplateId)
            => ResolvePanels()?.ShowCrossSectionPanel(existingLayout as CrossSectionLayout, existingTemplateId);

        private static PanelManager ResolvePanels()
        {
            try
            {
                return ServiceLocator.Resolve<PanelManager>();
            }
            catch
            {
                return null;
            }
        }
    }
}
