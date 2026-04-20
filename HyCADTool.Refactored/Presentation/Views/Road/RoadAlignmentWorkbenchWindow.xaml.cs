using System;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 路线工作台独立 WPF 窗口（非模态，<c>Application.ShowModelessWindow</c>）。
    /// 承载 <see cref="RoadAlignmentWorkbenchPanel"/>；ViewModel 生命周期由面板
    /// <see cref="RoadAlignmentWorkbenchPanel"/> 的 Unloaded 路径释放。
    /// </summary>
    public partial class RoadAlignmentWorkbenchWindow : BlenderWindow
    {
        public RoadAlignmentWorkbenchWindow(RoadAlignmentWorkbenchViewModel viewModel)
        {
            InitializeComponent();
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            WorkbenchHost.ViewModel = viewModel;
        }
    }
}
