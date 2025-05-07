//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Runtime;
//using System;
//using System.IO;
//using System.Reflection;
//using System.Resources;
//[assembly: CommandClass(typeof(HyCADTool.ReCall.ReCallClass))]
//namespace HyCADTool.ReCall
//{
//    // ReCallClass 类负责加载和管理 AutoCAD 插件
//    public class ReCallClass
//    {
//        // 保存插件命令的委托
//        public Action Cmd1Action { get; private set; }
//        // 构造函数，初始化并加载插件
//        public ReCallClass()
//        {
//            Reload();
//        }
//        // AutoCAD 命令，用于重新加载插件
//        [CommandMethod("C2")]
//        public void Reload()
//        {
//            try
//            {
//                // 获取当前程序集的文件信息
//                var adapterFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
//                if (adapterFileInfo.DirectoryName == null)
//                {
//                    throw new InvalidOperationException("无法获取当前程序集的目录名。");
//                }
//                // 获取项目根目录，向上移动4级目录
//                string rootDirectory = GetRootDirectory(adapterFileInfo, 4);
//                // 定义插件和依赖项的路径
//                var targetFilePath = Path.Combine(rootDirectory, "HyCADToolGpt", "HyCADTool", "bin", "Debug", "HyCADTool.dll");
//                var dependenciesPath = Path.Combine(rootDirectory, "HyCADTool", "bin", "Debug", "net8.0-windows");
//                var nugetPackagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
//                if (!File.Exists(targetFilePath))
//                {
//                    throw new FileNotFoundException($"找不到目标文件: {targetFilePath}");
//                }
//                // 注册 AssemblyResolve 事件处理程序，用于解决程序集加载问题
//                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => ResolveAssembly(args, dependenciesPath, nugetPackagesPath);
//                // 加载插件并获取插件命令的委托
//                LoadPlugin(targetFilePath, out Action cmdAction);
//                Cmd1Action = cmdAction;
//                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n插件加载成功。");
//            }
//            catch (System.Exception ex)
//            {
//                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n加载插件失败: {ex.Message}");
//            }
//        }
//        // AutoCAD 命令，用于执行插件命令
//        [CommandMethod("C1")]
//        public void Cmd1()
//         {
//            Cmd1Action?.Invoke();
//        }
//        // 获取指定目录向上移动若干级后的根目录
//        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
//        {
//            DirectoryInfo directoryInfo = fileInfo.Directory;
//            for (int i = 0; i < levelsUp; i++)
//            {
//                if (directoryInfo == null)
//                {
//                    throw new InvalidOperationException("无法向上移动到指定的层级数。");
//                }
//                directoryInfo = directoryInfo.Parent;
//            }
//            // 添加 null 检查，以确保 directoryInfo 不为 null
//            if (directoryInfo == null)
//            {
//                throw new InvalidOperationException("无法获取目录信息。");
//            }
//            return directoryInfo.FullName;
//        }
//        // 加载插件并获取插件命令的委托
//        private void LoadPlugin(string pluginPath, out Action cmdAction)
//        {
//            // 加载插件程序集
//            var targetAssembly = Assembly.Load(File.ReadAllBytes(pluginPath));
//            // 设置 ResourceManager.ResourceAssembly 为动态加载的程序集
//            ResourceManager.ResourceAssembly = targetAssembly;
//            // 获取目标类型和方法
//            var targetType = targetAssembly.GetType("HyCADTool.TestCommand")
//                ?? throw new InvalidOperationException("无法找到目标类型 'HyCADTool.TestCommand'。");
//            var targetMethod = targetType.GetMethod("Test")
//                ?? throw new InvalidOperationException("无法找到目标方法 'Test'。");
//            var targetObject = Activator.CreateInstance(targetType)
//                ?? throw new InvalidOperationException("无法创建目标对象 'HyCADTool.TestCommand' 的实例。");
//            // 创建命令委托
//            cmdAction = () => targetMethod.Invoke(targetObject, null);
//        }
//        // 解析程序集的方法
//        private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
//        {
//            // 排除 .resources 文件，避免将其误认为程序集
//            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
//            {
//                return null;
//            }
//            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
//            // 在本地 dependenciesPath 查找
//            string assemblyPath = Path.Combine(dependenciesPath, assemblyName);
//            if (File.Exists(assemblyPath))
//            {
//                return Assembly.LoadFrom(assemblyPath);
//            }
//            // 在 NuGet 包目录中递归查找
//            var directories = Directory.GetDirectories(nugetPackagesPath, assemblyName.Replace(".dll", ""), SearchOption.AllDirectories);
//            foreach (var dir in directories)
//            {
//                assemblyPath = Path.Combine(dir, assemblyName);
//                if (File.Exists(assemblyPath))
//                {
//                    return Assembly.LoadFrom(assemblyPath);
//                }
//            }
//            return null;
//        }
//    }
//    public static class ResourceManager
//    {
//        public static Assembly ResourceAssembly { get; set; }
//    }
//}
