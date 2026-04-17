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

namespace HyCADTool.ReCall
{
    /// <summary>
    /// 热重启底座：C2 重载目标插件，C1 执行 TestCommand.Run()，hyRecallSelfCheck 输出诊断快照。
    /// ReCall 本身不参与热重载 —— 改 ReCall.cs 必须关 AutoCAD 重新 NETLOAD。
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

        /// <summary>临时目录副本保留数量（按 mtime 最新优先），超过则删除多余的</summary>
        private const int TEMP_COPY_RETAIN = 15;

        /// <summary>临时目录基名（在 %TEMP% 下）</summary>
        private const string TEMP_BASE_NAME = "HyCADToolRefactored";

        #endregion

        private static Action _c1Action;
        private static bool _assemblyResolveRegistered = false;
        private static bool _unhandledExceptionRegistered = false;
        private static string _currentDependenciesPath;
        private static string _currentNugetPackagesPath;
        private static ResolveEventHandler _assemblyResolveHandler;
        private static int _c2Count = 0;
        private static DateTime _lastC2Utc = DateTime.MinValue;
        private static string _lastLoadDir;

        /// <summary>C2 - 重新加载插件（仅在此命令执行时加载，不在构造函数中调用，避免加载时执行两次）</summary>
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

                // 先清理历史副本，防止磁盘无限增长
                try { CleanupOldTempCopies(TEMP_COPY_RETAIN); } catch { }

                // 复制到新临时目录再加载（每次新目录 → 不锁原始 bin，不覆盖旧副本）
                var swCopy = System.Diagnostics.Stopwatch.StartNew();
                string loadPath = CopyToTempAndGetLoadPath(depsPath, pluginPath, ed);
                if (loadPath == null) return;
                string loadDepsPath = Path.GetDirectoryName(loadPath);
                swCopy.Stop();

                _currentDependenciesPath = loadDepsPath;
                _currentNugetPackagesPath = nugetPath;
                _lastLoadDir = loadDepsPath;

                // 注册全局未处理异常捕获（防止 WPF 线程异常导致致命错误）
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

                // 只注册一次 AssemblyResolve 事件
                if (!_assemblyResolveRegistered)
                {
                    _assemblyResolveHandler = CurrentDomain_AssemblyResolve;
                    AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveHandler;
                    _assemblyResolveRegistered = true;
                }

                // 加载程序集
                var swLoad = System.Diagnostics.Stopwatch.StartNew();
                Assembly asm = Assembly.Load(File.ReadAllBytes(loadPath));
                swLoad.Stop();

                // 初始化 DI 容器
                var swDi = System.Diagnostics.Stopwatch.StartNew();
                ResourceManager.ResourceAssembly = asm;
                InitializeServiceLocator(asm, ed);
                _c1Action = CreateStaticMethodDelegate(asm, TEST_ENTRY_TYPE, TEST_ENTRY_METHOD, ed);
                swDi.Stop();

                sw.Stop();
                _c2Count++;
                _lastC2Utc = DateTime.UtcNow;

                if (_c1Action != null)
                {
                    ed.WriteMessage($"\nC2 #{_c2Count} 完成 {sw.ElapsedMilliseconds}ms (复制{swCopy.ElapsedMilliseconds} + 加载{swLoad.ElapsedMilliseconds} + DI{swDi.ElapsedMilliseconds})");
                    if (_c2Count == 1)
                        ed.WriteMessage("\n提示: 新增 [CommandMethod] 命令首次 NETLOAD 后才进命令表；C2 不扫描，请用 C1 → 输入命令 key 运行（或参见 Doc/Recall-陷阱与问题.md）。");
                }
                else
                {
                    ed.WriteMessage("\n请重新生成后再执行 C2。");
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
        /// hyRecallSelfCheck - ReCall 运行时状态诊断快照。
        /// 用于定位"C2 不起作用 / C1 报错 / 依赖加载异常"等问题；输出可复制到 issue。
        ///
        /// 关键：此命令注册在 ReCall.dll，只有 ReCall 被 NETLOAD 就能用，不依赖 C2 成功。
        /// </summary>
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
                lines.Add($"AssemblyResolve 已注册    = {_assemblyResolveRegistered}");
                lines.Add($"UnhandledException 已注册 = {_unhandledExceptionRegistered}");
                lines.Add($"当前依赖目录              = {_currentDependenciesPath ?? "<空>"}");
                lines.Add($"当前 NuGet 目录           = {_currentNugetPackagesPath ?? "<空>"}");

                // Refactored 主 DLL 状态
                lines.Add("");
                lines.Add("-------- Refactored 主程序集 --------");
                var refactored = GetLoadedAssembly(TARGET_PROJECT_NAME);
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

                // ServiceLocator 探针
                lines.Add("");
                lines.Add("-------- ServiceLocator --------");
                lines.Add(ProbeServiceLocator(refactored));

                // Temp 副本统计
                lines.Add("");
                lines.Add("-------- 临时副本目录 --------");
                lines.Add(ProbeTempCopies());

                // AppDomain 内 HyCAD* / Markdown* / Autofac / Newtonsoft 清单
                lines.Add("");
                lines.Add("-------- 关联程序集 (AppDomain 快照) --------");
                lines.AddRange(ListRelatedAssemblies());

                // CommandRelayStore 状态
                lines.Add("");
                lines.Add("-------- CommandRelayStore --------");
                lines.Add(ProbeCommandRelay());

                lines.Add("==============================================");

                foreach (var line in lines)
                    ed.WriteMessage("\n" + line);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 自检过程异常: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string ProbeServiceLocator(Assembly refactored)
        {
            if (refactored == null) return "ServiceLocator: <Refactored 未加载>";
            try
            {
                var slType = refactored.GetType("HyCADTool.Refactored.Infrastructure.Configuration.ServiceLocator");
                if (slType == null) return "ServiceLocator: <类型未找到>";
                var containerProp = slType.GetProperty("Container", BindingFlags.Public | BindingFlags.Static);
                var container = containerProp?.GetValue(null);
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

        private static string ProbeCommandRelay()
        {
            try
            {
                string relay = Path.Combine(Path.GetTempPath(), "HyCADTool.Refactored.command-relay.json");
                if (!File.Exists(relay))
                    return "无 pending key（正常：relay 文件会被 C1 消费后立即删除）";
                var json = File.ReadAllText(relay);
                var mtime = File.GetLastWriteTime(relay);
                return $"存在 pending key: {relay}  (写入时间 {mtime:HH:mm:ss})\n  内容: {json}";
            }
            catch (System.Exception ex)
            {
                return "CommandRelayStore 探针异常: " + ex.Message;
            }
        }

        /// <summary>C1 - 调用最新一次 C2 绑定的 TestCommand.Run 委托</summary>
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
            catch (System.Reflection.TargetInvocationException tie)
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


        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
        {
            var dir = fileInfo.Directory;
            for (int i = 0; i < levelsUp && dir != null; i++)
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("无法解析根目录。");
        }

        /// <summary>将 bin\Debug 复制到新的临时子目录，返回副本中主 DLL 的路径。加载从副本进行，不锁定原始目录；每次用新目录避免覆盖已加载的副本。</summary>
        private static string CopyToTempAndGetLoadPath(string sourceDir, string mainDllPath, Editor ed)
        {
            string tempBase = Path.Combine(Path.GetTempPath(), "HyCADToolRefactored");
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

        /// <summary>
        /// 清理 %TEMP%\HyCADToolRefactored\ 下的旧副本目录，按最后写入时间降序保留最新 <paramref name="retain"/> 个。
        /// 删除失败（如文件被进程锁定）静默跳过，不影响本次 C2 流程。
        /// </summary>
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

        /// <summary>获取静态方法委托（用于 TestCommand.Run）</summary>
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

        private static void InitializeServiceLocator(Assembly targetAssembly, Editor ed)
        {
            try
            {
                var slType = targetAssembly.GetType("HyCADTool.Refactored.Infrastructure.Configuration.ServiceLocator");
                if (slType == null) return;

                var containerProp = slType.GetProperty("Container", BindingFlags.Public | BindingFlags.Static);
                if (containerProp?.GetValue(null) != null) return;

                var moduleType = targetAssembly.GetType("HyCADTool.Refactored.Infrastructure.Configuration.AutofacModule");
                var builderType = Type.GetType("Autofac.ContainerBuilder, Autofac");
                if (moduleType == null || builderType == null) return;

                var builder = Activator.CreateInstance(builderType);
                var registerMethod = builderType.GetMethod("RegisterModule", new[] { Type.GetType("Autofac.Core.IModule, Autofac") });
                registerMethod?.Invoke(builder, new[] { Activator.CreateInstance(moduleType) });
                var container = builderType.GetMethod("Build", Type.EmptyTypes)?.Invoke(builder, null);

                var initMethod = slType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initMethod?.Invoke(null, new[] { container });
            }
            catch
            {
                // 静默忽略，部分命令不依赖 DI
            }
        }

        private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
        {
            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.IsNullOrWhiteSpace(dependenciesPath))
                return null;

            string name = new AssemblyName(args.Name).Name + ".dll";
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

            // 搜索 net8 子目录（MarkdownEditor 依赖如 WebView2）
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
    }

    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}
