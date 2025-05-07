//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Runtime;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Reflection;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;

//[assembly: CommandClass(typeof(HyRetainingWallSolver.ReCall.ReCallClass))]
//namespace HyRetainingWallSolver.ReCall
//{
//    public class ReCallClass
//    {
//        // 路径参数 - 可修改的目录设置
//        private static readonly string RootLevelsUp = "4"; // 上级目录层数
//        private static readonly string PluginFolderRelativePath = @"HyCADToolGpt\HyRetainingWallSolver\bin\Debug";
//        private static readonly string TargetDllName = "HyRetainingWallSolver.dll";
//        private static readonly string TempDllName = "HyRetainingWallSolver_temp.dll";
//        private static readonly string NugetPackagesRelativePath = ".nuget\\packages";

//        // 其他字段保持不变
//        private Action Cmd1Action { get; set; }
//        private static string DependenciesPath;
//        private static string NugetPackagesPath;
//        private static readonly Dictionary<string, Assembly> AssemblyCache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

//        [CommandMethod("C2")]
//        public void Reload()
//        {
//            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
//            try
//            {
//                string rootDir = GetRootDirectory(new FileInfo(Assembly.GetExecutingAssembly().Location), int.Parse(RootLevelsUp));
//                string pluginFolder = Path.Combine(rootDir, PluginFolderRelativePath);
//                string targetPath = Path.Combine(pluginFolder, TargetDllName);
//                string tempPath = Path.Combine(Path.GetTempPath(), TempDllName);

//                if (!File.Exists(targetPath))
//                {
//                    editor.WriteMessage($"\n找不到目标文件: {targetPath}");
//                    return;
//                }

//                AssemblyCache.Clear();
//                HandleTempFile(tempPath, targetPath, editor);
//                DependenciesPath = pluginFolder;
//                NugetPackagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), NugetPackagesRelativePath);
//                AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssemblyHandler;
//                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyHandler;
//                LoadPlugin(tempPath, out Action cmdAction);
//                Cmd1Action = cmdAction;
//                editor.WriteMessage("\n插件加载成功");
//            }
//            catch (Exception ex)
//            {
//                editor.WriteMessage($"\n加载插件失败: {ex.Message}");
//            }
//        }

//        [CommandMethod("C1")]
//        public void Cmd1() => Cmd1Action?.Invoke();

//        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
//        {
//            var dir = fileInfo.Directory;
//            for (int i = 0; i < levelsUp && dir != null; i++) dir = dir.Parent;
//            return dir?.FullName ?? throw new InvalidOperationException("无法获取根目录");
//        }

//        private static void HandleTempFile(string tempPath, string targetPath, Editor editor)
//        {
//            try
//            {
//                if (File.Exists(tempPath))
//                {
//                    File.SetAttributes(tempPath, FileAttributes.Normal);
//                    File.Delete(tempPath);
//                }
//            }
//            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
//            {
//                editor.WriteMessage($"\n无法删除临时文件: {ex.Message}");
//                string backupPath = tempPath + ".bak";
//                try
//                {
//                    if (File.Exists(backupPath)) File.Delete(backupPath);
//                    if (File.Exists(tempPath)) File.Move(tempPath, backupPath);
//                }
//                catch (Exception e)
//                {
//                    editor.WriteMessage($"\n临时文件清理失败: {e.Message}");
//                }
//            }
//            File.Copy(targetPath, tempPath, true);
//        }

//        private void LoadPlugin(string pluginPath, out Action cmdAction)
//        {
//            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
//            try
//            {
//                var assembly = Assembly.Load(File.ReadAllBytes(pluginPath));
//                ResourceManager.ResourceAssembly = assembly;
//                AppDomain.CurrentDomain.SetData("HyRetainingWallSolverAssembly", assembly);
//                editor.WriteMessage($"\n加载程序集: {assembly.FullName}");
//                var type = assembly.GetType("HyRetainingWallSolver.TestCommand") ?? throw new InvalidOperationException("未找到类型 'TestCommand'");
//                var method = type.GetMethod("Test") ?? throw new InvalidOperationException("未找到方法 'Test'");
//                var instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException("无法实例化 'TestCommand'");
//                cmdAction = () => method.Invoke(instance, null);
//            }
//            catch (Exception ex)
//            {
//                editor.WriteMessage($"\n加载插件失败: {ex.Message}");
//                throw;
//            }
//        }

//        private static Assembly ResolveAssemblyHandler(object sender, ResolveEventArgs args)
//        {
//            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
//            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
//            if (AssemblyCache.TryGetValue(assemblyName, out var cached)) return cached;
//            string path = Path.Combine(DependenciesPath, assemblyName);
//            if (File.Exists(path))
//            {
//                var assembly = Assembly.LoadFrom(path);
//                AssemblyCache[assemblyName] = assembly;
//                return assembly;
//            }
//            foreach (var dir in Directory.GetDirectories(NugetPackagesPath, assemblyName.Replace(".dll", ""), SearchOption.AllDirectories))
//            {
//                path = Path.Combine(dir, assemblyName);
//                if (File.Exists(path))
//                {
//                    var assembly = Assembly.LoadFrom(path);
//                    AssemblyCache[assemblyName] = assembly;
//                    return assembly;
//                }
//            }
//            if (args.Name.StartsWith("HyRetainingWallSolver", StringComparison.OrdinalIgnoreCase))
//            {
//                return ResourceManager.ResourceAssembly ?? (Assembly)AppDomain.CurrentDomain.GetData("HyRetainingWallSolverAssembly");
//            }
//            return null;
//        }
//    }

//    public static class ResourceManager
//    {
//        public static Assembly ResourceAssembly { get; set; }
//    }
//}