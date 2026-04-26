using System.Windows.Controls;
using Autofac;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.App.Bootstrap;

namespace HyCADTool.Presentation.Views
{
    /// <summary>
    /// ClusterPanel.xaml 的交互逻辑
    /// 聚类分析面板（已迁移自原项目）
    /// </summary>
    public partial class ClusterPanel : UserControl
    {
        /// <summary>
        /// 无参构造函数（自动解析依赖）
        /// </summary>
        public ClusterPanel()
        {
            InitializeComponent();
            
            // 如果 DataContext 未设置，尝试从容器解析
            if (DataContext == null && ServiceLocator.Container != null)
            {
                try
                {
                    DataContext = ServiceLocator.Container.Resolve<ClusterPanelViewModel>();
                }
                catch
                {
                    // 设计器模式或容器未初始化时忽略
                }
            }
        }

        /// <summary>
        /// ViewModel 注入的构造函数
        /// </summary>
        public ClusterPanel(ClusterPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

