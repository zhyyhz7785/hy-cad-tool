using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;

[assembly: CommandClass(typeof(HyCADTool.ReCall.ReCallClass))]
[assembly: ExtensionApplication(typeof(HyCADTool.ReCall.ReCallExtension))]

namespace HyCADTool.ReCall
{
    /// <summary>
    /// ReCall 启动钩子（v2.1）：让 AutoCAD NETLOAD ReCall.dll 后无需手动输 C2，
    /// 自动触发一次 <see cref="ReCallClass.Reload"/>，使 PluginInitializer 跑完，
    /// Ribbon / CUIX 菜单 / 命令表全部就位 —— 用户开 CAD 即见 HyCAD 入口。
    ///
    /// 为什么 ReCall 可以走 IExtensionApplication：
    /// - Refactored.dll 不能走（注释见下方 Recall.cs 顶部 v2 说明：会与 C2 反射调用形成双初始化）。
    /// - ReCall.dll 是单一入口、AutoCAD 唯一直接 NETLOAD 的程序集，
    ///   IExtensionApplication.Initialize 在这里就是"启动唯一入口"，不会重复。
    ///
    /// 时序：
    /// - AutoCAD 调 Initialize 时 MdiActiveDocument 可能还未就绪（启动套件加载阶段尤其如此）。
    /// - 通过 Application.Idle 事件延迟到第一个空闲帧再调 Reload，此时文档/命令行/Ribbon 都已可用。
    /// - Idle 处理完毕立即解绑，避免重复触发。
    ///
    /// Terminate 不做任何事：AutoCAD 关闭时 PluginInitializer.Terminate 会被现有清理路径调用。
    /// </summary>
    public sealed class ReCallExtension : Autodesk.AutoCAD.Runtime.IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                Application.Idle += OnIdleAutoReload;
            }
            catch
            {
                // Initialize 内任何异常都会被 AutoCAD 升级为致命错误，吞掉以保护启动。
            }
        }

        public void Terminate()
        {
            // 不做任何事，避免 shutdown 流程被打断。
        }

        private static void OnIdleAutoReload(object sender, EventArgs e)
        {
            try { Application.Idle -= OnIdleAutoReload; } catch { }

            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    // 没文档（极少见，例如批处理模式）就放弃自动加载，让用户手动 C2。
                    return;
                }
                doc.Editor.WriteMessage("\nReCall: 自动加载 HyCADTool.Refactored …");
                new ReCallClass().Reload();
            }
            catch (System.Exception ex)
            {
                try
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                        "\nReCall: 自动加载失败（请手动输入 C2）：" + ex.Message);
                }
                catch { }
            }
        }
    }


    /// <summary>
    /// ReCall 热重启底座（v2 架构：命令表 + 反射调度）。
    ///
    /// 职责：
    /// - <c>C2</c>：复制 Refactored.dll 到 %TEMP% → <c>Assembly.Load(byte[])</c> → 反射调用
    ///   <c>PluginInitializer.Initialize()</c>（替代首次 NETLOAD 的效果）→ 刷新命令表并校验。
    /// - <c>C1</c>：临时测试入口，调用最新 Refactored 里的 <c>TestCommand.Run()</c>。
    /// - <c>hyRecallSelfCheck</c>：输出运行时快照，用于诊断。
    /// - <c>Invoke(key)</c>：被 <see cref="CommandFacade"/> 里所有 <c>[CommandMethod]</c> 调用，
    ///   查 <see cref="CommandTable"/> 反射定位 Refactored 里的 Command 类执行。
    ///
    /// 设计原则：
    /// - AutoCAD 只 NETLOAD <b>ReCall.dll</b>（一次）。Refactored 通过 C2 进入，不再参与 AutoCAD
    ///   的 <c>[CommandMethod]</c> 扫描 —— 消除"首次 NETLOAD 才注册命令"的历史限制。
    /// - 加新命令：改 <c>commands.json</c> 并用 N1~N50 占位符即可，零关 CAD。
    /// - 改业务代码：C2 刷新 Refactored 引用即可，零关 CAD。
    /// - ReCall.cs 自身修改：需要关 CAD 重新 <c>NETLOAD ReCall.dll</c>（罕见）。
    /// </summary>
    public class ReCallClass
    {
        #region ========== 配置 ==========

        private const string TARGET_PROJECT_NAME = "HyCADTool.Refactored";
        private const string TARGET_DLL_NAME = "HyCADTool.Refactored.dll";
        private const string BUILD_CONFIGURATION = "Debug";
        /// <summary>ReCall.dll 到解决方案根的层级：Debug→bin→ReCall→根 = 3</summary>
        private const int DIRECTORY_LEVELS_UP = 3;

        /// <summary>C1 固定调用 TestCommand.Run()，要测谁请改 Refactored/Test/TestCommand.cs</summary>
        private const string TEST_ENTRY_TYPE = "HyCADTool.Refactored.Test.TestCommand";
        private const string TEST_ENTRY_METHOD = "Run";

        /// <summary>PluginInitializer 全名（C2 每次反射调用 Initialize / 旧实例 Terminate）</summary>
        private const string PLUGIN_INIT_TYPE = "HyCADTool.Refactored.Presentation.PluginInitializer";
        private const string PLUGIN_INIT_METHOD_INITIALIZE = "Initialize";
        private const string PLUGIN_INIT_METHOD_TERMINATE = "Terminate";

        /// <summary>SettingsPanelViewModel 前置钩子（每个业务命令调用前反射触发一次）</summary>
        private const string VM_TYPE = "HyCADTool.Refactored.Presentation.ViewModels.SettingsPanelViewModel";

        /// <summary>临时目录副本保留数量（按 mtime 最新优先），超过则删除多余的</summary>
        private const int TEMP_COPY_RETAIN = 15;

        /// <summary>临时目录基名（在 %TEMP% 下）</summary>
        private const string TEMP_BASE_NAME = "HyCADToolRefactored";

        /// <summary>_HyExec 特殊 key 的 type 字段标记（JSON 里写此值，Invoke 识别后走面板 Pending 路径）</summary>
        internal const string PANEL_PENDING_TYPE_MARKER = "__PANEL_PENDING__";

        #endregion

        #region ========== 静态状态 ==========

        private static Action _c1Action;
        private static bool _assemblyResolveRegistered = false;
        private static bool _unhandledExceptionRegistered = false;
        private static string _currentDependenciesPath;
        private static string _currentNugetPackagesPath;
        private static ResolveEventHandler _assemblyResolveHandler;
        private static int _c2Count = 0;
        private static DateTime _lastC2Utc = DateTime.MinValue;
        private static string _lastLoadDir;

        /// <summary>当前已加载的 Refactored 程序集（每次 C2 后刷新，Invoke 据此反射）</summary>
        private static Assembly _refactoredAssembly;

        /// <summary>上次 C2 创建的 PluginInitializer 实例（用于下次 C2 前调用其 Terminate 清理事件订阅）</summary>
        private static object _lastPluginInitInstance;

        #endregion

        #region ========== C2 ==========

        /// <summary>C2 - 重新加载 Refactored.dll，反射执行 PluginInitializer.Initialize，刷新命令表。</summary>
        [CommandMethod("C2")]
        public void Reload()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var adapterDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(adapterDir))
                    throw new InvalidOperationException("无法获取当前程序集目录。");

                string root = GetRootDirectory(new FileInfo(Path.Combine(adapterDir, "_.dummy")), DIRECTORY_LEVELS_UP);
                string pluginPath = Path.Combine(root, TARGET_PROJECT_NAME, "bin", BUILD_CONFIGURATION, TARGET_DLL_NAME);
                string depsPath = Path.Combine(root, TARGET_PROJECT_NAME, "bin", BUILD_CONFIGURATION);
                string nugetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

                if (!File.Exists(pluginPath))
                {
                    ed.WriteMessage("\n✗ 未找到: " + pluginPath);
                    ed.WriteMessage("\n  期望路径: " + pluginPath);
                    ed.WriteMessage("\n  请先在 Visual Studio 中编译 " + TARGET_PROJECT_NAME + "（配置: " + BUILD_CONFIGURATION + "）");
                    return;
                }

                try { CleanupOldTempCopies(TEMP_COPY_RETAIN); } catch { }

                var swCopy = System.Diagnostics.Stopwatch.StartNew();
                string loadPath = CopyToTempAndGetLoadPath(depsPath, pluginPath, ed);
                if (loadPath == null) return;
                string loadDepsPath = Path.GetDirectoryName(loadPath);
                swCopy.Stop();

                _currentDependenciesPath = loadDepsPath;
                _currentNugetPackagesPath = nugetPath;
                _lastLoadDir = loadDepsPath;

                if (!_unhandledExceptionRegistered)
                {
                    _unhandledExceptionRegistered = true;
                    AppDomain.CurrentDomain.UnhandledException += (s, ue) =>
                    {
                        try
                        {
                            var ex = ue.ExceptionObject as System.Exception;
                            var ued = Application.DocumentManager.MdiActiveDocument?.Editor;
                            ued?.WriteMessage($"\n[UnhandledException] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}");
                        }
                        catch { }
                    };
                }

                if (!_assemblyResolveRegistered)
                {
                    _assemblyResolveHandler = CurrentDomain_AssemblyResolve;
                    AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveHandler;
                    _assemblyResolveRegistered = true;
                }

                // 预加载 HyCAD.* / HyCADTool.* 伙伴程序集到 AppDomain。
                // 原因：Refactored XAML 含大量跨程序集 pack URI（如
                // pack://application:,,,/HyCAD.BlenderUI;component/...），
                // WPF 解析时不会触发 AssemblyResolve，只会遍历 AppDomain.GetAssemblies()
                // 按 short name 查找，找不到则读空 baml 流 → PresentationFramework native 崩溃
                // → AutoCAD 原生崩溃（无托管异常）。必须在 Load Refactored 前就位。
                PreloadCompanionAssemblies(loadDepsPath, ed);

                var swLoad = System.Diagnostics.Stopwatch.StartNew();
                Assembly asm = Assembly.Load(File.ReadAllBytes(loadPath));
                _refactoredAssembly = asm;
                swLoad.Stop();

                ResourceManager.ResourceAssembly = asm;

                // 先终止旧实例（解绑文档事件、Reset ServiceLocator、保存面板设置）
                var swTerm = System.Diagnostics.Stopwatch.StartNew();
                InvokeLegacyTerminate(ed);
                swTerm.Stop();

                // 反射执行新实例的 Initialize —— 这等价于"首次 NETLOAD 后 AutoCAD 调 IExtensionApplication.Initialize"。
                // Autofac 容器、图层、道路子系统、文档事件订阅全在这里完成。
                var swInit = System.Diagnostics.Stopwatch.StartNew();
                InvokePluginInitialize(asm, ed);
                swInit.Stop();

                // 绑定 C1 → Refactored.TestCommand.Run
                _c1Action = CreateStaticMethodDelegate(asm, TEST_ENTRY_TYPE, TEST_ENTRY_METHOD, ed);

                // 刷新命令表并校验
                var swTbl = System.Diagnostics.Stopwatch.StartNew();
                CommandTable.ForceReload();
                var validation = CommandTable.ValidateAll(asm);
                swTbl.Stop();

                sw.Stop();
                _c2Count++;
                _lastC2Utc = DateTime.UtcNow;

                ed.WriteMessage($"\nC2 #{_c2Count} 完成 {sw.ElapsedMilliseconds}ms "
                    + $"(复制{swCopy.ElapsedMilliseconds} + 加载{swLoad.ElapsedMilliseconds} + Terminate{swTerm.ElapsedMilliseconds} + Initialize{swInit.ElapsedMilliseconds} + 表{swTbl.ElapsedMilliseconds})");

                int failed = validation.Count(v => v.Entry != null && !(v.TypeFound && v.MethodFound) && v.Note != "<特殊：面板待执行命令>");
                int placeholders = validation.Count(v => v.Entry == null);
                int configured = validation.Count - placeholders;
                ed.WriteMessage($"\n  命令表：{configured} 条已配置 + {placeholders} 条占位符，解析失败 {failed} 条");
                if (failed > 0)
                {
                    ed.WriteMessage("\n  失败明细（建议编辑 " + CommandTable.GetFilePath() + " 修正 type/method）：");
                    foreach (var v in validation.Where(x => x.Entry != null && !(x.TypeFound && x.MethodFound) && x.Note != "<特殊：面板待执行命令>").Take(10))
                    {
                        ed.WriteMessage("\n    - " + v.Key + "  →  " + v.Note);
                    }
                }

                if (_c2Count == 1)
                {
                    ed.WriteMessage("\n提示: 新命令只需改 commands.json 并用 N1~N50 占位符；改 CommandFacade.cs 才需关 CAD 重 NETLOAD ReCall。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 加载失败: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    ed.WriteMessage("\n  Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return ResolveAssembly(args, _currentDependenciesPath, _currentNugetPackagesPath);
        }

        /// <summary>
        /// 把副本目录下的 HyCAD.* 伙伴程序集（除主 Refactored.dll 外）预加载进 AppDomain。
        /// 幂等：已在 AppDomain 中的 short name 会跳过（Assembly 不能卸载，二次 C2 无需重载伙伴）。
        /// </summary>
        private static void PreloadCompanionAssemblies(string loadDepsPath, Editor ed)
        {
            if (string.IsNullOrEmpty(loadDepsPath) || !Directory.Exists(loadDepsPath))
                return;

            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(loadDepsPath, "HyCAD*.dll");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n  ⚠ 预加载枚举伙伴程序集失败: " + ex.Message);
                return;
            }

            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { loaded.Add(a.GetName().Name); } catch { }
            }

            int newly = 0, skipped = 0;
            foreach (var path in candidates)
            {
                var fileName = Path.GetFileName(path);
                if (string.Equals(fileName, TARGET_DLL_NAME, StringComparison.OrdinalIgnoreCase))
                    continue;

                var shortName = Path.GetFileNameWithoutExtension(path);
                if (loaded.Contains(shortName))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    Assembly.Load(File.ReadAllBytes(path));
                    newly++;
                }
                catch (System.Exception ex)
                {
                    ed?.WriteMessage("\n  ⚠ 预加载 " + shortName + " 失败: " + ex.Message);
                }
            }

            if (newly > 0 || skipped > 0)
                ed?.WriteMessage("\n  预加载伙伴: +" + newly + " / 已存在 " + skipped);
        }

        /// <summary>反射调用上次 PluginInitializer 实例的 Terminate，幂等性兜底（解绑文档事件、Reset 容器）。</summary>
        private static void InvokeLegacyTerminate(Editor ed)
        {
            var legacy = _lastPluginInitInstance;
            if (legacy == null) return;
            try
            {
                var m = legacy.GetType().GetMethod(PLUGIN_INIT_METHOD_TERMINATE, BindingFlags.Public | BindingFlags.Instance);
                m?.Invoke(legacy, null);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n  ⚠ 旧 PluginInitializer.Terminate 异常（不影响继续）：" + ex.Message);
            }
            finally
            {
                _lastPluginInitInstance = null;
            }
        }

        /// <summary>反射创建 PluginInitializer 并调用 Initialize（每次 C2 一个新实例，由新 Refactored.dll 提供）。</summary>
        private static void InvokePluginInitialize(Assembly refactored, Editor ed)
        {
            try
            {
                var t = refactored.GetType(PLUGIN_INIT_TYPE);
                if (t == null)
                {
                    ed?.WriteMessage("\n  ⚠ 未找到类型 " + PLUGIN_INIT_TYPE + "（Autofac 容器未初始化，业务命令可能失败）");
                    return;
                }
                var instance = Activator.CreateInstance(t);
                var m = t.GetMethod(PLUGIN_INIT_METHOD_INITIALIZE, BindingFlags.Public | BindingFlags.Instance);
                m?.Invoke(instance, null);
                _lastPluginInitInstance = instance;
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed?.WriteMessage("\n  ⚠ PluginInitializer.Initialize 异常: " + inner.GetType().Name + ": " + inner.Message);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n  ⚠ PluginInitializer 反射失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 三连段路幅（独立）→ <c>commands.json</c> 键 <c>hySeg3</c>。
        /// 注册在 <see cref="ReCallClass"/>：与 <c>C2</c> 同属 NETLOAD 的 ReCall.dll，重装本 DLL 后即识别；
        /// 勿在 <see cref="CommandFacade"/> 再挂同名 <c>CommandMethod</c>，以免重复定义。
        /// </summary>
        [CommandMethod("HYSEG3")]
        [CommandMethod("hySeg3")]
        public void HySeg3() => Invoke("hySeg3");

        #endregion

        #region ========== Invoke（命令表调度） ==========

        /// <summary>
        /// 被 <see cref="CommandFacade"/> 与 <see cref="ReCallClass"/> 上各 <c>[CommandMethod]</c> 转发的统一入口。
        /// 1) 确保 Refactored 已加载（未加载提示 C2）
        /// 2) 处理 <c>_HyExec</c> 特殊分支（面板按钮待执行命令）
        /// 3) 查命令表获得 <see cref="CommandEntry"/>
        /// 4) 反射前置钩子（SettingsPanelViewModel 三件套）
        /// 5) 反射创建实例（支持带构造参数 + 枚举）并调用目标方法
        /// </summary>
        public static void Invoke(string key)
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                var refactored = _refactoredAssembly ?? GetLoadedAssembly(TARGET_PROJECT_NAME);
                if (refactored == null)
                {
                    ed.WriteMessage("\n✗ Refactored 尚未加载，请先执行 C2。");
                    return;
                }

                // _HyExec 特殊分支：直接从面板 VM 取待执行 Action 并调用
                if (string.Equals(key, "_HyExec", StringComparison.Ordinal))
                {
                    InvokePanelPendingCommand(refactored, ed);
                    return;
                }

                if (!CommandTable.Contains(key))
                {
                    ed.WriteMessage("\n✗ 命令表中未定义键 \"" + key + "\"。请检查 commands.json（" + CommandTable.GetFilePath() + "）。");
                    return;
                }

                var entry = CommandTable.Get(key);
                if (entry == null)
                {
                    ed.WriteMessage("\n✗ 占位符 \"" + key + "\" 尚未分配。请在 " + CommandTable.GetFilePath() + " 中把它指向实际的 type/method。");
                    return;
                }

                InvokePreHooks(refactored);

                var targetType = refactored.GetType(entry.Type);
                if (targetType == null)
                {
                    ed.WriteMessage("\n✗ 类型未找到: " + entry.Type);
                    return;
                }

                object instance = null;
                if (!(targetType.IsAbstract && targetType.IsSealed))
                {
                    var ctorArgs = ResolveCtorArgs(entry.Ctor, entry.CtorEnumTypes, targetType, refactored);
                    instance = Activator.CreateInstance(targetType, ctorArgs);
                }

                var method = targetType.GetMethod(entry.Method,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (method == null)
                {
                    ed.WriteMessage("\n✗ 方法未找到: " + entry.Type + "." + entry.Method);
                    return;
                }

                method.Invoke(instance, null);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed.WriteMessage("\n✗ 命令执行失败 [" + key + "]: " + inner.GetType().Name + ": " + inner.Message);
                if (inner.StackTrace != null)
                    ed.WriteMessage("\n  " + inner.StackTrace.Split('\n').FirstOrDefault());
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 命令调度失败 [" + key + "]: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>前置钩子：CommitFocusedTextBoxValue + LoadSettings + EnsureStylesApplied。等价于 Refactored 原 Run(action) 的开头。</summary>
        private static void InvokePreHooks(Assembly refactored)
        {
            try
            {
                var vmType = refactored.GetType(VM_TYPE);
                if (vmType == null) return;

                vmType.GetMethod("CommitFocusedTextBoxValue", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

                var currentProp = vmType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var current = currentProp?.GetValue(null);
                if (current == null) return;

                vmType.GetMethod("LoadSettings", BindingFlags.Public | BindingFlags.Instance)?.Invoke(current, null);
                vmType.GetMethod("EnsureStylesApplied", BindingFlags.Public | BindingFlags.Instance)?.Invoke(current, null);
            }
            catch
            {
                /* 钩子失败不应阻塞命令执行，最坏情况下面板最新参数没同步 */
            }
        }

        /// <summary>_HyExec：面板按钮通过 SendStringToExecute 触发此命令，反射取出 ConsumePendingCommand 返回的 Action 并执行。</summary>
        private static void InvokePanelPendingCommand(Assembly refactored, Editor ed)
        {
            try
            {
                var vmType = refactored.GetType(VM_TYPE);
                if (vmType == null)
                {
                    ed?.WriteMessage("\n✗ 未找到 " + VM_TYPE);
                    return;
                }
                var consume = vmType.GetMethod("ConsumePendingCommand", BindingFlags.Public | BindingFlags.Static);
                var action = consume?.Invoke(null, null) as Delegate;
                action?.DynamicInvoke();
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed?.WriteMessage("\n✗ 面板命令失败: " + inner.GetType().Name + ": " + inner.Message);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n✗ 面板命令失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 把 JSON 里的 ctor 数组（Newtonsoft 反序列化后元素类型为 long/double/bool/string）按目标构造函数的参数类型转换；
        /// 若 ctorEnumTypes[i] 非空，则把 string 值解析为指定枚举类型的成员。
        /// </summary>
        private static object[] ResolveCtorArgs(object[] ctor, string[] ctorEnumTypes, Type targetType, Assembly refactored)
        {
            if (ctor == null || ctor.Length == 0) return null;

            // 先按 ctorEnumTypes 把字符串转为枚举
            var args = new object[ctor.Length];
            for (int i = 0; i < ctor.Length; i++)
            {
                object raw = ctor[i];
                string enumTypeName = (ctorEnumTypes != null && i < ctorEnumTypes.Length) ? ctorEnumTypes[i] : null;
                if (!string.IsNullOrEmpty(enumTypeName) && raw is string enumValueName)
                {
                    var enumType = refactored.GetType(enumTypeName) ?? Type.GetType(enumTypeName);
                    if (enumType != null)
                    {
                        args[i] = Enum.Parse(enumType, enumValueName, ignoreCase: true);
                        continue;
                    }
                }
                args[i] = raw;
            }

            // 再按实际构造函数的参数类型二次 cast（long→int、double→float、JValue→enum 等）
            var ctors = targetType.GetConstructors();
            var best = ctors.FirstOrDefault(c => c.GetParameters().Length == args.Length);
            if (best != null)
            {
                var ps = best.GetParameters();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null) continue;
                    var expected = ps[i].ParameterType;
                    if (!expected.IsInstanceOfType(args[i]))
                    {
                        try
                        {
                            args[i] = Convert.ChangeType(args[i], expected);
                        }
                        catch
                        {
                            /* 保留原值让后续 CreateInstance 抛出清晰异常 */
                        }
                    }
                }
            }
            return args;
        }

        #endregion

        #region ========== C1 ==========

        /// <summary>C1 - 调用最近一次 C2 绑定的 TestCommand.Run 委托（临时测试入口，日常业务请用命令表）。</summary>
        [CommandMethod("C1")]
        public void RunTest()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            if (_c1Action == null)
            {
                ed.WriteMessage("\n✗ 请先执行 C2 加载插件");
                return;
            }

            try
            {
                _c1Action.Invoke();
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                ed.WriteMessage($"\n✗ 执行失败(TIE): {inner.GetType().Name}: {inner.Message}");
                ed.WriteMessage($"\n  StackTrace: {inner.StackTrace}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n✗ 执行失败: {ex.GetType().Name}: {ex.Message}");
                ed.WriteMessage($"\n  StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    ed.WriteMessage($"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }

        #endregion

        #region ========== hyRecallSelfCheck ==========

        /// <summary>ReCall 运行时状态诊断快照。用于定位"C2 不生效 / 命令未找到 / 类型解析失败"等问题。</summary>
        [CommandMethod("hyRecallSelfCheck")]
        public void SelfCheck()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                var lines = new List<string>();
                lines.Add("================ ReCall 自检 ================");

                var self = Assembly.GetExecutingAssembly();
                var selfName = self.GetName();
                lines.Add($"ReCall.dll                = {selfName.Name} v{selfName.Version}");
                lines.Add($"ReCall.Location           = {self.Location}");
                lines.Add($"C2 已执行次数             = {_c2Count}");
                lines.Add($"最近 C2 时间 (UTC)        = {(_lastC2Utc == DateTime.MinValue ? "<未执行>" : _lastC2Utc.ToString("yyyy-MM-dd HH:mm:ss"))}");
                lines.Add($"C1 _c1Action 已绑定       = {(_c1Action != null ? "是" : "否（请先 C2）")}");
                lines.Add($"_refactoredAssembly       = {(_refactoredAssembly == null ? "<未加载>" : "已缓存")}");
                lines.Add($"_lastPluginInitInstance   = {(_lastPluginInitInstance == null ? "<无>" : _lastPluginInitInstance.GetType().FullName)}");
                lines.Add($"AssemblyResolve 已注册    = {_assemblyResolveRegistered}");
                lines.Add($"UnhandledException 已注册 = {_unhandledExceptionRegistered}");
                lines.Add($"当前依赖目录              = {_currentDependenciesPath ?? "<空>"}");
                lines.Add($"当前 NuGet 目录           = {_currentNugetPackagesPath ?? "<空>"}");

                lines.Add("");
                lines.Add("-------- Refactored 主程序集 --------");
                var refactored = _refactoredAssembly ?? GetLoadedAssembly(TARGET_PROJECT_NAME);
                if (refactored == null)
                {
                    lines.Add($"{TARGET_PROJECT_NAME}: <未加载>（请先执行 C2）");
                }
                else
                {
                    var rn = refactored.GetName();
                    string loc;
                    try { loc = refactored.IsDynamic ? "<dynamic>" : (refactored.Location ?? string.Empty); }
                    catch { loc = string.Empty; }
                    var isHot = string.IsNullOrEmpty(loc);
                    lines.Add($"{TARGET_PROJECT_NAME} v{rn.Version}");
                    lines.Add($"  Location = {(isHot ? "<空 → 已热重载 / Load(byte[])>" : loc)}");
                    lines.Add($"  IsDynamic={refactored.IsDynamic}, ReflectionOnly={refactored.ReflectionOnly}");
                }

                lines.Add("");
                lines.Add("-------- ServiceLocator --------");
                lines.Add(ProbeServiceLocator(refactored));

                lines.Add("");
                lines.Add("-------- 临时副本目录 --------");
                lines.Add(ProbeTempCopies());

                lines.Add("");
                lines.Add("-------- 关联程序集 (AppDomain 快照) --------");
                lines.AddRange(ListRelatedAssemblies());

                lines.Add("");
                lines.Add("-------- 命令表 (commands.json) --------");
                lines.Add(ProbeCommandTableHeader());

                lines.Add("");
                lines.Add("-------- 命令校验结果 --------");
                lines.AddRange(ProbeCommandTableValidation(refactored));

                lines.Add("");
                lines.Add("-------- 占位符状态 (N1~N50) --------");
                lines.AddRange(ProbePlaceholders());

                lines.Add("==============================================");

                foreach (var line in lines)
                    ed.WriteMessage("\n" + line);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 自检过程异常: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string ProbeCommandTableHeader()
        {
            try
            {
                var p = CommandTable.GetFilePath();
                var mtime = CommandTable.GetFileMtimeUtc();
                var loaded = CommandTable.LastLoadUtc;
                var snap = CommandTable.Snapshot();
                var configured = snap.Count(kv => kv.Value != null);
                var placeholder = snap.Count(kv => kv.Value == null);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"文件路径 = {p}");
                sb.AppendLine($"文件 mtime (UTC) = {(mtime.HasValue ? mtime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "<不存在>")}");
                sb.AppendLine($"上次加载 (UTC)   = {(loaded == DateTime.MinValue ? "<未加载>" : loaded.ToString("yyyy-MM-dd HH:mm:ss"))}");
                sb.AppendLine($"条目统计         = {configured} 已配置 + {placeholder} 占位符 = 共 {snap.Count}");
                if (!string.IsNullOrEmpty(CommandTable.LastError))
                    sb.AppendLine($"最近错误         = {CommandTable.LastError}");
                return sb.ToString().TrimEnd();
            }
            catch (System.Exception ex)
            {
                return "命令表探针异常: " + ex.Message;
            }
        }

        private static IEnumerable<string> ProbeCommandTableValidation(Assembly refactored)
        {
            var list = new List<string>();
            try
            {
                var results = CommandTable.ValidateAll(refactored);
                int ok = results.Count(r => r.Entry != null && r.TypeFound && r.MethodFound
                                         && r.Note != "<特殊：面板待执行命令>");
                int special = results.Count(r => r.Note == "<特殊：面板待执行命令>");
                int placeholder = results.Count(r => r.Entry == null);
                int fail = results.Count - ok - special - placeholder;
                list.Add($"OK={ok}  Special={special}  Placeholder={placeholder}  Fail={fail}");
                if (fail <= 0)
                {
                    list.Add("  ✓ 全部已配置命令都能解析到 Refactored 里的 Type.Method");
                }
                else
                {
                    list.Add("  ✗ 以下命令解析失败（请编辑 commands.json）：");
                    foreach (var r in results.Where(x => x.Entry != null && !(x.TypeFound && x.MethodFound) && x.Note != "<特殊：面板待执行命令>"))
                    {
                        list.Add("    " + r.Key.PadRight(28) + " " + r.Note);
                    }
                }
            }
            catch (System.Exception ex)
            {
                list.Add("命令校验异常: " + ex.Message);
            }
            return list;
        }

        private static IEnumerable<string> ProbePlaceholders()
        {
            var list = new List<string>();
            try
            {
                var snap = CommandTable.Snapshot();
                var placeholders = snap.Where(kv => kv.Key.StartsWith("N", StringComparison.Ordinal)
                                                 && int.TryParse(kv.Key.Substring(1), out _))
                                       .OrderBy(kv => int.TryParse(kv.Key.Substring(1), out var n) ? n : 0)
                                       .ToList();
                var used = placeholders.Where(kv => kv.Value != null).ToList();
                var free = placeholders.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();

                list.Add($"占位符总数 = {placeholders.Count}，已占用 {used.Count}，空闲 {free.Count}");
                if (used.Count > 0)
                {
                    list.Add("  已占用：");
                    foreach (var u in used)
                    {
                        list.Add("    " + u.Key.PadRight(5) + " → " + u.Value.Type + "." + u.Value.Method);
                    }
                }
                if (free.Count > 0)
                {
                    list.Add("  空闲 (" + free.Count + " 个)：" + string.Join(" ", free));
                }
            }
            catch (System.Exception ex)
            {
                list.Add("占位符探针异常: " + ex.Message);
            }
            return list;
        }

        private static string ProbeServiceLocator(Assembly refactored)
        {
            if (refactored == null) return "ServiceLocator: <Refactored 未加载>";
            try
            {
                var slType = refactored.GetType("HyCADTool.Refactored.Infrastructure.Configuration.ServiceLocator");
                if (slType == null) return "ServiceLocator: <类型未找到>";
                var containerProp = slType.GetProperty("Container", BindingFlags.Public | BindingFlags.Static);
                object container = null;
                try { container = containerProp?.GetValue(null); } catch { /* 未初始化会抛 */ }
                return container == null
                    ? "ServiceLocator.Container: <未初始化>"
                    : "ServiceLocator.Container: " + container.GetType().FullName + " (已初始化)";
            }
            catch (System.Exception ex)
            {
                return "ServiceLocator 探针异常: " + ex.Message;
            }
        }

        private static string ProbeTempCopies()
        {
            try
            {
                string tempBase = Path.Combine(Path.GetTempPath(), TEMP_BASE_NAME);
                if (!Directory.Exists(tempBase))
                    return "<空>（%TEMP%\\" + TEMP_BASE_NAME + " 尚未创建）";

                var dirs = new DirectoryInfo(tempBase).GetDirectories()
                    .OrderByDescending(d => d.LastWriteTimeUtc).ToList();

                long totalBytes = 0;
                foreach (var d in dirs)
                {
                    try { totalBytes += DirectorySize(d); } catch { }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"副本数 = {dirs.Count}  (保留阈值 {TEMP_COPY_RETAIN})");
                sb.AppendLine($"总占用 = {FormatBytes(totalBytes)}");
                sb.AppendLine($"基目录 = {tempBase}");
                for (int i = 0; i < Math.Min(3, dirs.Count); i++)
                    sb.AppendLine($"  [{i}] {dirs[i].LastWriteTimeUtc:HH:mm:ss} {dirs[i].Name}");
                return sb.ToString().TrimEnd();
            }
            catch (System.Exception ex)
            {
                return "临时副本探针异常: " + ex.Message;
            }
        }

        private static long DirectorySize(DirectoryInfo d)
        {
            long size = 0;
            foreach (var f in d.GetFiles()) size += f.Length;
            foreach (var sub in d.GetDirectories()) size += DirectorySize(sub);
            return size;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
        }

        private static IEnumerable<string> ListRelatedAssemblies()
        {
            var patterns = new[] { "HyCAD", "Refactored", "Markdown", "Autofac", "Newtonsoft", "WebView2", "ReCall" };
            var list = new List<string>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name;
                try { name = a.GetName().Name ?? ""; }
                catch { continue; }
                if (!patterns.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                var ver = a.GetName().Version?.ToString() ?? "?";
                string loc;
                try { loc = a.IsDynamic ? "<dynamic>" : (a.Location ?? ""); }
                catch { loc = "<n/a>"; }
                string tag;
                if (loc == "<dynamic>") tag = "[Dynamic]";
                else if (loc == "<n/a>" || string.IsNullOrEmpty(loc)) tag = "[Load(byte[])]";
                else tag = "[静态/LoadFrom]";
                list.Add($"  {tag,-18} {name,-40} v{ver}");
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            if (list.Count == 0) list.Add("  <无匹配程序集>");
            return list;
        }

        #endregion

        #region ========== 辅助：加载副本、依赖解析 ==========

        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
        {
            var dir = fileInfo.Directory;
            for (int i = 0; i < levelsUp && dir != null; i++)
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("无法解析根目录。");
        }

        /// <summary>复制 bin\Debug 到新的临时子目录并返回副本中主 DLL 的路径。每次用新目录避免覆盖已加载的副本。</summary>
        private static string CopyToTempAndGetLoadPath(string sourceDir, string mainDllPath, Editor ed)
        {
            string tempBase = Path.Combine(Path.GetTempPath(), TEMP_BASE_NAME);
            string tempDir = Path.Combine(tempBase, DateTime.UtcNow.Ticks.ToString());
            try
            {
                CopyDirectoryRecursive(sourceDir, tempDir);
                string loadPath = Path.Combine(tempDir, Path.GetFileName(mainDllPath));
                return File.Exists(loadPath) ? loadPath : null;
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n✗ 复制到临时目录失败: " + ex.Message);
                return null;
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                try { File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true); }
                catch (System.Exception) { }
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                try { CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir))); }
                catch (System.Exception) { }
            }
        }

        /// <summary>清理 %TEMP%\HyCADToolRefactored\ 下的旧副本目录，按 mtime 降序保留最新 retain 个。</summary>
        private static void CleanupOldTempCopies(int retain)
        {
            if (retain < 0) retain = 0;
            string tempBase = Path.Combine(Path.GetTempPath(), TEMP_BASE_NAME);
            if (!Directory.Exists(tempBase)) return;

            var dirs = new DirectoryInfo(tempBase)
                .GetDirectories()
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .ToList();

            if (dirs.Count <= retain) return;

            foreach (var old in dirs.Skip(retain))
            {
                try { old.Delete(recursive: true); }
                catch { /* 副本可能被旧 Assembly 的 Resolve handler 引用，跳过下次再试 */ }
            }
        }

        private static Assembly GetLoadedAssembly(string shortName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (a.GetName().Name == shortName)
                        return a;
                }
                catch { }
            }
            return null;
        }

        /// <summary>给 C1 用：反射封装 Refactored.TestCommand.Run 为 Action。</summary>
        private static Action CreateStaticMethodDelegate(Assembly asm, string typeName, string methodName, Editor ed)
        {
            var type = asm?.GetType(typeName);
            if (type == null)
            {
                var alt = GetLoadedAssembly(TARGET_PROJECT_NAME);
                if (alt != null && alt != asm)
                    type = alt.GetType(typeName);
            }
            if (type == null)
            {
                ed?.WriteMessage("\n⚠ 未找到类型: " + typeName + "（请重新生成 HyCADTool.Refactored）");
                return null;
            }
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                ed?.WriteMessage("\n⚠ 未找到方法: " + typeName + "." + methodName);
                return null;
            }
            return () => method.Invoke(null, null);
        }

        // 【AutoCAD 宿主程序集黑名单 · 2026-04-21】
        //   AutoCAD 进程已加载的宿主程序集（AdWindows / AcMr / AcCoreMgd / AcDbMgd / AcMgd / AcCui ...）
        //   绝对禁止 ReCall 从临时目录 byte[] 加载——
        //   原因：AutoCAD.NET 24.3.0 NuGet 包传递引用的 AdWindows 是 5.0.1.2（旧版本），
        //   而 AutoCAD 2025 进程实际加载 5.1.1.1。Refactored 程序集 manifest 严格要 5.0.1.2
        //   → CLR 触发 AssemblyResolve → 若 ReCall 从 deps 目录返回 5.0.1.2 byte[]，
        //   AppDomain 立刻多出一份 AdWindows，类型身份割裂（5.0.1.2 的 Badge ≠ 5.1.1.1 的 Badge），
        //   AutoCAD UI 渲染 PanelListView/Ribbon 时 Badge.InitializeComponent 抛
        //   "组件 Badge 不具有由 URI/AdWindows;component/themes/badge.xaml 识别的资源"。
        //   修复：宿主程序集请求一律走 AppDomain 已加载查表（短名匹配，忽略版本），返回 AutoCAD 那份"真"5.1.1.1。
        //   详见 doc/RoadDesign/00.md。
        private static readonly string[] AutoCadHostAssemblyNames =
        {
            "AdWindows",
            "AcMr",
            "AcCoreMgd",
            "AcDbMgd",
            "AcMgd",
            "AcCui",
            "AcWindows",
            "Autodesk.AutoCAD.Interop",
            "Autodesk.AutoCAD.Interop.Common",
            "PresentationCore",
            "PresentationFramework",
            "WindowsBase",
            "System.Xaml",
        };

        private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
        {
            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            string shortName = new AssemblyName(args.Name).Name;

            // 宿主程序集：用 AppDomain 已加载的版本（短名匹配），杜绝双载入。
            if (AutoCadHostAssemblyNames.Contains(shortName, StringComparer.OrdinalIgnoreCase))
            {
                Assembly hostExisting = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, shortName, StringComparison.OrdinalIgnoreCase));
                return hostExisting; // 返回 null 让 CLR fallback；返回非 null 则 CLR 接受为版本替代
            }

            if (string.IsNullOrWhiteSpace(dependenciesPath))
                return null;

            string name = shortName + ".dll";
            string path = Path.Combine(dependenciesPath, name);
            if (File.Exists(path))
            {
                try
                {
                    return Assembly.Load(File.ReadAllBytes(path));
                }
                catch
                {
                    return null;
                }
            }

            string net8Path = Path.Combine(dependenciesPath, "net8", name);
            if (File.Exists(net8Path))
            {
                try { return Assembly.Load(File.ReadAllBytes(net8Path)); }
                catch { return null; }
            }

            try
            {
                var dirs = Directory.GetDirectories(nugetPackagesPath, name.Replace(".dll", ""), SearchOption.AllDirectories);
                foreach (var dir in dirs)
                {
                    path = Path.Combine(dir, name);
                    if (File.Exists(path))
                    {
                        try
                        {
                            return Assembly.Load(File.ReadAllBytes(path));
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion
    }

    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}
