using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
[assembly: CommandClass(typeof(HyCADTool.ReCall.ReCallClass))]
namespace HyCADTool.ReCall
{
    public class ReCallClass
    {
        // 路径参数 - 可修改的目录设置
        private static readonly string RootLevelsUp = "4"; // 上级目录层数
        private static readonly string PluginFolderRelativePath = @"hy-cad-tool\HyCADTool.Refactored\bin\Debug";
        private static readonly string TargetDllName = "HyCADTool.Refactored.dll";
        private static readonly string TempDllName = "HyCADTool.Refactored_temp.dll";
        private static readonly string NugetPackagesRelativePath = ".nuget\\packages";
        // 其他字段保持不变
        private Action Cmd1Action { get; set; }
        private Action Cmd2Phase2Action { get; set; }
        private Action Cmd3Phase3Action { get; set; }
        private Action Cmd4Phase4Action { get; set; }
        private static string DependenciesPath;
        private static string NugetPackagesPath;
        private static readonly Dictionary<string, Assembly> AssemblyCache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        [CommandMethod("C2")]
        public void Reload()
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                string rootDir = GetRootDirectory(new FileInfo(Assembly.GetExecutingAssembly().Location), int.Parse(RootLevelsUp));
                string pluginFolder = Path.Combine(rootDir, PluginFolderRelativePath);
                string targetPath = Path.Combine(pluginFolder, TargetDllName);
                string tempPath = Path.Combine(Path.GetTempPath(), TempDllName);
                if (!File.Exists(targetPath))
                {
                    editor.WriteMessage($"\n找不到目标文件: {targetPath}");
                    return;
                }
                AssemblyCache.Clear();
                HandleTempFile(tempPath, targetPath, editor);
                DependenciesPath = pluginFolder;
                NugetPackagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), NugetPackagesRelativePath);
                AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssemblyHandler;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyHandler;
                LoadPlugin(tempPath, out Action cmd1Action, out Action cmd2Phase2Action, out Action cmd3Phase3Action, out Action cmd4Phase4Action);
                Cmd1Action = cmd1Action;
                Cmd2Phase2Action = cmd2Phase2Action;
                Cmd3Phase3Action = cmd3Phase3Action;
                Cmd4Phase4Action = cmd4Phase4Action;
                editor.WriteMessage("\n插件加载成功");
                editor.WriteMessage("\n可用命令: C1 (阶段1测试), C1P2 (阶段2测试), C1P3 (阶段3测试), C1P4 (阶段4测试)");
            }
            catch (Exception ex)
            {
                editor.WriteMessage($"\n加载插件失败: {ex.Message}");
            }
        }
        [CommandMethod("C1")]
        public void Cmd1() => Cmd1Action?.Invoke();

        [CommandMethod("C1P2")]
        public void Cmd1Phase2() => Cmd2Phase2Action?.Invoke();

        [CommandMethod("C1P3")]
        public void Cmd1Phase3() => Cmd3Phase3Action?.Invoke();

        [CommandMethod("C1P4")]
        public void Cmd1Phase4() => Cmd4Phase4Action?.Invoke();
        private static string GetRootDirectory(FileInfo fileInfo, int levelsUp)
        {
            var dir = fileInfo.Directory;
            for (int i = 0; i < levelsUp && dir != null; i++) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("无法获取根目录");
        }
        private static void HandleTempFile(string tempPath, string targetPath, Editor editor)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.SetAttributes(tempPath, FileAttributes.Normal);
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                editor.WriteMessage($"\n无法删除临时文件: {ex.Message}");
                string backupPath = tempPath + ".bak";
                try
                {
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    if (File.Exists(tempPath)) File.Move(tempPath, backupPath);
                }
                catch (Exception e)
                {
                    editor.WriteMessage($"\n临时文件清理失败: {e.Message}");
                }
            }
            File.Copy(targetPath, tempPath, true);
        }
        private void LoadPlugin(string pluginPath, out Action cmd1Action, out Action cmd2Phase2Action, out Action cmd3Phase3Action, out Action cmd4Phase4Action)
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var assembly = Assembly.Load(File.ReadAllBytes(pluginPath));
                ResourceManager.ResourceAssembly = assembly;
                AppDomain.CurrentDomain.SetData("HyCADToolAssembly", assembly);
                editor.WriteMessage($"\n加载程序集: {assembly.FullName}");
                
                // 加载 Phase1 测试
                var type1 = assembly.GetType("HyCADTool.Refactored.Test.Phase1TestCommand") 
                    ?? throw new InvalidOperationException("未找到类型 'Phase1TestCommand'");
                var method1 = type1.GetMethod("RunAllPhase1Tests") 
                    ?? throw new InvalidOperationException("未找到方法 'RunAllPhase1Tests'");
                cmd1Action = () => method1.Invoke(null, null);

                // 加载 Phase2 测试
                var type2 = assembly.GetType("HyCADTool.Refactored.Test.Phase2TestCommand") 
                    ?? throw new InvalidOperationException("未找到类型 'Phase2TestCommand'");
                var method2 = type2.GetMethod("RunAllPhase2Tests") 
                    ?? throw new InvalidOperationException("未找到方法 'RunAllPhase2Tests'");
                cmd2Phase2Action = () => method2.Invoke(null, null);

                // 加载 Phase3 测试
                var type3 = assembly.GetType("HyCADTool.Refactored.Test.Phase3TestCommand") 
                    ?? throw new InvalidOperationException("未找到类型 'Phase3TestCommand'");
                var method3 = type3.GetMethod("RunAllPhase3Tests") 
                    ?? throw new InvalidOperationException("未找到方法 'RunAllPhase3Tests'");
                cmd3Phase3Action = () => method3.Invoke(null, null);

                // 加载 Phase4 测试
                var type4 = assembly.GetType("HyCADTool.Refactored.Test.Phase4TestCommand") 
                    ?? throw new InvalidOperationException("未找到类型 'Phase4TestCommand'");
                var method4 = type4.GetMethod("RunAllPhase4Tests") 
                    ?? throw new InvalidOperationException("未找到方法 'RunAllPhase4Tests'");
                cmd4Phase4Action = () => method4.Invoke(null, null);
            }
            catch (Exception ex)
            {
                editor.WriteMessage($"\n加载插件失败: {ex.Message}");
                throw;
            }
        }
        private static Assembly ResolveAssemblyHandler(object sender, ResolveEventArgs args)
        {
            if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            if (AssemblyCache.TryGetValue(assemblyName, out var cached)) return cached;
            string path = Path.Combine(DependenciesPath, assemblyName);
            if (File.Exists(path))
            {
                var assembly = Assembly.LoadFrom(path);
                AssemblyCache[assemblyName] = assembly;
                return assembly;
            }
            foreach (var dir in Directory.GetDirectories(NugetPackagesPath, assemblyName.Replace(".dll", ""), SearchOption.AllDirectories))
            {
                path = Path.Combine(dir, assemblyName);
                if (File.Exists(path))
                {
                    var assembly = Assembly.LoadFrom(path);
                    AssemblyCache[assemblyName] = assembly;
                    return assembly;
                }
            }
            if (args.Name.StartsWith("HyCADTool", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceManager.ResourceAssembly ?? (Assembly)AppDomain.CurrentDomain.GetData("HyCADToolAssembly");
            }
            return null;
        }
    }
    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}