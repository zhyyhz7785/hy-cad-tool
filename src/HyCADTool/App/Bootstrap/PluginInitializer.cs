using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autofac;
using HyCADTool.Features.TextEdit.Services;
using HyCADTool.Shell.Configuration.Global;
using HyCADTool.Shell.Configuration.Modules;
using HyCADTool.Shell.Contracts;
using HyCADTool.Shell.Input;
using HyCADTool.Shell.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

// v2 架构：Refactored 不再被 AutoCAD 直接 NETLOAD，故不需要 [assembly: CommandClass]。
// 所有 AutoCAD 命令都注册在 ReCall.CommandFacade（只 NETLOAD ReCall.dll）。
// PluginInitializer.Initialize 由 ReCall.C2 通过反射每次重载后调用，等价原 IExtensionApplication 入口。
//
// 为什么不继承 IExtensionApplication？
// AutoCAD 会监听 AppDomain.AssemblyLoad 事件，即使 Refactored.dll 是通过 Assembly.Load(byte[])
// 加载的，AutoCAD 也会扫到它里面的 IExtensionApplication 实现并自动调 Initialize/Terminate，
// 结果就是 C2 反射调一次 + AutoCAD 自动调一次 = 两次初始化。去掉接口后这个自动路径消失，
// 生命周期完全由 ReCall 控制，符合 v2 架构。

namespace HyCADTool.App.Bootstrap
{
    /// <summary>
    /// 插件初始化器（v2：不再是 IExtensionApplication，由 ReCall.ReCallClass.Reload 反射调用）。
    /// </summary>
    public class PluginInitializer
    {
        private static readonly HashSet<string> _initializedDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>文档关闭时移除初始化标记，同名文件再打开时可重新初始化。</summary>
        public static void RemoveInitializedDocument(string documentName)
        {
            if (string.IsNullOrEmpty(documentName)) return;
            _initializedDocuments.Remove(documentName);
        }

        /// <summary>
        /// 插件初始化：构建 Autofac 容器、加载配置、初始化样式/图层、订阅文档事件、启动道路子系统。
        ///
        /// 调用时机（v2）：
        /// - 由 <c>ReCall.ReCallClass.Reload()</c>（C2）反射创建实例并调用，每次 C2 一个新实例。
        /// - ReCall 会在调用本方法前，先对上一次 C2 保存的旧实例调用 <see cref="Terminate"/>，
        ///   通过旧实例自己的方法引用把文档事件解绑，确保跨 C2 幂等。
        /// - 因此本方法内部无需担心"旧实例事件残留"问题。
        /// </summary>
        public void Initialize()
        {
            var swTotal = Stopwatch.StartNew();

            // 【根因修复 H6】Application.Current 在 AutoCAD 进程内为 null（AutoCAD 是 WinForms+WPF
            // 混合宿主，从未实例化 WPF Application 单例）。这直接导致：
            //   1. Application.LoadComponent(relativeUri) → IOException("Assembly.GetEntryAssembly() 返回 null")
            //   2. AutoCAD 自身 Ribbon Badge 控件 LoadComponent("/AdWindows;component/themes/badge.xaml") 同样失败
            //      → 在某个 idle tick 内升级为 0xE0434352 native fatal（AutoCAD "致命错误"弹窗）
            // 修复：在最早阶段 ensure Application 单例存在（仅创建对象，不调 .Run() 不启动消息循环），
            // 这样后续所有 WPF LoadComponent / ResourceAssembly / Dispatcher 引用都有 host 对象可用。
            var swWpf = Stopwatch.StartNew();
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    new System.Windows.Application();
                }

                // 宿主默认 ShutdownMode=OnLastWindowClose：任意模式对话框 / 面板子窗体关掉若成为
                // 「最后一个 WPF 窗口」，WPF 会开始关闭 Application，随后 hyRoadCs 等再 new
                // BlenderWindow 会在 InitializeComponent → GetResourcePackage 抛出
                // 「应用程序对象正在关闭」。插件进程内必须显式关闭才退出 WPF。
                var wpf = System.Windows.Application.Current;
                if (wpf?.Dispatcher != null && !wpf.Dispatcher.HasShutdownStarted)
                    wpf.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

                // 【真根因修复 · 2026-04-21】见 doc/RoadDesign/00.md：
                //   AutoCAD Ribbon Badge XamlParseException 的真正诱因是 ReCall 的
                //   AssemblyResolve handler 把项目引用的旧版 AdWindows.dll（5.0.1.2，
                //   AutoCAD.NET 24.3.0 NuGet 传递引用）byte[] 加载进 AppDomain，
                //   与 AutoCAD 进程实际加载的 5.1.1.1 形成"双 AdWindows"，
                //   类型身份割裂导致 BAML 资源解析失败。
                //   修复见 ReCall/Recall.cs ResolveAssembly 宿主程序集黑名单。
                //   本初始化器不需要任何 ResourceAssembly / Assembly.Load("AdWindows") 干预。
            }
            catch
            {
                // 创建 Application stub 失败时降级运行：后续 WPF 相关路径自有兜底，
                // 但 LoadComponent 大概率会跟着挂；不在此处吞错的更深处再抛更利于诊断。
            }
            long msWpfStub = swWpf.ElapsedMilliseconds;

#if HYCAD_PRODUCTION
            try
            {
                HyCADTool.Licensing.LicenseService.Instance.Refresh();
            }
            catch
            {
            }
#endif

            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool 插件初始化中...");
                WriteMessage("\n========================================");

                // 必装：WPF Dispatcher / Binding 异常兜底。PaletteSet 宿主默认会吞掉所有
                // UI 线程异常，导致面板控件首次实例化失败时表现为 AutoCAD 原生崩溃，无任何线索。
                long msWpfTraps = RunTimed(InstallWpfExceptionTraps);

                // 【关键】在任何 WPF XAML/控件被触发前，同步 warmup 跨程序集 BlenderTheme.xaml。
                // 原因：HyCAD.BlenderUI 标记了 [assembly: ThemeInfo(SourceAssembly)]，
                // WPF 首次创建其自定义控件时会在 UI tick 异步查找 Themes/Generic.xaml，
                // 此时若 pack URI 解析失败，异常会在 PresentationFramework native 层
                // 以 0xE0434352 抛出，表现为 AutoCAD "致命错误" 弹窗（无托管堆栈可捕获）。
                // 同步 warmup 把这条路径从"异步 native"变成"同步托管异常"，必崩时可见。
                long msWarmup = RunTimed(WarmupBlenderTheme);

                // 注意：不再 WarmupSubViews()。SubView 都是 UserControl，首次 new 时
                // InitializeComponent 自动 LoadComponent；BlenderTheme 已驻留缓存即可命中。
                // 历史 WarmupSubViews 用 XamlReader.Load(stream) 读 BAML 二进制流是 API 误用，
                // 11 个 SubView 全部预热失败，命令行刷 11 条假阳性 XamlParseException。
                // 见 doc/RoadDesign/00.md。

                // 构建 Autofac 容器
                long msAutofac = RunTimed(() =>
                {
                    var builder = new ContainerBuilder();
                    builder.RegisterModule<AutofacModule>();
                    var container = builder.Build();
                    ServiceLocator.Initialize(container);
                });

                WriteMessage("\n✓ 依赖注入容器已初始化");

                // 加载配置
                long msLoadCfg = RunTimed(LoadConfigurations);

                WriteMessage("\n✓ 配置已加载");

                // HyCAD 标准线型（点划线 / 虚线）：须在图层落表前注入当前图形线型表
                long msLinetypes = RunTimed(TryEnsureHyCadStandardLinetypesLoaded);

                // 一次性初始化所有样式和图层
                long msStylesLayers = RunTimed(InitializeStylesAndLayers);

                // 注册文档事件（可选）
                long msDocEvents = RunTimed(RegisterDocumentEvents);

                // 道路子系统启动（P0 落地：事件总线 + JSON 持久化）
                long msRoad = RunTimed(InitializeRoadSubsystem);

                // 三入口 UI：AutoCAD Ribbon 选项卡（Ribbon 未启用时会静默跳过，不影响其他入口）
                long msRibbonMenus = RunTimed(InitializeRibbonAndMenus);

                // 把 CommandTable 里的命令批量注册为 BlenderUI Operator（Id = hy.cmd.{key}），
                // 为后续 KeyMap / SearchMenu 统一走 WM/Operators 链路做准备。幂等，commands.json 异常静默失败。
                long msOperators = 0;
                try
                {
                    msOperators = RunTimed(OperatorBootstrapper.RegisterAllCommands);
                    WriteMessage($"\n✓ 命令 Operator 已注册：{OperatorBootstrapper.RegisteredCommandCount} 条");
                }
                catch (System.Exception ex)
                {
                    WriteMessage($"\n  ⚠ 命令 Operator 注册警告：{ex.Message}");
                }

                swTotal.Stop();
                WriteMessage("\n  ⏱ PluginInitializer 阶段耗时(ms): " +
                    $"WpfStub={msWpfStub} WpfTraps={msWpfTraps} Warmup={msWarmup} " +
                    $"Autofac+SL={msAutofac} LoadCfg={msLoadCfg} Linetypes={msLinetypes} " +
                    $"StylesLayers={msStylesLayers} DocEvents={msDocEvents} Road={msRoad} " +
                    $"RibbonMenus={msRibbonMenus} Operators={msOperators} | Total={swTotal.ElapsedMilliseconds}");

                WriteMessage("\n========================================");
                WriteMessage("\n✓ HyCADTool 插件初始化完成！");
                WriteMessage("\n========================================\n");

                WriteEntryGuide();
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n✗ [错误] 插件初始化失败：{ex.Message}");
                WriteMessage($"\n堆栈跟踪：{ex.StackTrace}");
            }
        }

        /// <summary>Layer 4：单阶段耗时（毫秒），吞异常由被测方法自行处理。</summary>
        private static long RunTimed(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            return sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// 插件终止
        /// </summary>
        public void Terminate()
        {
            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool 插件正在卸载...");

                // 保存当前面板设置
                try
                {
                    SettingsPanelViewModel.Current?.SaveSettings();
                }
                catch { }

                // 卸载三入口 UI：Ribbon 选项卡、CUIX 菜单（防 C2 热重载累积）
                TerminateRibbonAndMenus();

                // 道路子系统收尾：
                // v1.1 起各命令在 tr.Commit() 后已同步写盘，卸载时不再需要 FlushAll；
                // 如果用户在执行过命令后直接关闭 AutoCAD，数据已经在 .roaddesign.json 里。

                // 清理事件订阅
                UnregisterDocumentEvents();

                try
                {
                    ServiceLocator.TryResolve<HyCADTool.Shell.PanelManager>()?.UnregisterDocumentEvents();
                }
                catch { }

                _initializedDocuments.Clear();

                // 清理容器
                ServiceLocator.Reset();

                WriteMessage("\n✓ HyCADTool 插件已卸载");
                WriteMessage("\n========================================\n");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n✗ [错误] 插件终止失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfigurations()
        {
            try
            {
                var configService = ServiceLocator.Resolve<IConfigurationService>();
                configService.LoadAll();

                // 显示配置信息
                var globalConfig = configService.Global;
                WriteMessage($"\n  - Scale: {globalConfig.Scale.Default}");
                WriteMessage($"\n  - ElevationLength: {globalConfig.ElevationLength}");
                WriteMessage($"\n  - Tolerance: {globalConfig.Tolerance.Double}");

                var pileConfig = configService.GetModuleConfig<PileConfiguration>("Pile");
                WriteMessage($"\n  - Pile Diameter: {pileConfig.DiameterOrEdge}mm");
                WriteMessage($"\n  - Pile Section: {pileConfig.Section}");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 配置加载警告：{ex.Message}（使用默认值）");
            }
        }

        /// <summary>
        /// 从程序集旁 <c>Resources/HyCAD-Linetypes.lin</c> 按需加载「点划线」「虚线」。
        /// 无活动文档、文件缺失或加载失败时仅写命令行警告，不阻断初始化。
        /// </summary>
        private void TryEnsureHyCadStandardLinetypesLoaded()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            string asmPath = typeof(PluginInitializer).Assembly.Location;
            if (string.IsNullOrEmpty(asmPath))
            {
                WriteMessage("\n  ⚠ [线型] 无法解析程序集路径，跳过 HyCAD 标准线型注入。");
                return;
            }

            string linPath = Path.Combine(Path.GetDirectoryName(asmPath) ?? "", "Resources", "HyCAD-Linetypes.lin");
            if (!File.Exists(linPath))
            {
                WriteMessage($"\n  ⚠ [线型] 未找到 {linPath}，跳过点划线/虚线注入。");
                return;
            }

            IStyleService styleService;
            try
            {
                styleService = ServiceLocator.Resolve<IStyleService>();
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ [线型] StyleService 不可用，跳过线型注入：{ex.Message}");
                return;
            }

            try
            {
                bool needCenter;
                bool needDashed;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var lt = (LinetypeTable)tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);
                    needCenter = !lt.Has(HyLinetypeNames.Center);
                    needDashed = !lt.Has(HyLinetypeNames.Dashed);
                    tr.Commit();
                }

                if (needCenter)
                    styleService.LoadLinetype(linPath, HyLinetypeNames.Center);
                if (needDashed)
                    styleService.LoadLinetype(linPath, HyLinetypeNames.Dashed);
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ [线型] 注入 HyCAD 标准线型失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 一次性初始化所有样式和图层（插件启动时执行）
        /// 从 hy-settings.json 加载上次持久化的参数（Scale、字体等），不再硬编码
        /// 后续命令不再重复创建，仅面板参数变更时按需更新
        /// </summary>
        private void InitializeStylesAndLayers()
        {
            try
            {
                EnsureCurrentDocumentResourcesInitialized(force: true);
                WriteMessage("\n  ✓ 图层已创建");
                WriteMessage($"\n  ✓ 设置文件: {SettingsPanelViewModel.GetSettingsFilePath()}");

                // rLaw 原线层锁定：05_hy_道路_原线 启动即置 IsLocked = true。
                // 工作台内部写入 RawPick / Apply 清理需要临时 LayerLockScope.Unlock。
                try
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    if (doc != null)
                    {
                        HyCADTool.Shared.AutoCAD.Xdata.HyRoadLayerInitializer
                            .EnsureRawPolylineLayerLocked(doc);
                    }
                }
                catch
                {
                    /* 无活动文档 / 冷启动静默 */
                }

                // 主题兜底（v2 host-replace 模型）：
                // - LoadSettings 中 Theme setter 已经 Apply 过一次，把 BlenderThemeManager.Current
                //   设为目标值（此时 ColorsHost 还没创建，无 host 可改）；
                // - 这里调 Refresh()，强制按 Current 再刷一遍 host —— 若用户已先开过面板再触发
                //   插件重载，能立即生效；若 ColorsHost 尚未注册（典型首启），由 ColorsHost 构造时
                //   自检 Current 完成补刷，无需在此重复调 Apply（同主题幂等会 early-return）。
                try
                {
                    var themeName = SettingsPanelViewModel.Current?.Theme ?? "BlenderDark";
                    HyCAD.BlenderUI.Theming.BlenderThemeManager.Apply(themeName);
                    HyCAD.BlenderUI.Theming.BlenderThemeManager.Refresh();
                }
                catch
                {
                    /* Application 未就绪等场景静默：ColorsHost ctor 时会按 Current 自动补刷 */
                }

                try
                {
                    var vm = SettingsPanelViewModel.Current;
                    if (vm != null)
                    {
                        HyCAD.BlenderUI.Theming.BlenderMetricsScaleManager.CaptureBaselineIfNeeded();
                        HyCAD.BlenderUI.Theming.BlenderMetricsScaleManager.Apply(
                            vm.UiFontScale, vm.UiDensityScale, vm.UiInputWidthScale);
                    }
                }
                catch
                {
                    /* 无活动文档 / VM 未建：静默；首次打开文档后 LoadSettings 的 finally 会再 Apply */
                }
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 样式/图层初始化警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 注册文档事件
        /// </summary>
        private void RegisterDocumentEvents()
        {
            try
            {
                AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                AcApp.DocumentManager.DocumentCreated += OnDocumentCreated;
                HyEdDoubleClickInterceptor.Install();
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 事件注册警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消注册文档事件
        /// </summary>
        private void UnregisterDocumentEvents()
        {
            try
            {
                HyEdDoubleClickInterceptor.Uninstall();

                AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
                AcApp.DocumentManager.DocumentCreated -= OnDocumentCreated;
            }
            catch
            {
                // 忽略取消注册错误
            }
        }

        /// <summary>
        /// 文档激活事件处理
        /// </summary>
        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            EnsureCurrentDocumentResourcesInitialized(force: false);
        }

        /// <summary>
        /// 文档创建事件处理
        /// </summary>
        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;

            try
            {
                var styleService = ServiceLocator.Resolve<IStyleService>();
                SettingsPanelViewModel.GetOrCreate(HyCADTool.Shared.AutoCAD.Utilities.DocumentKeys.GetKey(e.Document), styleService);
            }
            catch
            {
                // 忽略新文档预热失败，切换到该文档时会再次初始化
            }
        }

        /// <summary>
        /// 启动道路子系统（v1.1）。
        ///
        /// 变更历史：
        /// - v1.0：事件驱动 + 500ms 防抖自动持久化；
        /// - v1.1：取消防抖服务，各写命令（hyRoadA / P / T / C / Save）在 <c>tr.Commit()</c> 后同步落盘，
        ///   彻底消除 SAVEAS / 多文档切换时 Timer 回调与 <c>doc.Name</c> 不一致导致的"空 JSON / 路径失配"bug。
        ///   事件总线（<see cref="Domain.Events.Road.IRoadEventBus"/>）仍保留，供 v2 UI / Blender 插件等未来订阅者使用。
        ///
        /// v2（P7）会在此处启动 <c>RoadDesignFileWatcher</c> 监听外部 JSON 修改（Blender → AutoCAD 回推）。
        /// </summary>
        private void InitializeRoadSubsystem()
        {
            try
            {
                // 预解析核心服务，保证 AutoCAD 启动后第一次 Resolve 的成本不落在用户命令上
                ServiceLocator.Resolve<HyCADTool.Shared.AutoCAD.Services.Road.RoadDesignRegistry>();
                ServiceLocator.Resolve<HyCADTool.Shared.AutoCAD.Services.Road.RoadJsonExportService>();

                WriteMessage("\n  ✓ 道路子系统已启动（命令收尾同步落盘，无防抖 Timer）");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 道路子系统启动警告：{ex.Message}");
            }
        }

        private static bool _wpfTrapsInstalled;

        /// <summary>
        /// 装 WPF UI 线程异常兜底 + Binding 错误监听。
        ///
        /// PaletteSet 宿主会在 WPF 控件首次寄宿（OnApplyTemplate / OnInitialized 等）抛异常时
        /// 吞掉异常并触发 native crash（0xE0434352 致命错误）。必须在 Dispatcher 上挂
        /// UnhandledException 以转写到 AutoCAD 命令行。
        /// </summary>
        private void InstallWpfExceptionTraps()
        {
            if (_wpfTrapsInstalled) return;
            _wpfTrapsInstalled = true;

            try
            {
                // PaletteSet 内部 Dispatcher 通常就是 Application UI 线程，挂当前 Dispatcher 已足够覆盖。
                System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        // 关闭流程中 MdiActiveDocument 可能已为 null，包一层防御
                        try
                        {
                            var doc = AcApp.DocumentManager?.MdiActiveDocument;
                            if (doc != null)
                            {
                                doc.Editor?.WriteMessage($"\n  ✗ [WPF UI] {ex.GetType().Name}: {ex.Message}");
                                if (ex.InnerException != null)
                                    doc.Editor?.WriteMessage($"\n    内层：{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                                doc.Editor?.WriteMessage($"\n    堆栈：{ex.StackTrace}");
                            }
                        }
                        catch { }

                        // 标记已处理，让 AutoCAD 不要把它升级成致命错误
                        e.Handled = true;
                    }
                    catch { }
                };

                // Binding 错误（找不到资源 / DataContext 类型不匹配）默认只进 Debug Output，
                // 这里强制写到命令行
                System.Diagnostics.PresentationTraceSources.Refresh();
                System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
                    new BindingErrorListener(WriteMessage));
                System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
                    System.Diagnostics.SourceLevels.Error;

                WriteMessage("\n  ✓ WPF 异常兜底已装");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ WPF 异常兜底安装失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 把 PresentationTraceSources 错误转发到 AutoCAD 命令行。
        /// 过滤掉 AutoCAD 自家 Ribbon (Autodesk.Windows.ComboBoxControl 等) 的噪音，
        /// 只保留来自 HyCAD 命名空间或 BlenderUI 的错误。
        /// </summary>
        private sealed class BindingErrorListener : System.Diagnostics.TraceListener
        {
            private readonly Action<string> _write;
            public BindingErrorListener(Action<string> write) { _write = write; }
            public override void Write(string message) { TryWrite(message, false); }
            public override void WriteLine(string message) { TryWrite(message, true); }

            private void TryWrite(string message, bool newline)
            {
                try
                {
                    if (string.IsNullOrEmpty(message)) return;

                    // 白名单：只保留本项目相关的 binding 错误
                    // （AutoCAD 内部 Menu/Ribbon/Badge 的 mBorder 模板会刷几百条，
                    //  逐条 forward 到命令栏会同步阻塞 UI 线程导致点击卡死）
                    bool isProjectRelevant =
                        message.IndexOf("HyCAD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        message.IndexOf("BaseReinVm", StringComparison.Ordinal) >= 0 ||
                        message.IndexOf("Settings.", StringComparison.Ordinal) >= 0 ||
                        message.IndexOf("PreferencesVm", StringComparison.Ordinal) >= 0 ||
                        message.IndexOf("FilterVm", StringComparison.Ordinal) >= 0 ||
                        message.IndexOf("SettingsVm", StringComparison.Ordinal) >= 0;

                    if (!isProjectRelevant) return;

                    // 项目相关的 binding 错误也不再 forward 到命令栏（同步 IO 会阻塞 UI 线程）；
                    // 仅把消息丢到 Debug 输出，需要诊断时再用 PresentationTraceSources 临时打开。
                    System.Diagnostics.Debug.WriteLine("[HyCAD WPF Binding] " + message);
                }
                catch { }
            }
        }

        /// <summary>
        /// 同步 warmup 跨程序集 Blender 主题字典。必须在任何 WPF 控件创建、Ribbon 构建前调用。
        ///
        /// 触发条件 & 症状（2026-04-18 已修复）：
        /// - HyCAD.BlenderUI 独立 csproj 后，Refactored shim BlenderTheme.xaml 跨程序集合并
        ///   `pack://application:,,,/HyCAD.BlenderUI;component/Themes/BlenderTheme.xaml`。
        /// - HyCAD.BlenderUI.AssemblyInfo 含 [assembly: ThemeInfo(SourceAssembly)]，
        ///   WPF 首次构造其自定义控件时在 Dispatcher 异步 tick 查 Themes/Generic.xaml，
        ///   该字典又合并 9 条跨程序集 pack URI。
        /// - 若 BlenderUI 通过 Assembly.Load(byte[]) 加载（ReCall 热重载路径），.Location 为空，
        ///   WPF native 解析 pack URI 路径失稳 → 0xE0434352 致命错误，无托管栈。
        /// - 主动同步加载把该问题从"C2 返回后 native 崩"转为"Initialize 同步托管异常可见"。
        /// </summary>
        private void WarmupBlenderTheme()
        {
            try
            {
                // 先 log AppDomain 里 BlenderUI 的状态，便于判断 ReCall 预加载是否生效
                var blenderAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "HyCAD.BlenderUI", StringComparison.OrdinalIgnoreCase));
                if (blenderAsm == null)
                {
                    WriteMessage("\n  ⚠ BlenderUI 未在 AppDomain 中（ReCall 预加载未生效，将触发 AssemblyResolve）");
                }
                else
                {
                    var loc = string.IsNullOrEmpty(blenderAsm.Location) ? "(byte[] 加载，.Location 空)" : blenderAsm.Location;
                    WriteMessage($"\n  BlenderUI 已在 AppDomain: {loc}");
                }

                var themeUri = new System.Uri(
                    "pack://application:,,,/HyCAD.BlenderUI;component/Themes/BlenderTheme.xaml",
                    System.UriKind.Absolute);

                // 【根因修复 H6】Application.LoadComponent(absoluteUri) 在 IsAbsoluteUri==true 时
                // 抛 ArgumentException("无法使用绝对 URI")。WPF 跨程序集 ResourceDictionary 加载的
                // 标准 API 是 `new ResourceDictionary { Source = absoluteUri }`，
                // XAML 中的 <ResourceDictionary Source="pack://..."/> 走的就是这条路径，接受绝对 pack URI，
                // 内部同样会同步驱动 BAML 反序列化 + PackUriHelper 缓存填充。
                var dict = new System.Windows.ResourceDictionary { Source = themeUri };
                if (dict != null)
                {
                    // ⚠ 严禁 merge 到 Application.Current.Resources！
                    //
                    // 历史教训（hycad-project-pitfalls B1/B2 + 2026-04-19 Badge 崩溃）：
                    // Application.Current.Resources 是整个 WPF 进程的全局资源根，
                    // AutoCAD 自己的 Ribbon / PanelListView / Badge 等内部控件在 measure /
                    // ApplyTemplate 阶段会沿可视/逻辑树向上冒泡到 Application 顶层查资源。
                    // 一旦把 BlenderTheme（含 32+ 子字典 / 32 个 Brush_* + 自定义控件 Style）merge 进去：
                    //   1. AutoCAD 内部控件资源查找路径被外部字典拦截；
                    //   2. v3 ColorsHost ctor 自我修改字典会触发 ResourcesChanged 全局广播；
                    //   3. 已观察到的最致命表现：Autodesk.Internal.Windows.Badge ApplyTemplate
                    //      时报 "组件 Badge 不具有由 URI '/AdWindows;component/themes/badge.xaml'
                    //      识别的资源" XamlParseException，连锁污染整个 PanelListView 渲染。
                    //
                    // 正确做法：LoadComponent 已经把 BAML 解析结果缓存到 PackUriHelper（AppDomain 级
                    // 静态缓存）；后续任何 UserControl 通过 <ResourceDictionary Source="..."/> 引用
                    // 同一 pack URI 都命中此缓存，无需 Application merge 也能秒级初始化。
                    // 主题资源全部走 UserControl.Resources 局部 merge（HyBlenderPanel.xaml 等），
                    // 严格隔离在我们自己的视觉子树内，绝不冒泡污染 AutoCAD 宿主。
                    WriteMessage($"\n  ✓ BlenderUI 主题预热完成（顶层 {dict.Count} 条、子字典 {dict.MergedDictionaries.Count} 层；BAML 已驻留 PackUriHelper 缓存，未污染 Application 资源）");
                }
                else
                {
                    WriteMessage("\n  ⚠ BlenderUI 主题预热返回 null（BAML 解析成功但类型不是 ResourceDictionary）");
                }
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ✗ BlenderUI 主题预热失败：{ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    WriteMessage($"\n    内层：{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                WriteMessage($"\n    堆栈：{ex.StackTrace}");
            }

            // 单独把 BlenderWindow.xaml 模板提前一次解析。
            // 原因：Generic.xaml 由 WPF 在 DefaultStyleKey 命中时按需异步加载，
            //       Road 命令第一次 new XxxWindow 时才会触发；如果跨程序集 pack URI 解析这条路径
            //       和 BlenderTheme 一样有 native 风险，必须前置到 Initialize 同步阶段。
            try
            {
                var winUri = new System.Uri(
                    "pack://application:,,,/HyCAD.BlenderUI;component/Themes/Controls/BlenderWindow.xaml",
                    System.UriKind.Absolute);

                // 同 H6：避免 LoadComponent(absoluteUri) → ArgumentException
                var winDict = new System.Windows.ResourceDictionary { Source = winUri };
                if (winDict != null)
                {
                    WriteMessage($"\n  ✓ BlenderWindow 模板预热完成（顶层资源 {winDict.Count} 条）");
                }
                else
                {
                    WriteMessage("\n  ⚠ BlenderWindow 模板预热返回 null");
                }
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ✗ BlenderWindow 模板预热失败：{ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    WriteMessage($"\n    内层：{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }

        /// <summary>
        /// 构建 AutoCAD Ribbon 选项卡 + CUIX 菜单栏。数据源与 Blender 面板同一份 commands.json。
        /// 对 Ribbon 未就绪场景（ComponentManager.Ribbon == null）做静默兜底，不影响其他入口。
        /// </summary>
        private long _msRibbonBuild;
        private long _msCuixEnsure;

        private void InitializeRibbonAndMenus()
        {
            _msRibbonBuild = 0;
            _msCuixEnsure = 0;
            // 这两个状态供 WriteEntryGuide 末尾的提示文案使用，避免重复尝试。
            _ribbonReady = false;
            _cuixLoaded = false;

            var swR = Stopwatch.StartNew();
            // 用户级开关：HYCAD_RIBBON_DISABLED=1 → 完全跳过 Ribbon 构建（CUIX 菜单仍走原流程）。
            // 适合"只用菜单栏 + 命令面板入口"的轻量配置，省下 RibbonBuild 段和 144 个 RibbonButton/RibbonToolTip 常驻 WPF 对象。
            bool ribbonDisabled = string.Equals(
                Environment.GetEnvironmentVariable("HYCAD_RIBBON_DISABLED"), "1", StringComparison.Ordinal);
            if (ribbonDisabled)
            {
                WriteMessage("\n  ⚙ Ribbon 已按 HYCAD_RIBBON_DISABLED=1 关闭（仅保留菜单栏 + 命令面板入口）");
            }
            else
            {
                try
                {
                    HyCADTool.Shell.Ribbon.HyCadRibbonBuilder.Build();
                    _ribbonReady = Autodesk.Windows.ComponentManager.Ribbon != null;
                    WriteMessage(_ribbonReady
                        ? "\n  ✓ Ribbon 选项卡 HyCAD 已挂载"
                        : "\n  ⚠ Ribbon 未启用（ComponentManager.Ribbon == null），输 _RIBBON 打开后会自动重挂");
                }
                catch (System.Exception ex)
                {
                    WriteMessage($"\n  ⚠ Ribbon 构建失败（跳过，不影响 Blender 面板）：{ex.Message}");
                }
            }
            _msRibbonBuild = swR.ElapsedMilliseconds;

            var swC = Stopwatch.StartNew();
            try
            {
                _cuixLoaded = HyCADTool.Shell.Ribbon.CuiMenuBuilder.EnsureLoaded();
                WriteMessage(_cuixLoaded
                    ? "\n  ✓ CUIX 菜单栏 HyCAD 已加载"
                    : "\n  ⚠ CUIX 菜单加载未成功（详见上方日志）");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ CUIX 菜单加载失败（跳过，不影响其他入口）：{ex.Message}");
            }
            _msCuixEnsure = swC.ElapsedMilliseconds;

            WriteMessage($"\n  ⏱ RibbonMenus 子阶段(ms): RibbonBuild={_msRibbonBuild} CuixEnsure={_msCuixEnsure}");
        }

        /// <summary>状态位：用于 WriteEntryGuide 末尾输出针对性的引导。</summary>
        private bool _ribbonReady;
        private bool _cuixLoaded;

        /// <summary>
        /// 在所有初始化完成后输出"三入口"提示，让用户首次启动就知道往哪点。
        /// 包含 MENUBAR=0 / RIBBONCLOSE 这种"看不到入口"场景的具体救急命令。
        /// </summary>
        private void WriteEntryGuide()
        {
            WriteMessage("\n┌─ HyCAD 入口指引 ─────────────────────────");
            WriteMessage("\n│ 1) 命令面板（推荐）：在 AutoCAD 命令行输 Hy 打开统一面板");
            WriteMessage("\n│                       或 HyB 打开 Blender 风格命令检索");

            bool ribbonDisabled = string.Equals(
                Environment.GetEnvironmentVariable("HYCAD_RIBBON_DISABLED"), "1", StringComparison.Ordinal);
            if (ribbonDisabled)
                WriteMessage("\n│ 2) Ribbon 选项卡：已按 HYCAD_RIBBON_DISABLED=1 关闭（节省启动 ~300ms）");
            else if (_ribbonReady)
                WriteMessage("\n│ 2) Ribbon 选项卡：顶部切到 \"HyCAD\" 选项卡（看不见？输 _RIBBON）");
            else
                WriteMessage("\n│ 2) Ribbon 选项卡：当前 Ribbon 未启用，输 _RIBBON 打开后会自动重挂");

            if (_cuixLoaded)
            {
                if (HyCADTool.Shell.Ribbon.CuiMenuBuilder.IsMenuBarHidden())
                    WriteMessage("\n│ 3) 菜单栏 \"HyCAD\"：当前 MENUBAR=0 隐藏中，输 MENUBAR 设为 1 即可看到");
                else
                    WriteMessage("\n│ 3) 菜单栏 \"HyCAD\"：顶部菜单栏的 HyCAD 菜单");
            }
            else
            {
                WriteMessage("\n│ 3) 菜单栏 \"HyCAD\"：本次未加载（看 ⚠ 日志），命令面板与 Ribbon 仍可用");
            }

            WriteMessage("\n│ 4) 出问题时：输 hyRecallSelfCheck 看诊断快照；改业务后输 C2 热重载");
            WriteMessage("\n└──────────────────────────────────────────\n");
        }

        /// <summary>
        /// C2 热重载收尾：默认 **保留** 已挂的 Ribbon Tab 与 CUIX，仅解绑自愈事件订阅，
        /// 让下次 C2 的 <see cref="InitializeRibbonAndMenus"/> 命中 mtime 跳过分支（≈0ms）。
        /// 仅当环境变量 <c>HYCAD_RIBBON_FORCE_TEARDOWN=1</c>（或 <c>HYCAD_CUIX_FORCE_UNLOAD=1</c>）
        /// 才执行真正的 Teardown/Unload，留给完全卸载场景使用。
        /// </summary>
        private void TerminateRibbonAndMenus()
        {
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable("HYCAD_RIBBON_FORCE_TEARDOWN"), "1", StringComparison.Ordinal))
                    HyCADTool.Shell.Ribbon.HyCadRibbonBuilder.Teardown();
                else
                    HyCADTool.Shell.Ribbon.HyCadRibbonBuilder.UnsubscribeSelfHealOnly();
            }
            catch { }

            try { HyCADTool.Shell.Ribbon.CuiMenuBuilder.UnloadOnExitOnly(); } catch { }
        }

        private void EnsureCurrentDocumentResourcesInitialized(bool force)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            string documentName = HyCADTool.Shared.AutoCAD.Utilities.DocumentKeys.GetKey(doc);
            if (!force && _initializedDocuments.Contains(documentName))
                return;

            try
            {
                TryEnsureHyCadStandardLinetypesLoaded();

                var styleService = ServiceLocator.Resolve<IStyleService>();
                var layerService = ServiceLocator.Resolve<ILayerService>();

                var vm = SettingsPanelViewModel.GetOrCreate(documentName, styleService);
                vm.LoadSettings();
                vm.EnsureLayerCatalogForDocumentInit();
                vm.EnsureStylesApplied();

                layerService.EnsureUserLayerItems(vm.LayerCatalogItems.ToList());
                _initializedDocuments.Add(documentName);
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 文档资源初始化警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 向 AutoCAD 命令行写入消息
        /// </summary>
        private void WriteMessage(string message)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage(message);
            }
            catch
            {
                // 如果没有活动文档，忽略错误
            }
        }
    }
}

