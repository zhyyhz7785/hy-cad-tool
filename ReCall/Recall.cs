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
    /// HyCADTool.Refactored 通用热重启类
    /// 用于快速测试重构代码而无需重启 AutoCAD
    /// 
    /// 使用说明：
    /// - C2: 重新加载插件（修改代码后执行）
    /// - C1: 运行所有测试（执行 TestRunner.RunAllTests）
    /// 
    /// 注意：
    /// - 本文件在后续重构中不应再修改
    /// - 所有测试逻辑应在 HyCADTool.Refactored/Test 中实现
    /// - 分部测试应通过修改 TestRunner 实现
    /// </summary>
    public class ReCallClass
    {
        // 保存 TestRunner.RunAllTests 的委托
        private Action _runAllTestsAction;

        /// <summary>
        /// 构造函数，初始化并加载插件
        /// </summary>
        public ReCallClass()
        {
            Reload();
        }

        /// <summary>
        /// AutoCAD 命令：C2 - 重新加载插件
        /// 修改 HyCADTool.Refactored 代码后执行此命令刷新
        /// </summary>
        [CommandMethod("C2")]
        public void Reload()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc?.Editor;
                
                if (ed == null)
                {
                    return;
                }

                ed.WriteMessage("\n" + new string('=', 60));
                ed.WriteMessage("\n开始重新加载 HyCADTool.Refactored.dll...");
                ed.WriteMessage("\n" + new string('=', 60));

                // 获取当前程序集的文件信息
                var adapterFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
                if (adapterFileInfo.DirectoryName == null)
                {
                    throw new InvalidOperationException("无法获取当前程序集的目录名。");
                }

                // 获取解决方案根目录
                // ReCall.dll 位于: hy-cad-tool\ReCall\bin\Debug\ReCall.dll
                // 向上3级目录到达 hy-cad-tool 根目录
                string rootDirectory = GetRootDirectory(adapterFileInfo, 3);
                
                // 定义插件路径（重构项目）
                var targetFilePath = Path.Combine(rootDirectory, "HyCADTool.Refactored", "bin", "Debug", "HyCADTool.Refactored.dll");
                var dependenciesPath = Path.Combine(rootDirectory, "HyCADTool.Refactored", "bin", "Debug");
                var nugetPackagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

                ed.WriteMessage($"\n解决方案根目录: {rootDirectory}");

                ed.WriteMessage($"\n插件路径: {targetFilePath}");
                
                if (!File.Exists(targetFilePath))
                {
                    throw new FileNotFoundException($"找不到目标文件: {targetFilePath}");
                }

                // 注册 AssemblyResolve 事件处理程序
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => 
                    ResolveAssembly(args, dependenciesPath, nugetPackagesPath);

                // 加载插件并获取 TestRunner
                LoadPlugin(targetFilePath, out _runAllTestsAction);

                ed.WriteMessage("\n✓ 插件加载成功！");
                ed.WriteMessage("\n" + new string('=', 60));
                ed.WriteMessage("\n可用命令：");
                ed.WriteMessage("\n  C1 - 运行所有测试");
                ed.WriteMessage("\n  C2 - 重新加载插件");
                ed.WriteMessage("\n" + new string('=', 60));
                ed.WriteMessage("\n");
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n✗ 加载插件失败: {ex.Message}");
                ed?.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// AutoCAD 命令：C1 - 运行所有测试
        /// 执行 TestRunner.RunAllTests() 方法
        /// </summary>
        [CommandMethod("C1")]
        public void ExecuteAllTests()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                if (_runAllTestsAction == null)
                {
                    ed.WriteMessage("\n✗ 测试运行器未初始化，请先执行 C2 命令加载插件");
                    return;
                }

                // 执行所有测试
                _runAllTestsAction.Invoke();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n✗ 测试执行失败: {ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 获取指定目录向上移动若干级后的根目录
        /// </summary>
        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
        {
            DirectoryInfo directoryInfo = fileInfo.Directory;
            for (int i = 0; i < levelsUp; i++)
            {
                if (directoryInfo == null)
                {
                    throw new InvalidOperationException("无法向上移动到指定的层级数。");
                }
                directoryInfo = directoryInfo.Parent;
            }

            if (directoryInfo == null)
            {
                throw new InvalidOperationException("无法获取目录信息。");
            }

            return directoryInfo.FullName;
        }

        /// <summary>
        /// 加载插件并获取 TestRunner.RunAllTests 的委托
        /// </summary>
        private void LoadPlugin(string pluginPath, out Action runAllTestsAction)
        {
            // 加载插件程序集
            var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));

            // 设置 ResourceManager.ResourceAssembly
            ResourceManager.ResourceAssembly = targetAssembly;

            // 获取 TestRunner 类型
            var testRunnerType = targetAssembly.GetType("HyCADTool.Refactored.Test.TestRunner")
                ?? throw new InvalidOperationException("无法找到类型 'TestRunner'。");

            // 获取 RunAllTests 方法
            var runAllTestsMethod = testRunnerType.GetMethod("RunAllTests")
                ?? throw new InvalidOperationException("无法找到方法 'RunAllTests'。");

            // 创建 TestRunner 实例
            var testRunnerInstance = Activator.CreateInstance(testRunnerType)
                ?? throw new InvalidOperationException("无法创建 TestRunner 的实例。");

            // 创建委托
            runAllTestsAction = () => runAllTestsMethod.Invoke(testRunnerInstance, null);
        }

        /// <summary>
        /// 解析程序集
        /// </summary>
        private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
        {
            // 排除 .resources 文件
            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string assemblyName = new AssemblyName(args.Name).Name + ".dll";

            // 在本地 dependenciesPath 查找
            string assemblyPath = Path.Combine(dependenciesPath, assemblyName);
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            // 在 NuGet 包目录中递归查找
            try
            {
                var directories = Directory.GetDirectories(nugetPackagesPath, assemblyName.Replace(".dll", ""), SearchOption.AllDirectories);
                foreach (var dir in directories)
                {
                    assemblyPath = Path.Combine(dir, assemblyName);
                    if (File.Exists(assemblyPath))
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
            }
            catch
            {
                // 忽略搜索错误
            }

            return null;
        }
    }

    /// <summary>
    /// 资源管理器
    /// </summary>
    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}
