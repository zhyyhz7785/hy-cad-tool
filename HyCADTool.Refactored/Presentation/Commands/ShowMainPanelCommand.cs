using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.Views;
using System;
using System.Windows.Controls;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.ShowMainPanelCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 显示集成主面板命令（类似原项目的 hy 命令）
    /// 将所有子面板（钢筋、过滤器、基础钢筋、桩、聚类）集成在一个 PaletteSet 中
    /// 
    /// 面板 DI 策略：
    /// - ReinPanel / FilterPanel：通过 Autofac 容器解析（构造函数注入 ViewModel）
    /// - BaseReinPanel：暂用无参构造 + 手动解析 ViewModel（待 IBaseReinforcementService 实现后切换为 DI）
    /// - PilePanel / ClusterPanel：无参构造（内部自行管理旧项目依赖）
    /// </summary>
    public class ShowMainPanelCommand
    {
        private static PaletteSet _mainPalette;

        /// <summary>
        /// 显示集成主面板
        /// 命令: HYREFACTOR
        /// </summary>
        [CommandMethod("HYREFACTOR")]
        public static void ShowMainPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                if (_mainPalette == null)
                {
                    _mainPalette = new PaletteSet("HY 主面板 (重构版)")
                    {
                        Style = PaletteSetStyles.ShowAutoHideButton
                              | PaletteSetStyles.ShowCloseButton
                              | PaletteSetStyles.Snappable
                    };

                    int loaded = 0;

                    // 1. 钢筋面板 - 通过 DI 解析
                    loaded += TryAddPanel<ReinPanel>(_mainPalette, "钢筋", ed);

                    // 2. 过滤器面板 - 通过 DI 解析
                    loaded += TryAddPanel<FilterPanel>(_mainPalette, "过滤器", ed);

                    // 3. 基础钢筋面板 - 暂用无参构造（IBaseReinforcementService 尚未实现）
                    loaded += TryAddPanelDirect(() => new BaseReinPanel(), _mainPalette, "基础钢筋", ed);

                    // 4. 桩基面板 - 无参构造（内部管理旧项目依赖）
                    // 注意: PilePanel 依赖旧项目，若未在 csproj 中恢复编译则跳过
                    // loaded += TryAddPanelDirect(() => new PilePanel(), _mainPalette, "桩", ed);

                    // 5. 聚类面板 - 无参构造（内部管理旧项目依赖）
                    // 注意: ClusterPanel 依赖旧项目，若未在 csproj 中恢复编译则跳过
                    // loaded += TryAddPanelDirect(() => new ClusterPanel(), _mainPalette, "螺栓聚类与标注", ed);

                    ed.WriteMessage($"\nHY 主面板创建完成：{loaded}/3 个面板已加载（钢筋、过滤器、基础钢筋）");
                }

                _mainPalette.Visible = true;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n显示主面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 隐藏主面板
        /// 命令: HYHIDE
        /// </summary>
        [CommandMethod("HYHIDE")]
        public static void HideMainPanel()
        {
            if (_mainPalette != null)
                _mainPalette.Visible = false;
        }

        /// <summary>
        /// 切换主面板显示状态
        /// 命令: HYTOGGLE
        /// </summary>
        [CommandMethod("HYTOGGLE")]
        public static void ToggleMainPanel()
        {
            if (_mainPalette == null)
                ShowMainPanel();
            else
                _mainPalette.Visible = !_mainPalette.Visible;
        }

        #region 辅助方法

        /// <summary>
        /// 通过 DI 容器解析面板并添加到 PaletteSet
        /// </summary>
        private static int TryAddPanel<T>(PaletteSet palette, string tabName, Autodesk.AutoCAD.EditorInput.Editor ed) where T : UserControl
        {
            try
            {
                if (ServiceLocator.Container == null)
                {
                    ed.WriteMessage($"\n  {tabName}: DI 容器未初始化");
                    return 0;
                }

                var panel = ServiceLocator.Container.Resolve<T>();
                palette.AddVisual(tabName, panel);
                return 1;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n  {tabName} 加载失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 直接创建面板（不通过 DI）并添加到 PaletteSet
        /// 用于依赖旧项目或 DI 尚未就绪的面板
        /// </summary>
        private static int TryAddPanelDirect(Func<UserControl> factory, PaletteSet palette, string tabName, Autodesk.AutoCAD.EditorInput.Editor ed)
        {
            try
            {
                var panel = factory();
                palette.AddVisual(tabName, panel);
                return 1;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n  {tabName} 加载失败: {ex.Message}");
                return 0;
            }
        }

        #endregion
    }
}

