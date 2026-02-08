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
    /// 热重启：C2 重载插件，C1 测试当前配置的一个命令。
    /// </summary>
    public class ReCallClass
    {
        #region ========== 配置（换测哪个命令就改这里） ==========

        private const string TARGET_PROJECT_NAME = "HyCADTool.Refactored";
        private const string TARGET_DLL_NAME = "HyCADTool.Refactored.dll";
        private const string BUILD_CONFIGURATION = "Debug";
        /// <summary>ReCall.dll 到解决方案根的层级：Debug→bin→ReCall→根 = 3</summary>
        private const int DIRECTORY_LEVELS_UP = 3;

        /// <summary>C1 要执行的命令类（完整命名空间.类名）</summary>
        private const string C1_CLASS_NAME = "HyCADTool.Refactored.Presentation.Commands.OverKillCommand";
        /// <summary>C1 要调用的方法名</summary>
        private const string C1_METHOD_NAME = "Execute";

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

                var asm = Assembly.Load(File.ReadAllBytes(pluginPath));
                ResourceManager.ResourceAssembly = asm;

                InitializeServiceLocator(asm, ed);
                _c1Action = CreateCommandDelegate(asm, C1_CLASS_NAME, C1_METHOD_NAME, ed);

                ed.WriteMessage("\n插件已加载。C2 重载 | C1 测试");
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

        private static Action CreateCommandDelegate(Assembly asm, string className, string methodName, Editor ed)
        {
            var type = asm.GetType(className);
            if (type == null)
            {
                ed?.WriteMessage("\n⚠ 未找到类型: " + className);
                return null;
            }
            var method = type.GetMethod(methodName);
            if (method == null)
            {
                ed?.WriteMessage("\n⚠ 未找到方法: " + className + "." + methodName);
                return null;
            }
            var instance = Activator.CreateInstance(type);
            if (instance == null)
            {
                ed?.WriteMessage("\n⚠ 无法创建实例: " + className);
                return null;
            }
            return () => method.Invoke(instance, null);
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
