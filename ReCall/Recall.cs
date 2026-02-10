using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.IO;
using System.Reflection;
using System.Resources;

[assembly: CommandClass(typeof(HyCADTool.ReCall.ReCallClass))]

namespace HyCADTool.ReCall
{
    /// <summary>
    /// 热重启：C2 重载插件，C1 执行 TestCommand.Run()。要测谁请改 Refactored/Test/TestCommand.cs。
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

        #endregion

        private static Action _c1Action;
        private static bool _assemblyResolveRegistered = false;

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
                    return;
                }

                // 复制到临时目录再加载
                var swCopy = System.Diagnostics.Stopwatch.StartNew();
                string loadPath = CopyToTempAndGetLoadPath(depsPath, pluginPath, ed);
                if (loadPath == null) return;
                string loadDepsPath = Path.GetDirectoryName(loadPath);
                swCopy.Stop();

                // 只注册一次 AssemblyResolve 事件
                if (!_assemblyResolveRegistered)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                        ResolveAssembly(args, loadDepsPath, nugetPath);
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

                if (_c1Action != null)
                    ed.WriteMessage($"\nC2 完成 {sw.ElapsedMilliseconds}ms (复制{swCopy.ElapsedMilliseconds} + 加载{swLoad.ElapsedMilliseconds} + DI{swDi.ElapsedMilliseconds})");
                else
                    ed.WriteMessage("\n请重新生成后再执行 C2。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 加载失败: " + ex.Message);
            }
        }

        /// <summary>C1 - 执行当前配置的一个测试命令</summary>
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
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ 执行失败: " + ex.Message);
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
                Directory.CreateDirectory(tempDir);
                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    string dest = Path.Combine(tempDir, Path.GetFileName(file));
                    try
                    {
                        File.Copy(file, dest, true);
                    }
                    catch (System.Exception)
                    {
                        // 忽略单文件失败
                    }
                }
                string loadPath = Path.Combine(tempDir, Path.GetFileName(mainDllPath));
                return File.Exists(loadPath) ? loadPath : null;
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\n✗ 复制到临时目录失败: " + ex.Message);
                return null;
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

            string name = new AssemblyName(args.Name).Name + ".dll";
            string path = Path.Combine(dependenciesPath, name);
            if (File.Exists(path))
            {
                // 使用 Load(byte[]) 而不是 LoadFrom，避免锁定文件
                try
                {
                    return Assembly.Load(File.ReadAllBytes(path));
                }
                catch
                {
                    return null;
                }
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
