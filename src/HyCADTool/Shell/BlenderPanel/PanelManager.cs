using Autodesk.AutoCAD.Windows;
using Autofac;
using System;
using HyCADTool.Domain.ValueObjects.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Presentation
{
    /// <summary>
    /// 面板管理器 —— 唯一的 PaletteSet（<see cref="Views.HyBlenderPanel"/>，命令 <c>Hy</c>/<c>HyB</c>）：
    /// - <c>Hy</c>      → 打开面板并跳到「设置」伪 Tab
    /// - <c>HyB</c>     → 打开/关闭面板（默认 Tab）
    /// - 过滤/桩基/聚类… → 打开面板并跳到对应分类 Tab
    ///
    /// 原 HyToolPanel 已退役、文件删除；保留的 ShowXxxPanel() 只作为回退入口，全部转发到
    /// <see cref="OpenHyBlenderPanelAndSelectTab"/>。多文档切换在 HyBlenderPanel 内部处理。
    /// </summary>
    public class PanelManager
    {
        private static readonly Guid HyBlenderPanelGuid = new Guid("A1B2C3D4-E5F6-7890-AB12-345678901234");

        /// <summary>路线工作台 PaletteSet GUID（2026-04-21 切到 PaletteSet 宿主后新增）。</summary>
        private static readonly Guid AlignmentWorkbenchPaletteGuid = new Guid("B2C3D4E5-F607-8901-BC23-456789012345");

        /// <summary>项目树 PaletteSet GUID（045 / M6）。</summary>
        private static readonly Guid RoadProjectTreePaletteGuid = new Guid("C3D4E5F6-0708-9012-CD34-56789012345A");

        /// <summary>横断面绘制（RCS）PaletteSet GUID。</summary>
        private static readonly Guid CrossSectionPaletteGuid = new Guid("D4E5F6A7-B8C9-0123-DE45-678901234567");

        private readonly IComponentContext _componentContext;

        private PaletteSet _blenderPaletteSet;
        private Views.HyBlenderPanel _blenderPanel;
        private bool _documentEventsRegistered;

        // ===== 路线工作台（PaletteSet 宿主，默认停靠在 AutoCAD 底部 = editor 上方 / 命令行上方） =====
        private PaletteSet _alignmentPaletteSet;
        private Views.Road.RoadAlignmentWorkbenchPanel _alignmentPanel;
        private ViewModels.Road.RoadAlignmentWorkbenchViewModel _alignmentVm;

        // ===== 项目树（045 / M6，独立 PaletteSet，默认停靠在左侧） =====
        private PaletteSet _projectTreePaletteSet;
        private Views.Road.RoadProjectTreePanel _projectTreePanel;
        private ViewModels.Road.RoadProjectTreeViewModel _projectTreeVm;

        // ===== 横断面绘制 v2（PaletteSet + CrossSectionDrawPanel） =====
        private PaletteSet _crossSectionPaletteSet;
        private Views.Road.CrossSectionDrawPanel _crossSectionPanel;
        private ViewModels.Road.CrossSectionDrawViewModel _crossSectionVm;

        /// <summary>
        /// 路线工作台 PaletteSet 上一次 <c>StateChanged</c> 观察到的 Visible 值，用于做边缘触发：
        /// 只有在 Visible 从 true → false 时才调 <c>HideAllWorkbenchArtifacts</c>，
        /// 从 false → true 时才调 <c>RestoreWorkbenchArtifacts</c>；避免 Dock/Float 切换重复触发。
        /// </summary>
        private bool _alignmentPaletteWasVisible;

        public PanelManager(IComponentContext componentContext)
        {
            _componentContext = componentContext ?? throw new ArgumentNullException(nameof(componentContext));
        }

        // ===== 公共入口 =====

        /// <summary>
        /// 打开 HyBlenderPanel 并跳到指定 Tab（Hy 命令的新入口：key = PreferencesTabKey 打开设置；
        /// FilterTabKey 打开过滤；其他为 commands.json 的 Category 名）。
        /// </summary>
        public void OpenHyBlenderPanelAndSelectTab(string tabKey)
        {
            if (_blenderPaletteSet == null)
                CreateHyBlenderPanel();
            else
                _blenderPaletteSet.Visible = true;

            if (_blenderPanel?.DataContext is ViewModels.HyBlenderPanelViewModel vm)
            {
                if (_blenderPanel.Dispatcher.CheckAccess())
                    vm.SelectTab(tabKey);
                else
                    _blenderPanel.Dispatcher.Invoke(() => vm.SelectTab(tabKey));
            }
        }

        // ===== HyB：Blender 命令面板 =====

        /// <summary>显示/隐藏 HyBlenderPanel（HyB 命令）。</summary>
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

        /// <summary>HyBlenderPanel 是否可见。</summary>
        public bool IsVisible => _blenderPaletteSet != null && _blenderPaletteSet.Visible;

        // ===== 旧面板入口（全部转发到 HyBlenderPanel 对应 Tab） =====
        //
        // commands.json 和旧代码仍可能通过 Hy/ShowXxxPanel 调用，保留 public 不抛异常。

        /// <summary>Hy 命令 → 打开 HyBlenderPanel 的「设置」Tab。</summary>
        public void ShowHyToolPanel()
            => OpenHyBlenderPanelAndSelectTab(ViewModels.HyBlenderPanelViewModel.PreferencesTabKey);

        /// <summary>显示设置 → 同 <see cref="ShowHyToolPanel"/>。</summary>
        public void ShowSettingsPanel()
            => OpenHyBlenderPanelAndSelectTab(ViewModels.HyBlenderPanelViewModel.PreferencesTabKey);

        /// <summary>显示桩基命令组。</summary>
        public void ShowPilePanel()      => OpenHyBlenderPanelAndSelectTab("桩基");

        /// <summary>显示底板配筋命令组。</summary>
        public void ShowBaseReinPanel()  => OpenHyBlenderPanelAndSelectTab("底板配筋");

        /// <summary>显示聚类命令组。</summary>
        public void ShowClusterPanel()   => OpenHyBlenderPanelAndSelectTab("块引线");

        /// <summary>显示过滤 Tab。</summary>
        public void ShowFilterPanel()
            => OpenHyBlenderPanelAndSelectTab(ViewModels.HyBlenderPanelViewModel.FilterTabKey);

        /// <summary>显示道路命令组。</summary>
        public void ShowRoadPanel()      => OpenHyBlenderPanelAndSelectTab("道路");

        // ===== 路线工作台入口（PaletteSet 宿主：HyRoadAlnEditPi / HyRoadA / HyRoadAlnByPi 共用） =====

        /// <summary>
        /// 显示"路线工作台" <see cref="PaletteSet"/>（2026-04-21 由独立 WPF 窗口改造）。默认停靠在
        /// AutoCAD 底部（绘图 editor 正下方 / 命令行上方），高度约 400 DIP。若传入 <paramref name="alignmentId"/>，
        /// 则打开后自动选中该线位，并把左侧 PI 列表定位到 <paramref name="piIndex"/>
        /// （null 表示不指定，默认选第一个内部 PI）。
        /// 典型调用：<c>hyRoadAlnEditPi</c> 拾取线位成功后 → <see cref="ShowAlignmentWorkbench"/>(id, pi)。
        /// </summary>
        public void ShowAlignmentWorkbench(Guid? alignmentId = null, int? piIndex = null)
        {
            RegisterDocumentEvents();

            if (_alignmentPaletteSet == null)
                CreateAlignmentWorkbenchPalette();
            else
                _alignmentPaletteSet.Visible = true;

            if (_alignmentPanel == null || _alignmentVm == null) return;

            void Apply()
            {
                _alignmentVm.RefreshAlignments();
                if (alignmentId.HasValue)
                    _alignmentVm.SelectAlignment(alignmentId.Value, piIndex);
            }

            var disp = _alignmentPanel.Dispatcher;
            if (disp.CheckAccess()) Apply();
            else disp.Invoke(Apply);
        }

        /// <summary>路线工作台 PaletteSet 是否仍可见。</summary>
        public bool IsAlignmentWorkbenchVisible
            => _alignmentPaletteSet != null && _alignmentPaletteSet.Visible;

        // ===== 045 / M6：项目树（Road Project Tree） =====

        /// <summary>
        /// 显示"项目树" PaletteSet（045 / M6）。命令入口 <c>hyRoadTree</c> / 别名 <c>rTree</c>。
        /// <para>默认停靠在左侧（与 HyBlenderPanel 并列），宽 320；可拖浮动。</para>
        /// <para>每次显示会从当前文档的 <see cref="HyCADTool.Shared.AutoCAD.Services.Road.RoadProjectRegistry"/> 取项目重新建树。</para>
        /// </summary>
        public void ShowRoadProjectTree()
        {
            RegisterDocumentEvents();

            if (_projectTreePaletteSet == null)
                CreateRoadProjectTreePalette();
            else
                _projectTreePaletteSet.Visible = true;

            RefreshProjectTreeFromCurrentDocument();
        }

        /// <summary>项目树 PaletteSet 是否可见。</summary>
        public bool IsRoadProjectTreeVisible
            => _projectTreePaletteSet != null && _projectTreePaletteSet.Visible;

        // ===== RCS：横断面绘制（hyRoadCs / hyRoadCsLoad） =====

        /// <summary>
        /// 显示横断面绘制 <see cref="PaletteSet"/>（底部停靠，与会话内路线工作台同区）。
        /// 每次调用会新建 <see cref="ViewModels.Road.CrossSectionDrawViewModel"/> 并注入面板。
        /// </summary>
        public void ShowCrossSectionPanel(CrossSectionLayout initialLayout = null, Guid? existingTemplateId = null)
        {
            RegisterDocumentEvents();

            if (_crossSectionPaletteSet == null)
                CreateCrossSectionPalette();
            else
                _crossSectionPaletteSet.Visible = true;

            if (_crossSectionPanel == null) return;

            _crossSectionVm = new ViewModels.Road.CrossSectionDrawViewModel(initialLayout, existingTemplateId);
            _crossSectionPanel.ViewModel = _crossSectionVm;
            TryStripCrossSectionCaptionIfDocked();
        }

        /// <summary>隐藏横断面 PaletteSet（<c>Esc</c> 快捷键等）。</summary>
        public void HideCrossSectionPanel()
        {
            if (_crossSectionPaletteSet != null)
                _crossSectionPaletteSet.Visible = false;
        }

        /// <summary>横断面面板是否可见。</summary>
        public bool IsCrossSectionPanelVisible
            => _crossSectionPaletteSet != null && _crossSectionPaletteSet.Visible;

        private void CreateCrossSectionPalette()
        {
            _crossSectionPanel = new Views.Road.CrossSectionDrawPanel();

            _crossSectionPaletteSet = new PaletteSet("横断面绘制", CrossSectionPaletteGuid)
            {
                Size = new System.Drawing.Size(1340, 780),
                MinimumSize = new System.Drawing.Size(1100, 640),
                DockEnabled = (DockSides)((int)DockSides.Bottom | (int)DockSides.Top),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _crossSectionPaletteSet.AddVisual("横断面绘制", _crossSectionPanel);
            _crossSectionPaletteSet.StateChanged += OnCrossSectionPaletteStateChanged;
            _crossSectionPaletteSet.Dock = DockSides.Bottom;
            _crossSectionPaletteSet.Visible = true;

            TryStripCrossSectionCaptionIfDocked();
        }

        private void OnCrossSectionPaletteStateChanged(object sender, PaletteSetStateEventArgs e)
        {
            TryStripCrossSectionCaptionIfDocked();
        }

        private void TryStripCrossSectionCaptionIfDocked()
        {
            if (_crossSectionPaletteSet == null || _crossSectionPanel == null) return;
            if (_crossSectionPaletteSet.Dock == DockSides.None) return;

            _crossSectionPanel.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        HyCADTool.Shell.Ribbon.PaletteTitleBarStripper.TryStripCaption("横断面绘制");
                    }
                    catch
                    {
                    }
                }));
        }

        private void CreateRoadProjectTreePalette()
        {
            // 从 DI 取 VM（含注入的 handler）；若 DI 未注册则退回默认 Null handler
            try
            {
                _projectTreeVm = _componentContext.Resolve<ViewModels.Road.RoadProjectTreeViewModel>();
            }
            catch
            {
                _projectTreeVm = new ViewModels.Road.RoadProjectTreeViewModel();
            }

            _projectTreePanel = new Views.Road.RoadProjectTreePanel { ViewModel = _projectTreeVm };

            _projectTreePaletteSet = new PaletteSet("项目树", RoadProjectTreePaletteGuid)
            {
                Size = new System.Drawing.Size(320, 720),
                MinimumSize = new System.Drawing.Size(240, 360),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _projectTreePaletteSet.AddVisual("项目树", _projectTreePanel);
            _projectTreePaletteSet.StateChanged += OnProjectTreePaletteStateChanged;
            _projectTreePaletteSet.Visible = true;

            TryStripProjectTreeCaptionIfDocked();
        }

        /// <summary>
        /// 按当前 AutoCAD MdiActiveDocument 刷新项目树：
        /// <c>RoadProjectRegistry.GetOrCreateForDocument</c> → 喂给 VM；失败或无 DI 时静默。
        /// </summary>
        public void RefreshProjectTreeFromCurrentDocument()
        {
            if (_projectTreeVm == null) return;
            try
            {
                var doc = AcApp.DocumentManager?.MdiActiveDocument;
                if (doc == null) return;
                var reg = _componentContext.Resolve<HyCADTool.Shared.AutoCAD.Services.Road.RoadProjectRegistry>();
                var project = reg?.GetOrCreateForDocument(doc.Name);
                if (project == null) return;

                var disp = _projectTreePanel?.Dispatcher;
                void Apply() => _projectTreeVm.Project = project;
                if (disp == null || disp.CheckAccess()) Apply();
                else disp.Invoke(Apply);
            }
            catch
            {
                // DI 缺 RoadProjectRegistry 或 doc 拿不到，安静吞
            }
        }

        private void OnProjectTreePaletteStateChanged(object sender, PaletteSetStateEventArgs e)
        {
            TryStripProjectTreeCaptionIfDocked();
        }

        private void TryStripProjectTreeCaptionIfDocked()
        {
            if (_projectTreePaletteSet == null || _projectTreePanel == null) return;
            if (_projectTreePaletteSet.Dock == DockSides.None) return;

            _projectTreePanel.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        HyCADTool.Shell.Ribbon.PaletteTitleBarStripper.TryStripCaption("项目树");
                    }
                    catch { }
                }));
        }

        /// <summary>
        /// 创建路线工作台 PaletteSet（一次性创建、全会话复用，对齐 <see cref="CreateHyBlenderPanel"/> 模式）。
        /// 初始停靠在 AutoCAD 底部（Editor 正下方 / 命令行上方），高度约 400 DIP。
        /// </summary>
        private void CreateAlignmentWorkbenchPalette()
        {
            _alignmentVm = new ViewModels.Road.RoadAlignmentWorkbenchViewModel();
            _alignmentPanel = new Views.Road.RoadAlignmentWorkbenchPanel { ViewModel = _alignmentVm };

            _alignmentPaletteSet = new PaletteSet("路线工作台", AlignmentWorkbenchPaletteGuid)
            {
                Size = new System.Drawing.Size(1200, 400),
                MinimumSize = new System.Drawing.Size(720, 240),
                DockEnabled = (DockSides)((int)DockSides.Bottom | (int)DockSides.Top),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _alignmentPaletteSet.AddVisual("路线工作台", _alignmentPanel);
            _alignmentPaletteSet.StateChanged += OnAlignmentPaletteStateChanged;
            // 初始停靠在底部（editor 上方、命令行之上）；用户可拖出浮动或改停靠位。
            _alignmentPaletteSet.Dock = DockSides.Bottom;
            _alignmentPaletteSet.Visible = true;
            _alignmentPaletteWasVisible = true;

            TryStripAlignmentCaptionIfDocked();
        }

        /// <summary>
        /// 监听 Dock/Float/显示切换：
        /// - Dock 时抹掉原生标题栏（对齐 <see cref="CreateHyBlenderPanel"/>）；
        /// - Visible 由 true → false（点 X / AutoHide 收起 / 程序 Visible=false）时调
        ///   <see cref="ViewModels.Road.RoadAlignmentWorkbenchViewModel.HideAllWorkbenchArtifacts"/>
        ///   把工作台产生的所有临时图形一次清零，解决"关了面板预览黄线赖在图上删不掉"的问题；
        /// - Visible 由 false → true 时调 <c>RestoreWorkbenchArtifacts</c> 按原选中线位重画主预览。
        ///
        /// 注意：StateChanged 也会在 Dock/Float 切换时触发，此时 Visible 保持不变，
        /// 这里用 <see cref="_alignmentPaletteWasVisible"/> 边缘触发，避免无谓重画 / 清理。
        /// </summary>
        private void OnAlignmentPaletteStateChanged(object sender, PaletteSetStateEventArgs e)
        {
            TryStripAlignmentCaptionIfDocked();

            if (_alignmentPaletteSet == null) return;
            bool nowVisible = _alignmentPaletteSet.Visible;
            if (nowVisible == _alignmentPaletteWasVisible) return;

            _alignmentPaletteWasVisible = nowVisible;
            try
            {
                if (!nowVisible) _alignmentVm?.HideAllWorkbenchArtifacts();
                else _alignmentVm?.RestoreWorkbenchArtifacts();
            }
            catch
            {
                // StateChanged 是 AutoCAD 原生回调，外抛会升级为宿主致命错误，必须吞掉
            }
        }

        /// <summary>
        /// 仅当 <c>_alignmentPaletteSet.Dock != DockSides.None</c> 时抹框。
        /// 延迟 1 个 Dispatcher tick 再抹，等 AutoCAD 内部 HWND 创建完毕。
        /// </summary>
        private void TryStripAlignmentCaptionIfDocked()
        {
            if (_alignmentPaletteSet == null || _alignmentPanel == null) return;
            if (_alignmentPaletteSet.Dock == DockSides.None) return;

            _alignmentPanel.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        HyCADTool.Shell.Ribbon.PaletteTitleBarStripper.TryStripCaption("路线工作台");
                    }
                    catch
                    {
                    }
                }));
        }

        /// <summary>创建 Blender 命令面板实例（独立 PaletteSet）。</summary>
        private void CreateHyBlenderPanel()
        {
            RegisterDocumentEvents();
            var blenderVm = new ViewModels.HyBlenderPanelViewModel();
            _blenderPanel = new Views.HyBlenderPanel(blenderVm);

            _blenderPaletteSet = new PaletteSet("HyCAD 命令", HyBlenderPanelGuid)
            {
                Size = new System.Drawing.Size(320, 680),
                MinimumSize = new System.Drawing.Size(240, 360),
                DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
                Style = PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.Snappable
            };

            _blenderPaletteSet.AddVisual("HyCAD 命令", _blenderPanel);
            _blenderPaletteSet.StateChanged += OnBlenderPaletteStateChanged;
            _blenderPaletteSet.Visible = true;

            TryStripBlenderCaptionIfDocked();
        }

        /// <summary>
        /// 监听 Dock/Float/显示切换；Dock 时抹掉原生标题栏，Float 时什么都不做（保留原框以便拖拽）。
        /// HWND 在 Dock/Float 切换后会重建，所以每次 StateChanged 都重抹一次。
        /// </summary>
        private void OnBlenderPaletteStateChanged(object sender, PaletteSetStateEventArgs e)
        {
            TryStripBlenderCaptionIfDocked();
        }

        /// <summary>
        /// 仅当 <c>_blenderPaletteSet.Dock != DockSides.None</c> 时抹框。
        /// 延迟 1 个 Dispatcher tick 再抹，等 AutoCAD 内部 HWND 创建完毕。
        /// </summary>
        private void TryStripBlenderCaptionIfDocked()
        {
            if (_blenderPaletteSet == null || _blenderPanel == null) return;
            if (_blenderPaletteSet.Dock == DockSides.None) return;

            _blenderPanel.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    try
                    {
                        HyCADTool.Shell.Ribbon.PaletteTitleBarStripper.TryStripCaption("HyCAD 命令");
                    }
                    catch
                    {
                    }
                }));
        }

        /// <summary>注册一次文档级事件：DocumentToBeDestroyed → 清理对应文档的 VM 缓存；DocumentActivated → 刷新路线工作台。</summary>
        private void RegisterDocumentEvents()
        {
            if (_documentEventsRegistered) return;

            try
            {
                AcApp.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
                AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                _documentEventsRegistered = true;
            }
            catch
            {
                // 静默失败
            }
        }

        private void OnDocumentToBeDestroyed(object sender, Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            ViewModels.SettingsPanelViewModel.RemoveDocument(e.Document.Name);
            HyCADTool.Features.Pile.ViewModels.PilePanelViewModel.RemoveDocument(e.Document.Name);
        }

        /// <summary>
        /// 文档切换时刷新路线工作台的 Alignment / PI / 表数据；其他面板由自身的文档缓存管理。
        /// 仅当工作台可见时才刷新，避免无谓的 JSON 读取。
        /// </summary>
        private void OnDocumentActivated(object sender, Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
        {
            // 路线工作台
            try
            {
                if (_alignmentVm != null && _alignmentPaletteSet != null && _alignmentPanel != null && _alignmentPaletteSet.Visible)
                {
                    var disp = _alignmentPanel.Dispatcher;
                    if (disp.CheckAccess())
                        _alignmentVm.RefreshAlignmentsSuppressingListLocator();
                    else
                        disp.BeginInvoke(new Action(_alignmentVm.RefreshAlignmentsSuppressingListLocator));
                }
            }
            catch { /* DocumentActivated 回调不可向外抛，否则会升级为 AutoCAD 原生致命错误 */ }

            // 045 / M6：项目树
            try
            {
                if (_projectTreeVm != null && _projectTreePaletteSet != null && _projectTreePanel != null && _projectTreePaletteSet.Visible)
                {
                    RefreshProjectTreeFromCurrentDocument();
                }
            }
            catch { }
        }
    }
}
