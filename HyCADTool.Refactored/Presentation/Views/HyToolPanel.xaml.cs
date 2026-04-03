using System.Windows.Controls;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Views
{
    /// <summary>
    /// HyToolPanel - 统一工具面板
    /// 单 PaletteSet + 内部 WPF TabControl，承载所有模块
    /// Tab 1-2 绑定 SettingsPanelViewModel，Tab 3-6 内嵌独立 Panel
    /// </summary>
    public partial class HyToolPanel : UserControl
    {
        /// <summary>
        /// 无参构造函数（PanelManager / Activator 使用）
        /// </summary>
        public HyToolPanel()
        {
            InitializeComponent();
            WireEmbeddedPanelDataContexts();
        }

        /// <summary>
        /// 带 ViewModel 注入的构造函数
        /// </summary>
        public HyToolPanel(SettingsPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            WireEmbeddedPanelDataContexts();
        }

        /// <summary>
        /// 为内嵌的 Panel 设置 DataContext
        /// 注意：不能用 DataContext == null 判断，因为 WPF DataContext 向下继承，
        /// 父级设置后子控件会继承父级的 ViewModel（SettingsPanelViewModel），不会是 null。
        /// 必须无条件覆盖为各面板自己的 ViewModel。
        /// </summary>
        private void WireEmbeddedPanelDataContexts()
        {
            if (ServiceLocator.Container == null) return;

            // BaseReinPanel: 需要 IBaseReinforcementService 来创建 ViewModel
            try
            {
                var reinforcementService = ServiceLocator.Container.Resolve<IBaseReinforcementService>();
                EmbeddedBaseReinPanel.DataContext = new BaseReinPanelViewModel(reinforcementService);
            }
            catch
            {
                // BaseReinPanel 初始化失败，不影响其他面板
            }

            // PilePanel: 使用 per-document ViewModel
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var docName = doc?.Name ?? "default";
                EmbeddedPilePanel.DataContext = PilePanelViewModel.GetOrCreate(docName);
            }
            catch
            {
                // PilePanel 初始化失败，静默处理
            }

            // SettlementPanel 已改为独立窗口，不再嵌入 Tab
        }

        /// <summary>
        /// 更新 PilePanel 的 DataContext（文档切换时调用）
        /// </summary>
        public void UpdatePilePanelDataContext(string documentName)
        {
            if (Dispatcher.CheckAccess())
            {
                EmbeddedPilePanel.DataContext = PilePanelViewModel.GetOrCreate(documentName);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    EmbeddedPilePanel.DataContext = PilePanelViewModel.GetOrCreate(documentName);
                });
            }
        }
    }
}
