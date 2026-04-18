using Autodesk.AutoCAD.Windows;
using Autofac;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 面板管理器 —— 管理两个相互独立的 PaletteSet：
    ///
    /// 1) <see cref="Views.HyToolPanel"/>（命令 <c>Hy</c>）：原统一参数面板（设置/钢筋/底板/桩基/聚类/过滤/道路）。
    /// 2) <see cref="Views.HyBlenderPanel"/>（命令 <c>HyB</c>）：Blender 风格命令检索面板，数据驱动自 commands.json。
    ///
    /// 两个面板各自有独立 GUID / PaletteSet，可以同时显示，也可以各自 Toggle。
    /// 多文档环境下 HyToolPanel 仍按文档切换 ViewModel；HyBlenderPanel 是无状态纯命令列表，不跟随文档。
    /// </summary>
    public class PanelManager
    {
        private static readonly Guid HyToolPanelGuid    = new Guid("F6A7B8C9-D0E1-2345-FA67-890ABCDEF123");
        private static readonly Guid HyBlenderPanelGuid = new Guid("A1B2C3D4-E5F6-7890-AB12-345678901234");

        private readonly IComponentContext _componentContext;

        // 参数面板（Hy）
        private PaletteSet _hyPaletteSet;
        private Views.HyToolPanel _panelInstance;
        private bool _documentEventsRegistered;

        // Blender 命令面板（HyB）
        private PaletteSet _blenderPaletteSet;
        private Views.HyBlenderPanel _blenderPanel;

        public PanelManager(IComponentContext componentContext)
        {
            _componentContext = componentContext ?? throw new ArgumentNullException(nameof(componentContext));
        }

        // ===== Hy：原参数面板 =====

        /// <summary>显示/隐藏 HY 统一参数面板。</summary>
        public void ToggleHyToolPanel()
        {
            if (_hyPaletteSet != null)
            {
                _hyPaletteSet.Visible = !_hyPaletteSet.Visible;
                if (_hyPaletteSet.Visible)
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc != null) UpdateDataContexts(doc.Name);
                }
                return;
            }

            CreateHyToolPanel();
        }

        /// <summary>显示 HY 统一参数面板（不切换）。</summary>
        public void ShowHyToolPanel()
        {
            if (_hyPaletteSet != null)
            {
                _hyPaletteSet.Visible = true;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null) UpdateDataContexts(doc.Name);
                return;
            }

            CreateHyToolPanel();
        }

        /// <summary>隐藏 HY 统一参数面板。</summary>
        public void HideHyToolPanel()
        {
            if (_hyPaletteSet != null) _hyPaletteSet.Visible = false;
        }

        /// <summary>HY 面板是否可见。</summary>
        public bool IsVisible => _hyPaletteSet != null && _hyPaletteSet.Visible;

        /// <summary>HY 参数面板实例。</summary>
        public Views.HyToolPanel PanelInstance => _panelInstance;

        // ===== HyB：Blender 命令面板 =====

        /// <summary>显示/隐藏 HyBlenderPanel。</summary>
        public void ToggleHyBlenderPanel()
        {
            if (_blenderPaletteSet != null)
            {
                _blenderPaletteSet.Visible = !_blenderPaletteSet.Visible;
                return;
            }

            CreateHyBlenderPanel();
        }

        /// <summary>显示 HyBlenderPanel（不切换）。</summary>
        public void ShowHyBlenderPanel()
        {
            if (_blenderPaletteSet != null)
            {
                _blenderPaletteSet.Visible = true;
                return;
            }

            CreateHyBlenderPanel();
        }

        /// <summary>隐藏 HyBlenderPanel。</summary>
        public void HideHyBlenderPanel()
        {
            if (_blenderPaletteSet != null) _blenderPaletteSet.Visible = false;
        }

        // ===== 旧面板兼容方法（已弃用，转发到统一面板） =====

        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowSettingsPanel()  { ShowHyToolPanel(); SetActiveTab(0); }

        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowPilePanel()      { ShowHyToolPanel(); SetActiveTab(3); }

        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowBaseReinPanel()  { ShowHyToolPanel(); SetActiveTab(2); }

        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowClusterPanel()   { ShowHyToolPanel(); SetActiveTab(4); }

        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public void ShowFilterPanel()    { ShowHyToolPanel(); SetActiveTab(5); }

        /// <summary>显示道路面板 → 打开 HY 面板并切到道路 Tab。</summary>
        public void ShowRoadPanel()      { ShowHyToolPanel(); SetActiveTab(6); }

        // ===== 私有方法 =====

        /// <summary>创建 HY 参数面板实例（原 HyToolPanel + 独立 PaletteSet）。</summary>
        private void CreateHyToolPanel()
        {
            RegisterDocumentEvents();

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var docName = doc?.Name ?? "default";
            var styleService = _componentContext.Resolve<Domain.Interfaces.IStyleService>();
            var settingsVm = ViewModels.SettingsPanelViewModel.GetOrCreate(docName, styleService);

            _panelInstance = new Views.HyToolPanel(settingsVm);

            _hyPaletteSet = new PaletteSet("HY 工具", HyToolPanelGuid)
            {
                Size = new System.Drawing.Size(320, 680),
                MinimumSize = new System.Drawing.Size(260, 420),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _hyPaletteSet.AddVisual("HY 工具", _panelInstance);
            _hyPaletteSet.Visible = true;
        }

        /// <summary>创建 Blender 命令面板实例（独立 PaletteSet）。</summary>
        private void CreateHyBlenderPanel()
        {
            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:enter", "before new VM", null, "H1");
            #endregion
            var blenderVm = new ViewModels.HyBlenderPanelViewModel();
            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:vm_created", "vm ok, tabs=" + blenderVm.Tabs.Count, new { tabs = blenderVm.Tabs.Count }, "H1");
            #endregion
            _blenderPanel = new Views.HyBlenderPanel(blenderVm);
            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:view_created", "view ok", null, "H3");
            #endregion

            _blenderPaletteSet = new PaletteSet("HyCAD 命令", HyBlenderPanelGuid)
            {
                Size = new System.Drawing.Size(320, 680),
                MinimumSize = new System.Drawing.Size(240, 360),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:before_add_visual", "before AddVisual", null, "H3");
            #endregion
            _blenderPaletteSet.AddVisual("HyCAD 命令", _blenderPanel);
            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:after_add_visual", "AddVisual ok", null, "H3");
            #endregion
            _blenderPaletteSet.Visible = true;
            #region agent log
            DebugLogger.Log("PanelManager.cs:CreateHyBlenderPanel:visible_set", "Visible=true ok", null, "H4");
            #endregion
        }

        /// <summary>切换 HY 参数面板内嵌 TabControl 的激活 Tab。</summary>
        private void SetActiveTab(int index)
        {
            if (_panelInstance == null) return;

            void Apply() => _panelInstance.MainTabControl.SelectedIndex = index;

            if (_panelInstance.Dispatcher.CheckAccess())
                Apply();
            else
                _panelInstance.Dispatcher.Invoke(Apply);
        }

        /// <summary>更新 HY 面板所有 DataContext（文档切换时）。</summary>
        private void UpdateDataContexts(string documentName)
        {
            if (_panelInstance == null) return;

            try
            {
                var styleService = _componentContext.Resolve<Domain.Interfaces.IStyleService>();
                var settingsVm = ViewModels.SettingsPanelViewModel.GetOrCreate(documentName, styleService);

                if (_panelInstance.Dispatcher.CheckAccess())
                    _panelInstance.DataContext = settingsVm;
                else
                    _panelInstance.Dispatcher.Invoke(() => _panelInstance.DataContext = settingsVm);

                _panelInstance.UpdatePilePanelDataContext(documentName);
            }
            catch
            {
                // 静默失败
            }
        }

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
