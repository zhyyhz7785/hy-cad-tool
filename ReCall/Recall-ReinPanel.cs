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
        private static readonly string PluginFolderRelativePath = @"hy-cad-tool\ReinPanel.Refactored\bin\Debug\net48";
        private static readonly string TargetDllName = "ReinPanel.Refactored.dll";
        private static readonly string TempDllName = "ReinPanel.Refactored_temp.dll";
        private static readonly string NugetPackagesRelativePath = ".nuget\\packages";
        // 其他字段保持不变
        private Action Cmd1Action { get; set; }
        private Action Cmd2Phase2Action { get; set; }
        private Action Cmd3Phase3Action { get; set; }
        private Action Cmd4Phase4Action { get; set; }
        private Action Cmd5Phase50Action { get; set; }
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
                LoadPlugin(tempPath, out Action cmd1Action, out Action cmd2Phase2Action, out Action cmd3Phase3Action, out Action cmd4Phase4Action, out Action cmd5Phase50Action);
                Cmd1Action = cmd1Action;
                Cmd2Phase2Action = cmd2Phase2Action;
                Cmd3Phase3Action = cmd3Phase3Action;
                Cmd4Phase4Action = cmd4Phase4Action;
                Cmd5Phase50Action = cmd5Phase50Action;
                editor.WriteMessage("\n插件加载成功");
                editor.WriteMessage("\n可用命令:");
                editor.WriteMessage("\n  C1    - 打开钢筋面板 (ShowPanel)");
                editor.WriteMessage("\n  C1P2  - 初始化容器 (InitializeContainer)");
                editor.WriteMessage("\n  C1P3  - 绘制钢筋 (DrawReinforcement)");
                editor.WriteMessage("\n  C1P4  - 三点标注 (DimensionRein1)");
                editor.WriteMessage("\n  C1P50 - 六点标注 (DimensionRein3)");
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

        [CommandMethod("C1P50")]
        public void Cmd1Phase50() => Cmd5Phase50Action?.Invoke();
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
        private void LoadPlugin(string pluginPath, out Action cmd1Action, out Action cmd2Phase2Action, out Action cmd3Phase3Action, out Action cmd4Phase4Action, out Action cmd5Phase50Action)
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var assembly = Assembly.Load(File.ReadAllBytes(pluginPath));
                ResourceManager.ResourceAssembly = assembly;
                AppDomain.CurrentDomain.SetData("PluginAssembly", assembly);
                editor.WriteMessage($"\n加载程序集: {assembly.FullName}");

                // ReinPanel 测试命令
                var testType = assembly.GetType("ReinPanel.Refactored.Test.ReinPanelTestCommand")
                    ?? throw new InvalidOperationException("未找到类型 'ReinPanel.Refactored.Test.ReinPanelTestCommand'");

                var showPanel = testType.GetMethod("ShowPanel")
                    ?? throw new InvalidOperationException("未找到方法 'ShowPanel'");
                var initContainer = testType.GetMethod("InitializeContainer")
                    ?? throw new InvalidOperationException("未找到方法 'InitializeContainer'");
                var drawRein = testType.GetMethod("DrawReinforcement")
                    ?? throw new InvalidOperationException("未找到方法 'DrawReinforcement'");
                var dim1 = testType.GetMethod("DimensionRein1")
                    ?? throw new InvalidOperationException("未找到方法 'DimensionRein1'");
                var dim3 = testType.GetMethod("DimensionRein3")
                    ?? throw new InvalidOperationException("未找到方法 'DimensionRein3'");

                // 将 C1/C1P* 映射到 ReinPanel 的测试方法
                cmd1Action = () => showPanel.Invoke(null, null);
                cmd2Phase2Action = () => initContainer.Invoke(null, null);
                cmd3Phase3Action = () => drawRein.Invoke(null, null);
                cmd4Phase4Action = () => dim1.Invoke(null, null);
                cmd5Phase50Action = () => dim3.Invoke(null, null);
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
            if (args.Name.StartsWith("HyCADTool", StringComparison.OrdinalIgnoreCase)
                || args.Name.StartsWith("ReinPanel", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceManager.ResourceAssembly ?? (Assembly)AppDomain.CurrentDomain.GetData("PluginAssembly");
            }
            return null;
        }
    }
    public static class ResourceManager
    {
        public static Assembly ResourceAssembly { get; set; }
    }
}
