using System;
using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 路线工作台 UserControl（三列 Blender 布局）。
    /// 设计要点：
    ///  1) 本地 Merge <c>BlenderTheme.xaml</c> + <c>RoadDesignerStyles.xaml</c>（XAML 中已完成），
    ///     降低宿主隐式 Style / DynamicResource 污染风险（pitfall B1 / B2 / B9 / B14）；
    ///  2) 默认构造器在设计期可用；运行期由 <see cref="RoadAlignmentWorkbenchWindow"/> + <see cref="PanelManager.ShowAlignmentWorkbench"/> 注入 ViewModel；
    ///  3) 卸载时释放 <see cref="RoadAlignmentWorkbenchViewModel"/>（释放 PreviewService 的 AutoCAD 瞬态句柄）。
    /// </summary>
    public partial class RoadAlignmentWorkbenchPanel : UserControl
    {
        public RoadAlignmentWorkbenchPanel()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>获取 / 设置视图模型（便于 PanelManager 调用 <see cref="RoadAlignmentWorkbenchViewModel.SelectAlignment"/>）。</summary>
        public RoadAlignmentWorkbenchViewModel ViewModel
        {
            get => DataContext as RoadAlignmentWorkbenchViewModel;
            set => DataContext = value;
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // 忽略二次释放异常；窗口关闭卸载时不可让 WPF 卸载路径向上抛。
                }
            }
        }
    }
}
