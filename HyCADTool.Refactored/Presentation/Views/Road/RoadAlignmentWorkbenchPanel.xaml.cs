using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
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
    ///     （pitfall：PaletteSet 宿主下 Unloaded 不等于销毁）。
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
