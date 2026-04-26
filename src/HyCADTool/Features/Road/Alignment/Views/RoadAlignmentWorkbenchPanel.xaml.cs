using System.Windows.Controls;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;

namespace HyCADTool.Features.Road.PlanAlignment.Views
{
    /// <summary>
    /// 路线工作台 UserControl（三列 Blender 布局，PaletteSet 宿主）。
    /// 设计要点：
    ///  1) 本地 Merge <c>BlenderTheme.xaml</c> + <c>RoadDesignerStyles.xaml</c>（XAML 中已完成），
    ///     降低宿主隐式 Style / DynamicResource 污染风险（pitfall B1 / B2 / B9 / B14）；
    ///  2) 默认构造器在设计期可用；运行期由 <see cref="PanelManager.ShowAlignmentWorkbench"/>
    ///     创建 <see cref="PaletteSet"/> 后通过 <see cref="ViewModel"/> setter 注入 ViewModel；
    ///  3) <b>不在 Unloaded 里释放 VM</b>：PaletteSet 在 Dock/Float/显隐切换时会重新 parent，
    ///     会触发 Unloaded，但 VM 必须在整个会话内存活以保持 PreviewService 瞬态句柄
    ///     （pitfall：PaletteSet 宿主下 Unloaded 不等于销毁）；
    ///  4) <b>面板关闭时的临时图形清理交给 PanelManager</b>：
    ///     <see cref="PanelManager.OnAlignmentPaletteStateChanged"/> 监听 PaletteSet Visible
    ///     由 true → false 时调 <c>ViewModel.HideAllWorkbenchArtifacts</c>，一次清零主预览实体
    ///     （<c>05_hy_道路_预览</c>）、PI 实时预览 Transient、原线图层、UserPick 预览。
    ///     再次打开时调 <c>RestoreWorkbenchArtifacts</c> 按当前选中线位重画主预览。
    /// </summary>
    public partial class RoadAlignmentWorkbenchPanel : UserControl
    {
        public RoadAlignmentWorkbenchPanel()
        {
            InitializeComponent();
        }

        /// <summary>获取 / 设置视图模型（便于 PanelManager 调用 <see cref="RoadAlignmentWorkbenchViewModel.SelectAlignment"/>）。</summary>
        public RoadAlignmentWorkbenchViewModel ViewModel
        {
            get => DataContext as RoadAlignmentWorkbenchViewModel;
            set => DataContext = value;
        }
    }
}
