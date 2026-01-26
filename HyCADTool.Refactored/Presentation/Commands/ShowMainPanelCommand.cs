using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.Views;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.ShowMainPanelCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 显示集成主面板命令（类似原项目的 hy 命令）
    /// 将所有子面板（钢筋、过滤器、基础钢筋、桩、聚类）集成在一个 PaletteSet 中
    /// </summary>
    public class ShowMainPanelCommand
    {
        private static PaletteSet _mainPalette; // 静态实例，确保单例

        /// <summary>
        /// 显示集成主面板
        /// 命令: HYREFACTOR
        /// </summary>
        [CommandMethod("HYREFACTOR")]
        public static void ShowMainPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (_mainPalette == null)
                {
                    ed.WriteMessage("\n正在创建 HY 主面板...\n");
                    
                    _mainPalette = new PaletteSet("HY 主面板 (重构版)")
                    {
                        Style = PaletteSetStyles.ShowAutoHideButton 
                              | PaletteSetStyles.ShowCloseButton 
                              | PaletteSetStyles.Snappable
                    };

                    // 添加各个子面板（使用 AddVisual 自动创建 ElementHost）
                    try
                    {
                        _mainPalette.AddVisual("钢筋", new ReinPanel());
                        ed.WriteMessage("✅ 钢筋面板已加载\n");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"❌ 钢筋面板加载失败: {ex.Message}\n");
                    }

                    try
                    {
                        var filterPanel = new FilterPanel();
                        if (filterPanel.DataContext == null && ServiceLocator.Container != null)
                        {
                            filterPanel.DataContext = ServiceLocator.Container.Resolve<HyCADTool.Refactored.Presentation.ViewModels.FilterPanelViewModel>();
                        }
                        _mainPalette.AddVisual("过滤器", filterPanel);
                        ed.WriteMessage("✅ 过滤器面板已加载\n");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"❌ 过滤器面板加载失败: {ex.Message}\n");
                    }

                    try
                    {
                        var baseReinPanel = new BaseReinPanel();
                        if (baseReinPanel.DataContext == null && ServiceLocator.Container != null)
                        {
                            baseReinPanel.DataContext = ServiceLocator.Container.Resolve<HyCADTool.Refactored.Presentation.ViewModels.BaseReinPanelViewModel>();
                        }
                        _mainPalette.AddVisual("基础钢筋", baseReinPanel);
                        ed.WriteMessage("✅ 基础钢筋面板已加载\n");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"❌ 基础钢筋面板加载失败: {ex.Message}\n");
                    }

                    try
                    {
                        var pilePanel = new PilePanel();
                        _mainPalette.AddVisual("桩", pilePanel);
                        ed.WriteMessage("✅ 桩基布置面板已加载\n");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"❌ 桩基布置面板加载失败: {ex.Message}\n");
                    }

                    try
                    {
                        var clusterPanel = new ClusterPanel();
                        _mainPalette.AddVisual("螺栓聚类与标注", clusterPanel);
                        ed.WriteMessage("✅ 聚类分析面板已加载\n");
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"❌ 聚类分析面板加载失败: {ex.Message}\n");
                    }

                    ed.WriteMessage("\n✅ HY 主面板创建完成\n");
                }

                _mainPalette.Visible = true;
                ed.WriteMessage("✅ HY 主面板已显示（共 5 个子面板）\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示主面板失败: {ex.Message}\n");
                ed.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 隐藏主面板
        /// 命令: HYHIDE
        /// </summary>
        [CommandMethod("HYHIDE")]
        public static void HideMainPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (_mainPalette != null)
                {
                    _mainPalette.Visible = false;
                    ed.WriteMessage("\n✅ HY 主面板已隐藏\n");
                }
                else
                {
                    ed.WriteMessage("\n⚠️ 主面板尚未创建\n");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 隐藏主面板失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 切换主面板显示状态
        /// 命令: HYTOGGLE
        /// </summary>
        [CommandMethod("HYTOGGLE")]
        public static void ToggleMainPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (_mainPalette == null)
                {
                    ShowMainPanel();
                }
                else
                {
                    _mainPalette.Visible = !_mainPalette.Visible;
                    ed.WriteMessage(_mainPalette.Visible 
                        ? "\n✅ HY 主面板已显示\n" 
                        : "\n✅ HY 主面板已隐藏\n");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 切换主面板失败: {ex.Message}\n");
            }
        }
    }
}

