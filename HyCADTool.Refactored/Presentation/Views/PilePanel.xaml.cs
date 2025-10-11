using System.Windows.Controls;
using Autofac;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// PilePanel.xaml 的交互逻辑
    /// 桩基布置面板（已迁移自原项目）
    /// </summary>
    public partial class PilePanel : UserControl
    {
        /// <summary>
        /// 无参构造函数（自动解析依赖）
        /// </summary>
        public PilePanel()
        {
            InitializeComponent();
            
            // 如果 DataContext 未设置，尝试从容器解析
            if (DataContext == null && ServiceLocator.Container != null)
            {
                try
                {
                    DataContext = ServiceLocator.Container.Resolve<PilePanelViewModel>();
                }
                catch
                {
                    // 设计器模式或容器未初始化时忽略
                }
            }
        }

        /// <summary>
        /// DI 注入的构造函数
        /// </summary>
        public PilePanel(ICadService cadService, IAreaFactory areaFactory, IConfigService configService)
        {
            InitializeComponent();
            DataContext = new PilePanelViewModel(cadService, areaFactory, configService);
        }

        /// <summary>
        /// ViewModel 注入的构造函数
        /// </summary>
        public PilePanel(PilePanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

