using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
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

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 插件初始化器（v2：不再是 IExtensionApplication，由 ReCall.ReCallClass.Reload 反射调用）。
    /// </summary>
    public class PluginInitializer
    {
        private static readonly HashSet<string> _initializedDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool.Refactored 插件初始化中...");
                WriteMessage("\n========================================");

                // 必装：WPF Dispatcher / Binding 异常兜底。PaletteSet 宿主默认会吞掉所有
                // UI 线程异常，导致面板控件首次实例化失败时表现为 AutoCAD 原生崩溃，无任何线索。
                InstallWpfExceptionTraps();

                // 【关键】在任何 WPF XAML/控件被触发前，同步 warmup 跨程序集主题字典 + 所有 SubView。
                // 原因：HyCAD.BlenderUI 标记了 [assembly: ThemeInfo(SourceAssembly)]，
                // WPF 首次创建其自定义控件时会在 UI tick 异步查找 Themes/Generic.xaml，
                // 此时若 pack URI 解析失败，异常会在 PresentationFramework native 层
                // 以 0xE0434352 抛出，表现为 AutoCAD "致命错误" 弹窗（无托管堆栈可捕获）。
                // 同步 warmup 把这条路径从"异步 native"变成"同步托管异常"，必崩时可见。
                WarmupBlenderTheme();
                WarmupSubViews();

                // 构建 Autofac 容器
                var builder = new ContainerBuilder();
                builder.RegisterModule<AutofacModule>();
                var container = builder.Build();

                // 注册到服务定位器
                ServiceLocator.Initialize(container);

                WriteMessage("\n✓ 依赖注入容器已初始化");

                // 加载配置
                LoadConfigurations();

                WriteMessage("\n✓ 配置已加载");

                // 一次性初始化所有样式和图层
                InitializeStylesAndLayers();

                // 注册文档事件（可选）
                RegisterDocumentEvents();

                // 道路子系统启动（P0 落地：事件总线 + JSON 持久化）
                InitializeRoadSubsystem();

                // 三入口 UI：AutoCAD Ribbon 选项卡（Ribbon 未启用时会静默跳过，不影响其他入口）
                InitializeRibbonAndMenus();

                WriteMessage("\n========================================");
                WriteMessage("\n✓ HyCADTool.Refactored 插件初始化完成！");
                WriteMessage("\n========================================\n");

                WriteEntryGuide();
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n✗ [错误] 插件初始化失败：{ex.Message}");
                WriteMessage($"\n堆栈跟踪：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 插件终止
        /// </summary>
        public void Terminate()
        {
            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool.Refactored 插件正在卸载...");

                // 保存当前面板设置
                try
                {
                    ViewModels.SettingsPanelViewModel.Current?.SaveSettings();
                }
                catch { }

                // 卸载三入口 UI：Ribbon 选项卡、CUIX 菜单（防 C2 热重载累积）
                TerminateRibbonAndMenus();

                // 道路子系统收尾：
                // v1.1 起各命令在 tr.Commit() 后已同步写盘，卸载时不再需要 FlushAll；
                // 如果用户在执行过命令后直接关闭 AutoCAD，数据已经在 .roaddesign.json 里。

                // 清理事件订阅
                UnregisterDocumentEvents();

                // 清理容器
                ServiceLocator.Reset();

                WriteMessage("\n✓ HyCADTool.Refactored 插件已卸载");
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
                var configService = ServiceLocator.Resolve<Domain.Interfaces.IConfigurationService>();
                configService.LoadAll();

                // 显示配置信息
                var globalConfig = configService.Global;
                WriteMessage($"\n  - Scale: {globalConfig.Scale.Default}");
                WriteMessage($"\n  - ElevationLength: {globalConfig.ElevationLength}");
                WriteMessage($"\n  - Tolerance: {globalConfig.Tolerance.Double}");

                var pileConfig = configService.GetModuleConfig<Domain.ValueObjects.Configuration.Modules.PileConfiguration>("Pile");
                WriteMessage($"\n  - Pile Diameter: {pileConfig.DiameterOrEdge}mm");
                WriteMessage($"\n  - Pile Section: {pileConfig.Section}");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 配置加载警告：{ex.Message}（使用默认值）");
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
                WriteMessage($"\n  ✓ 设置文件: {ViewModels.SettingsPanelViewModel.GetSettingsFilePath()}");

                // 主题兜底（v2 host-replace 模型）：
                // - LoadSettings 中 Theme setter 已经 Apply 过一次，把 BlenderThemeManager.Current
                //   设为目标值（此时 ColorsHost 还没创建，无 host 可改）；
                // - 这里调 Refresh()，强制按 Current 再刷一遍 host —— 若用户已先开过面板再触发
                //   插件重载，能立即生效；若 ColorsHost 尚未注册（典型首启），由 ColorsHost 构造时
                //   自检 Current 完成补刷，无需在此重复调 Apply（同主题幂等会 early-return）。
                try
                {
                    var themeName = ViewModels.SettingsPanelViewModel.Current?.Theme ?? "BlenderDark";
                    HyCAD.BlenderUI.Theming.BlenderThemeManager.Apply(themeName);
                    HyCAD.BlenderUI.Theming.BlenderThemeManager.Refresh();
                }
                catch { /* Application 未就绪等场景静默 */ }
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
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                ViewModels.SettingsPanelViewModel.GetOrCreate(e.Document.Name, styleService);
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
                ServiceLocator.Resolve<Infrastructure.AutoCAD.Services.Road.RoadDesignRegistry>();
                ServiceLocator.Resolve<Infrastructure.AutoCAD.Services.Road.RoadJsonExportService>();

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

        /// <summary>预热 HyPreferencesView 下所有 SubView，把跨程序集 pack URI + StaticResource 解析问题前置到 Initialize 同步阶段。</summary>
        private void WarmupSubViews()
        {
            // 顺序：HyPreferencesView 自身 → 各 sub:XxxSettingsView。任何一项失败立刻可见。
            var views = new (string Path, string Name)[]
            {
                ("/HyCADTool.Refactored;component/Presentation/Views/HyPreferencesView.xaml",            "HyPreferencesView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/StyleSettingsView.xaml",        "StyleSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/ReinSettingsView.xaml",         "ReinSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/BasePlateSettingsView.xaml",    "BasePlateSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/PileSettingsView.xaml",         "PileSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/ClusterSettingsView.xaml",      "ClusterSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/RoadSettingsView.xaml",         "RoadSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/ElevationSettingsView.xaml",    "ElevationSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/DimSettingsView.xaml",          "DimSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/AnchorBoltSettingsView.xaml",   "AnchorBoltSettingsView"),
                ("/HyCADTool.Refactored;component/Presentation/Views/Preferences/EquipFoundationSettingsView.xaml","EquipFoundationSettingsView"),
            };

            int ok = 0, fail = 0;
            foreach (var v in views)
            {
                try
                {
                    // Application.LoadComponent 第一参数必须是【相对 URI】（pack 里不能直接传绝对）
                    // 真要绝对必须先 GetContentStream + XamlReader.Load，但这里相对就够：
                    // ;component 后面那段就是 SourceAssembly 内的相对路径
                    var rel = v.Path.StartsWith("/") ? v.Path.Substring(1) : v.Path;
                    int compIdx = rel.IndexOf(";component/", StringComparison.Ordinal);
                    var relPath = compIdx >= 0 ? rel.Substring(compIdx + ";component/".Length) : rel;
                    var uri = new System.Uri(relPath, System.UriKind.Relative);
                    var obj = System.Windows.Application.LoadComponent(uri);
                    if (obj == null)
                    {
                        WriteMessage($"\n  ⚠ {v.Name} 预热返回 null");
                        fail++;
                    }
                    else
                    {
                        ok++;
                    }
                }
                catch (System.Exception ex)
                {
                    fail++;
                    WriteMessage($"\n  ✗ {v.Name} 预热失败：{ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        WriteMessage($"\n    内层：{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }

            WriteMessage($"\n  ✓ SubView 预热完成（成功 {ok} / 失败 {fail}）");
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

                // LoadComponent 会同步驱动 pack URI 解析 + BAML 反序列化，
                // 任何失败（找不到资源 / 子字典引用断链 / StaticResourceHolder 异常）
                // 都会就地抛托管异常而不是后续 UI tick 上的 native crash。
                var dict = System.Windows.Application.LoadComponent(themeUri) as System.Windows.ResourceDictionary;
                if (dict != null)
                {
                    // 挂到 Application 资源（若存在），否则仅驻留引用即可让 WPF 缓存解析结果
                    if (System.Windows.Application.Current != null)
                    {
                        System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
                    }
                    WriteMessage($"\n  ✓ BlenderUI 主题预热完成（顶层资源 {dict.Count} 条、合并字典 {dict.MergedDictionaries.Count} 层）");
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

                var winDict = System.Windows.Application.LoadComponent(winUri) as System.Windows.ResourceDictionary;
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
        private void InitializeRibbonAndMenus()
        {
            // 这两个状态供 WriteEntryGuide 末尾的提示文案使用，避免重复尝试。
            _ribbonReady = false;
            _cuixLoaded = false;

            try
            {
                Infrastructure.AutoCAD.UI.HyCadRibbonBuilder.Build();
                _ribbonReady = Autodesk.Windows.ComponentManager.Ribbon != null;
                WriteMessage(_ribbonReady
                    ? "\n  ✓ Ribbon 选项卡 HyCAD 已挂载"
                    : "\n  ⚠ Ribbon 未启用（ComponentManager.Ribbon == null），输 _RIBBON 打开后会自动重挂");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ Ribbon 构建失败（跳过，不影响 Blender 面板）：{ex.Message}");
            }

            try
            {
                _cuixLoaded = Infrastructure.AutoCAD.UI.CuiMenuBuilder.EnsureLoaded();
                WriteMessage(_cuixLoaded
                    ? "\n  ✓ CUIX 菜单栏 HyCAD 已加载"
                    : "\n  ⚠ CUIX 菜单加载未成功（详见上方日志）");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ CUIX 菜单加载失败（跳过，不影响其他入口）：{ex.Message}");
            }
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

            if (_ribbonReady)
                WriteMessage("\n│ 2) Ribbon 选项卡：顶部切到 \"HyCAD\" 选项卡（看不见？输 _RIBBON）");
            else
                WriteMessage("\n│ 2) Ribbon 选项卡：当前 Ribbon 未启用，输 _RIBBON 打开后会自动重挂");

            if (_cuixLoaded)
            {
                if (Infrastructure.AutoCAD.UI.CuiMenuBuilder.IsMenuBarHidden())
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

        /// <summary>卸载 Ribbon 选项卡 + CUIX 菜单（Terminate 时调用，防 C2 热重载累积）。</summary>
        private void TerminateRibbonAndMenus()
        {
            try { Infrastructure.AutoCAD.UI.HyCadRibbonBuilder.Teardown(); } catch { }
            try { Infrastructure.AutoCAD.UI.CuiMenuBuilder.Unload(); } catch { }
        }

        private void EnsureCurrentDocumentResourcesInitialized(bool force)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            string documentName = doc.Name ?? "default";
            if (!force && _initializedDocuments.Contains(documentName))
                return;

            try
            {
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                var layerService = ServiceLocator.Resolve<Domain.Interfaces.ILayerService>();

                var vm = ViewModels.SettingsPanelViewModel.GetOrCreate(documentName, styleService);
                vm.LoadSettings();
                vm.EnsureStylesApplied();

                layerService.CreateMultipleLayers(GetRequiredLayers());
                _initializedDocuments.Add(documentName);
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 文档资源初始化警告：{ex.Message}");
            }
        }

        private static (string layerName, short colorIndex)[] GetRequiredLayers()
        {
            return new[]
            {
                // ── 钢筋 ──
                ("01_hy_1钢筋_线钢筋", (short)1),
                ("01_hy_1钢筋_点钢筋", (short)5),
                ("01_hy_1钢筋_线钢筋_外部", (short)1),
                // ── 公共标注 ──
                ("00_hy_3公共_标注1_外", (short)3),
                ("00_hy_3公共_标注3_引线", (short)92),
                // ── 筏板附加配筋 ──
                ("00_hy_配筋轮廓", (short)1),
                ("00_hy_调整配筋轮廓", (short)3),
                ("00_hy_筏板附加配筋x_上", (short)1),
                ("00_hy_筏板附加配筋x_下", (short)1),
                ("00_hy_筏板附加配筋y_上", (short)3),
                ("00_hy_筏板附加配筋y_下", (short)3),
                ("00_hy_筏板附加配筋文字_x", (short)7),
                ("00_hy_筏板附加配筋文字_y", (short)7),
                ("00_hy_筏板附加配筋x_标注", (short)1),
                ("00_hy_筏板附加配筋Y_标注", (short)3),
                // ── 视口 ──
                ("00_hy_2公共_视口", (short)1),
                // ── 桩基 ──
                ("02_hy_1桩_主", (short)3),
                ("02_hy_3桩_地基内轮廓", (short)8),
                // ── 垫层 ──
                ("00_hy_垫层", (short)7),
                // ── 图框 ──
                ("00_hy_图框", (short)7),
                // ── 配筋文字分类 ──
                ("HY_H向钢筋", (short)7),
                ("HY_V向钢筋", (short)2),
                ("HY_手动配筋", (short)1),
                // ── 聚类分析 ──
                ("00_hy_BP", (short)3),
                ("00_hy_AAP", (short)1),
                ("00_hy_BAP", (short)4),
                ("00_hy_ABolt", (short)2),
                ("00_hy_SteelPlate", (short)5),
                ("00_hy_AxisCircle", (short)7),
                ("00_hy_AxisText", (short)7),
                ("00_hy_Region", (short)9),
                ("00_hy_RegionText", (short)9),
                ("00_hy_Dim_X", (short)7),
                ("00_hy_Dim_Y", (short)7),
                ("00_hy_ClusterEP", (short)8),
                ("00_hy_ClusterEEP", (short)8),
                ("00_hy_ClusterHull", (short)6),
                ("00_hy_ClusterPts", (short)34),
                // ── 道路（P1+）──
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.AlignmentLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.AlignmentColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.ProfileLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.ProfileColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.CorridorLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.CorridorColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.MarkingLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.MarkingColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.StationLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.StationColor),
            };
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

