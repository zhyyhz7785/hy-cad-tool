using Autodesk.AutoCAD.Windows;
using Autofac;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 面板管理器 - 管理统一工具面板 HyToolPanel
    /// 单例模式，通过依赖注入提供
    /// 支持多文档环境：面板全局唯一，ViewModel 按文档切换
    /// </summary>
    public class PanelManager
    {
        private static readonly Guid HyToolPanelGuid = new Guid("F6A7B8C9-D0E1-2345-FA67-890ABCDEF123");

        private readonly IComponentContext _componentContext;
        private PaletteSet _paletteSet;
        private Views.HyToolPanel _panelInstance;
        private bool _documentEventsRegistered;

        public PanelManager(IComponentContext componentContext)
        {
            _componentContext = componentContext ?? throw new ArgumentNullException(nameof(componentContext));
        }

        /// <summary>
        /// 显示/隐藏统一工具面板
        /// </summary>
        public void ToggleHyToolPanel()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Visible = !_paletteSet.Visible;

                // 面板重新显示时，确保 DataContext 对应当前文档
                if (_paletteSet.Visible)
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                        UpdateDataContexts(doc.Name);
                }
                return;
            }

            // 首次创建
            CreatePanel();
        }

        /// <summary>
        /// 显示统一工具面板（不切换，仅打开）
        /// </summary>
        public void ShowHyToolPanel()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Visible = true;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                    UpdateDataContexts(doc.Name);
                return;
            }

            CreatePanel();
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HideHyToolPanel()
        {
            if (_paletteSet != null)
                _paletteSet.Visible = false;
        }

        /// <summary>
        /// 面板是否可见
        /// </summary>
        public bool IsVisible => _paletteSet != null && _paletteSet.Visible;

        /// <summary>
        /// 获取面板实例
        /// </summary>
        public Views.HyToolPanel PanelInstance => _panelInstance;

        // ===== 旧面板兼容方法（已弃用，转发到统一面板） =====

        /// <summary>
        /// [已弃用] 显示设置面板 → 打开统一面板并切到样式 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowSettingsPanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(0);
        }

        /// <summary>
        /// [已弃用] 显示桩基面板 → 打开统一面板并切到桩基 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowPilePanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(3);
        }

        /// <summary>
        /// [已弃用] 显示基础配筋面板 → 打开统一面板并切到底板 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowBaseReinPanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(2);
        }

        /// <summary>
        /// [已弃用] 显示聚类面板 → 打开统一面板并切到聚类 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowClusterPanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(4);
        }

        /// <summary>
        /// [已弃用] 显示过滤器面板 → 打开统一面板并切到过滤 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowFilterPanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(5);
        }

        /// <summary>
        /// 显示道路面板 → 打开统一面板并切到道路 Tab
        /// </summary>
        public void ShowRoadPanel()
        {
            ShowHyToolPanel();
            if (_panelInstance != null)
                SetActiveTab(6);
        }

        // ===== 私有方法 =====

        /// <summary>
        /// 创建面板实例和 PaletteSet
        /// </summary>
        private void CreatePanel()
        {
            RegisterDocumentEvents();

            // 获取当前文档的 SettingsPanelViewModel
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var docName = doc?.Name ?? "default";
            var styleService = _componentContext.Resolve<Domain.Interfaces.IStyleService>();
            var settingsVm = ViewModels.SettingsPanelViewModel.GetOrCreate(docName, styleService);

            // 创建面板实例
            _panelInstance = new Views.HyToolPanel(settingsVm);

            // 创建 PaletteSet（使用 AddVisual 替代 ElementHost，与旧面板一致）
            _paletteSet = new PaletteSet("HY 工具", HyToolPanelGuid)
            {
                Size = new System.Drawing.Size(300, 650),
                MinimumSize = new System.Drawing.Size(240, 400),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _paletteSet.AddVisual("HY 工具", _panelInstance);

            _paletteSet.Visible = true;
        }

        /// <summary>
        /// 设置活动 Tab 索引
        /// </summary>
        private void SetActiveTab(int index)
        {
            if (_panelInstance == null) return;

            if (_panelInstance.Dispatcher.CheckAccess())
            {
                _panelInstance.MainTabControl.SelectedIndex = index;
            }
            else
            {
                _panelInstance.Dispatcher.Invoke(() => _panelInstance.MainTabControl.SelectedIndex = index);
            }
        }

        /// <summary>
        /// 更新面板所有 DataContext（文档切换时）
        /// </summary>
        private void UpdateDataContexts(string documentName)
        {
            if (_panelInstance == null) return;

            try
            {
                // 更新 Tab 1-2: SettingsPanelViewModel
                var styleService = _componentContext.Resolve<Domain.Interfaces.IStyleService>();
                var settingsVm = ViewModels.SettingsPanelViewModel.GetOrCreate(documentName, styleService);

                if (_panelInstance.Dispatcher.CheckAccess())
                {
                    _panelInstance.DataContext = settingsVm;
                }
                else
                {
                    _panelInstance.Dispatcher.Invoke(() => _panelInstance.DataContext = settingsVm);
                }

                // 更新 Tab 4: PilePanel
                _panelInstance.UpdatePilePanelDataContext(documentName);
            }
            catch
            {
                // 静默失败
            }
        }

        /// <summary>
        /// 注册文档切换事件
        /// </summary>
        private void RegisterDocumentEvents()
        {
            if (_documentEventsRegistered) return;

            try
            {
                AcApp.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
                AcApp.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
                _documentEventsRegistered = true;
            }
            catch
            {
                // 静默失败
            }
        }

        private void OnDocumentBecameCurrent(object sender, Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            UpdateDataContexts(e.Document.Name);
        }

        private void OnDocumentToBeDestroyed(object sender, Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            ViewModels.SettingsPanelViewModel.RemoveDocument(e.Document.Name);
            ViewModels.PilePanelViewModel.RemoveDocument(e.Document.Name);
        }
    }
}
