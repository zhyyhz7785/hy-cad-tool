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

        private Action _c1Action;

        /// <summary>C2 - 重新加载插件（仅在此命令执行时加载，不在构造函数中调用，避免加载时执行两次）</summary>
        [CommandMethod("C2")]
        public void Reload()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

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

                AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                    ResolveAssembly(args, depsPath, nugetPath);

                // 优先使用已加载的程序集，避免再次 Load 触发 eDuplicateKey
                Assembly asm = GetLoadedAssembly(TARGET_PROJECT_NAME);
                if (asm == null)
                {
                    asm = Assembly.Load(File.ReadAllBytes(pluginPath));
                    ed.WriteMessage("\n插件已从文件加载。");
                }
                else
                    ed.WriteMessage("\n插件已就绪（使用当前已加载版本）。");

                ResourceManager.ResourceAssembly = asm;
                InitializeServiceLocator(asm, ed);
                _c1Action = CreateStaticMethodDelegate(asm, TEST_ENTRY_TYPE, TEST_ENTRY_METHOD, ed);

                if (_c1Action != null)
                    ed.WriteMessage(" C2 重载 | C1 测试");
                else
                    ed.WriteMessage(" 请重新生成 HyCADTool.Refactored 后再执行 C2。");
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
                return Assembly.LoadFrom(path);

            try
            {
                var dirs = Directory.GetDirectories(nugetPackagesPath, name.Replace(".dll", ""), SearchOption.AllDirectories);
                foreach (var dir in dirs)
                {
                    path = Path.Combine(dir, name);
                    if (File.Exists(path))
                        return Assembly.LoadFrom(path);
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
