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
        #region ========== 项目配置参数（更换测试项目时修改这里） ==========

        /// <summary>
        /// 目标项目名称
        /// </summary>
        private const string TARGET_PROJECT_NAME = "HyCADTool.Refactored";

        /// <summary>
        /// 目标 DLL 文件名（不含路径）
        /// </summary>
        private const string TARGET_DLL_NAME = "HyCADTool.Refactored.dll";

        /// <summary>
        /// 构建配置（Debug 或 Release）
        /// </summary>
        private const string BUILD_CONFIGURATION = "Debug";

        /// <summary>
        /// 从 ReCall.dll 向上移动到解决方案根目录的层级数
        /// ReCall.dll 位于: hy-cad-tool\ReCall\bin\Debug\ReCall.dll
        /// 向上3级: Debug -> bin -> ReCall -> hy-cad-tool (根目录)
        /// </summary>
        private const int DIRECTORY_LEVELS_UP = 3;

        /// <summary>
        /// TestRunner 类的完整类型名称（包含命名空间）
        /// </summary>
        private const string TEST_RUNNER_TYPE_NAME = "HyCADTool.Refactored.Test.TestRunner";

        /// <summary>
        /// 要调用的测试方法名称
        /// </summary>
        private const string TEST_METHOD_NAME = "RunAllTests";

        /// <summary>
        /// 测试命令配置（C11-C19）
        /// 格式：命令名, 类名, 方法名
        /// 留空表示该槽位未使用
        /// </summary>
        private static readonly (string Command, string ClassName, string MethodName)[] TEST_COMMANDS = new[]
        {
            ("C11", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "Execute"),        // HYOV
            ("C12", "HyCADTool.Refactored.Presentation.Commands.OverKillCommand", "ExecuteSettings"), // HYOVSET
            ("C13", "HyCADTool.Refactored.Presentation.Commands.BreakCurvesCommand", "Execute"),     // HYBC
            ("C14", "HyCADTool.Refactored.Presentation.Commands.Elevation3DCommand", "Execute"),     // HY3 (原方法)
            ("C15", "HyCADTool.Refactored.Presentation.Commands.SurfaceBasedElevation3DCommand", "Execute"),  // HY3 (新方法-表面)
            ("C16", "HyCADTool.Refactored.Presentation.Commands.TestOffsetCommand", "Execute"),      // 测试多边形偏移
            ("C17", "HyCADTool.Refactored.Presentation.Commands.TestPanelCommand", "ShowTestPanel"), // HYTEST (性能对比测试)
            ("C18", "", ""),  // 预留槽位8
            ("C19", "", ""),  // 预留槽位9
        };

        #endregion

        // 保存 TestRunner.RunAllTests 的委托
        private Action _runAllTestsAction;
        
        // 保存测试命令的委托（C11-C19）
        private readonly Dictionary<string, Action> _testCommandActions = new Dictionary<string, Action>();

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
                string rootDirectory = GetRootDirectory(adapterFileInfo, DIRECTORY_LEVELS_UP);
                
                // 定义插件路径（重构项目）
                var targetFilePath = Path.Combine(rootDirectory, TARGET_PROJECT_NAME, "bin", BUILD_CONFIGURATION, TARGET_DLL_NAME);
                var dependenciesPath = Path.Combine(rootDirectory, TARGET_PROJECT_NAME, "bin", BUILD_CONFIGURATION);
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
                
                // 加载测试命令（C11-C19）
                LoadTestCommands(targetFilePath);

                ed.WriteMessage("\n✓ 插件加载成功！");
                ed.WriteMessage("\n" + new string('=', 60));
                ed.WriteMessage("\n可用命令：");
                ed.WriteMessage("\n  C1  - 运行所有测试（自动执行 HYOV）");
                ed.WriteMessage("\n  C2  - 重新加载插件");
                
                // 显示已配置的测试命令
                foreach (var cmd in TEST_COMMANDS)
                {
                    if (!string.IsNullOrEmpty(cmd.ClassName))
                    {
                        var shortClassName = cmd.ClassName.Split('.').Last();
                        ed.WriteMessage($"\n  {cmd.Command} - {shortClassName}.{cmd.MethodName}() ✓");
                    }
                }
                
                ed.WriteMessage("\n");
                ed.WriteMessage("\n⚠️ 重要提示：");
                ed.WriteMessage("\n  - 修改代码后，请使用 C11-C19 测试命令（支持热重启）");
                ed.WriteMessage("\n  - 或者重启 AutoCAD 后，正式命令才会更新");
                ed.WriteMessage("\n  - 在 Recall.cs 的 TEST_COMMANDS 中配置测试命令");
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
            var testRunnerType = targetAssembly.GetType(TEST_RUNNER_TYPE_NAME)
                ?? throw new InvalidOperationException($"无法找到类型 '{TEST_RUNNER_TYPE_NAME}'。");

            // 获取 RunAllTests 方法
            var runAllTestsMethod = testRunnerType.GetMethod(TEST_METHOD_NAME)
                ?? throw new InvalidOperationException($"无法找到方法 '{TEST_METHOD_NAME}'。");

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
        
        /// <summary>
        /// 初始化 ServiceLocator（如果尚未初始化）
        /// </summary>
        private void InitializeServiceLocator(Assembly targetAssembly, Editor ed)
        {
            try
            {
                // 获取 ServiceLocator 类型
                var serviceLocatorType = targetAssembly.GetType("HyCADTool.Refactored.Infrastructure.Configuration.ServiceLocator");
                if (serviceLocatorType == null)
                {
                    ed?.WriteMessage("\n⚠️ 警告：找不到 ServiceLocator 类型");
                    return;
                }
                
                // 获取 Container 属性
                var containerProperty = serviceLocatorType.GetProperty("Container", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                // 检查 Container 是否已初始化
                try
                {
                    var existingContainer = containerProperty?.GetValue(null);
                    if (existingContainer != null)
                    {
                        // 已初始化，无需重复初始化
                        return;
                    }
                }
                catch
                {
                    // Container 未初始化，继续初始化流程
                }
                
                // 获取 AutofacModule 类型
                var autofacModuleType = targetAssembly.GetType("HyCADTool.Refactored.Infrastructure.Configuration.AutofacModule");
                if (autofacModuleType == null)
                {
                    ed?.WriteMessage("\n⚠️ 警告：找不到 AutofacModule 类型");
                    return;
                }
                
                // 创建 ContainerBuilder
                var containerBuilderType = Type.GetType("Autofac.ContainerBuilder, Autofac");
                if (containerBuilderType == null)
                {
                    ed?.WriteMessage("\n⚠️ 警告：找不到 Autofac.ContainerBuilder 类型");
                    return;
                }
                
                var builder = Activator.CreateInstance(containerBuilderType);
                
                // 调用 builder.RegisterModule(new AutofacModule())
                var registerModuleMethod = containerBuilderType.GetMethod("RegisterModule", 
                    new[] { Type.GetType("Autofac.Core.IModule, Autofac") });
                var moduleInstance = Activator.CreateInstance(autofacModuleType);
                registerModuleMethod?.Invoke(builder, new[] { moduleInstance });
                
                // 调用 builder.Build()
                var buildMethod = containerBuilderType.GetMethod("Build", Type.EmptyTypes);
                var container = buildMethod?.Invoke(builder, null);
                
                // 调用 ServiceLocator.Initialize(container)
                var initializeMethod = serviceLocatorType.GetMethod("Initialize", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                initializeMethod?.Invoke(null, new[] { container });
                
                ed?.WriteMessage("\n✓ ServiceLocator 已初始化");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n⚠️ ServiceLocator 初始化失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ed?.WriteMessage($"\n   内部异常: {ex.InnerException.Message}");
                }
                ed?.WriteMessage($"\n   堆栈: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 加载测试命令（C11-C19）
        /// </summary>
        private void LoadTestCommands(string pluginPath)
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            
            // 清空现有命令
            _testCommandActions.Clear();
            
            // 加载插件程序集
            var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));
            
            // 初始化 ServiceLocator（如果尚未初始化）
            InitializeServiceLocator(targetAssembly, ed);
            
            int loadedCount = 0;
            foreach (var (command, className, methodName) in TEST_COMMANDS)
            {
                // 跳过未配置的槽位
                if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(methodName))
                    continue;
                
                try
                {
                    // 获取类型
                    var commandType = targetAssembly.GetType(className);
                    if (commandType == null)
                    {
                        ed?.WriteMessage($"\n⚠️ 警告：找不到类型 '{className}'");
                        continue;
                    }
                    
                    // 获取方法
                    var method = commandType.GetMethod(methodName);
                    if (method == null)
                    {
                        ed?.WriteMessage($"\n⚠️ 警告：找不到方法 '{className}.{methodName}'");
                        continue;
                    }
                    
                    // 创建实例
                    var instance = Activator.CreateInstance(commandType);
                    if (instance == null)
                    {
                        ed?.WriteMessage($"\n⚠️ 警告：无法创建 '{className}' 的实例");
                        continue;
                    }
                    
                    // 创建委托并保存
                    _testCommandActions[command] = () => method.Invoke(instance, null);
                    loadedCount++;
                }
                catch (System.Exception ex)
                {
                    ed?.WriteMessage($"\n⚠️ 警告：加载 {command} 失败: {ex.Message}");
                }
            }
            
            ed?.WriteMessage($"\n✓ 已加载 {loadedCount} 个测试命令");
        }
        
        /// <summary>
        /// 执行测试命令的通用方法
        /// </summary>
        private void ExecuteTestCommand(string commandName)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                System.Windows.Forms.MessageBox.Show("没有活动的文档。", "错误", 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
            
            var ed = doc.Editor;
            
            try
            {
                if (!_testCommandActions.ContainsKey(commandName))
                {
                    ed.WriteMessage($"\n✗ 命令 {commandName} 未配置或加载失败");
                    ed.WriteMessage($"\n   请在 Recall.cs 的 TEST_COMMANDS 中配置该命令");
                    return;
                }
                
                // 执行命令
                _testCommandActions[commandName].Invoke();
            }
            catch (System.Exception ex)
            {
                // 使用 MessageBox 显示错误，避免在错误的上下文中调用 WriteMessage
                var errorMsg = $"命令 {commandName} 执行失败:\n\n" +
                              $"错误: {ex.Message}\n\n" +
                              $"堆栈跟踪:\n{ex.StackTrace}";
                
                if (ex.InnerException != null)
                {
                    errorMsg += $"\n\n内部异常:\n{ex.InnerException.Message}";
                }
                
                System.Windows.Forms.MessageBox.Show(errorMsg, $"命令 {commandName} 执行失败", 
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
        
        // 自动生成 C11-C19 命令方法
        [CommandMethod("C11")] public void ExecuteC11() => ExecuteTestCommand("C11");
        [CommandMethod("C12")] public void ExecuteC12() => ExecuteTestCommand("C12");
        [CommandMethod("C13")] public void ExecuteC13() => ExecuteTestCommand("C13");
        [CommandMethod("C14")] public void ExecuteC14() => ExecuteTestCommand("C14");
        [CommandMethod("C15")] public void ExecuteC15() => ExecuteTestCommand("C15");
        [CommandMethod("C16")] public void ExecuteC16() => ExecuteTestCommand("C16");
        [CommandMethod("C17")] public void ExecuteC17() => ExecuteTestCommand("C17");
        [CommandMethod("C18")] public void ExecuteC18() => ExecuteTestCommand("C18");
        [CommandMethod("C19")] public void ExecuteC19() => ExecuteTestCommand("C19");
    }

    /// <summary>
    /// 资源管理器
    /// </summary>
    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}
