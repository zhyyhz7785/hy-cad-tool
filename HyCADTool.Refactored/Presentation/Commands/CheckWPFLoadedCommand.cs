using Autodesk.AutoCAD.Runtime;
using System;
using System.Linq;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 检查 WPF 程序集是否已被 AutoCAD 加载
    /// 命令：CHECKWPF
    /// </summary>
    public class CheckWPFLoadedCommand
    {
        public void CheckWPF()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            
            try
            {
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n检查 WPF 程序集是否已被加载...");
                ed.WriteMessage("\n========================================");
                
                // 获取当前应用程序域中已加载的所有程序集
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                
                // 要检查的 WPF 关键程序集
                string[] wpfAssemblies = new[]
                {
                    "PresentationCore",
                    "PresentationFramework",
                    "WindowsBase",
                    "System.Xaml"
                };
                
                ed.WriteMessage("\n\nWPF 程序集加载状态：");
                ed.WriteMessage("\n----------------------------------------");
                
                int loadedCount = 0;
                foreach (var wpfName in wpfAssemblies)
                {
                    var assembly = loadedAssemblies.FirstOrDefault(a => 
                        a.GetName().Name.Equals(wpfName, StringComparison.OrdinalIgnoreCase));
                    
                    if (assembly != null)
                    {
                        ed.WriteMessage($"\n✅ {wpfName,-25} 已加载");
                        ed.WriteMessage($"\n   版本: {assembly.GetName().Version}");
                        ed.WriteMessage($"\n   位置: {assembly.Location}");
                        loadedCount++;
                    }
                    else
                    {
                        ed.WriteMessage($"\n❌ {wpfName,-25} 未加载");
                    }
                }
                
                ed.WriteMessage("\n----------------------------------------");
                ed.WriteMessage($"\n总结: {loadedCount}/{wpfAssemblies.Length} 个 WPF 程序集已加载");
                
                if (loadedCount == wpfAssemblies.Length)
                {
                    ed.WriteMessage("\n\n🎯 结论：WPF 框架已完全加载！");
                    ed.WriteMessage("\n这解释了为什么 NETLOAD 和 HYOVSET 都很快：");
                    ed.WriteMessage("\n  - NETLOAD 不需要加载 WPF 程序集");
                    ed.WriteMessage("\n  - InitializeComponent 不需要初始化 WPF 框架");
                    ed.WriteMessage("\n  - 5秒的加载时间已在 AutoCAD 启动时完成");
                }
                else if (loadedCount > 0)
                {
                    ed.WriteMessage("\n\n⚠️ 结论：部分 WPF 程序集已加载");
                    ed.WriteMessage("\n  - 可能 AutoCAD 使用了部分 WPF 功能");
                }
                else
                {
                    ed.WriteMessage("\n\n❌ 结论：WPF 程序集未加载");
                    ed.WriteMessage("\n  - WPF 将在首次创建 WPF 控件时加载");
                }
                
                // 额外信息：显示所有已加载的程序集数量
                ed.WriteMessage("\n\n📊 额外信息：");
                ed.WriteMessage($"\n  当前应用程序域已加载程序集总数: {loadedAssemblies.Length}");
                
                // 显示所有 WPF 相关的程序集（包括其他名称的）
                var allWpfRelated = loadedAssemblies.Where(a => 
                    a.GetName().Name.Contains("Presentation") || 
                    a.GetName().Name.Contains("Windows") ||
                    a.GetName().Name.Contains("Xaml"));
                
                if (allWpfRelated.Any())
                {
                    ed.WriteMessage("\n\n  所有 WPF/Windows 相关程序集：");
                    foreach (var asm in allWpfRelated)
                    {
                        ed.WriteMessage($"\n    - {asm.GetName().Name}");
                    }
                }
                
                ed.WriteMessage("\n========================================");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
            }
        }
    }
}


























