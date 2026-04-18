using System;
using System.Linq;
using System.Windows.Controls;
using Autodesk.Windows;
using HyCADTool.ReCall;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.UI
{
    /// <summary>
    /// 向 AutoCAD 顶部 Ribbon 注入 HyCAD 选项卡（数据源 = commands.json 分组）。
    ///
    /// 每个 Category 对应一个 <see cref="RibbonPanel"/>，前 3 个 order 最小的命令渲染为大按钮
    /// （<see cref="RibbonItemSize.Large"/> + 垂直文字），其余命令以小按钮形式堆叠在右侧。
    ///
    /// Ribbon 是现代工作区入口；如果用户在经典工作区 / 关闭了 Ribbon（<c>RIBBONCLOSE</c>），
    /// ComponentManager.Ribbon == null，本 Builder 会静默跳过，CUIX 菜单栏作为后备入口。
    /// </summary>
    public static class HyCadRibbonBuilder
    {
        /// <summary>Ribbon 选项卡 Id，Build/Teardown 以它做幂等键。</summary>
        public const string TabId = "HYCAD_TAB";
        /// <summary>选项卡标题。</summary>
        public const string TabTitle = "HyCAD";

        /// <summary>
        /// 构建并挂载 Ribbon 选项卡。可重复调用（重入时先清理旧标签），C2 热重载友好。
        /// </summary>
        public static void Build()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            Teardown(ribbon);

            var tab = new RibbonTab
            {
                Title = TabTitle,
                Id = TabId,
            };

            try
            {
                var groups = CommandTable.GroupByCategory();
                foreach (var g in groups)
                {
                    var panel = BuildCategoryPanel(g);
                    if (panel != null) tab.Panels.Add(panel);
                }

                ribbon.Tabs.Add(tab);
            }
            catch (System.Exception)
            {
                // 构建异常：静默跳过（避免破坏 AutoCAD 启动流程），Blender 面板入口仍然可用。
            }
        }

        /// <summary>从 Ribbon 上移除 HyCAD 选项卡。PluginInitializer.Terminate / C2 重载前调用。</summary>
        public static void Teardown()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            Teardown(ribbon);
        }

        private static void Teardown(RibbonControl ribbon)
        {
            var existing = ribbon.Tabs.FirstOrDefault(t => t.Id == TabId);
            if (existing != null) ribbon.Tabs.Remove(existing);
        }

        private static RibbonPanel BuildCategoryPanel(CategoryGroup group)
        {
            if (group == null || group.Items == null || group.Items.Count == 0) return null;

            var src = new RibbonPanelSource { Title = group.Category };

            // 策略：前 3 个命令大按钮（一行 3 列），剩余命令小按钮 3 行 × N 列堆叠
            var largeItems = group.Items.Take(3).ToList();
            var smallItems = group.Items.Skip(3).ToList();

            var largeRow = new RibbonRowPanel();
            foreach (var cmd in largeItems)
            {
                largeRow.Items.Add(BuildRibbonButton(cmd, RibbonItemSize.Large));
            }
            if (largeRow.Items.Count > 0) src.Items.Add(largeRow);

            // 小按钮：每 3 个一列（Ribbon 常见布局）
            for (int i = 0; i < smallItems.Count; i += 3)
            {
                if (src.Items.Count > 0) src.Items.Add(new RibbonSeparator { SeparatorStyle = RibbonSeparatorStyle.Line });

                var column = new RibbonRowPanel();
                for (int k = i; k < Math.Min(i + 3, smallItems.Count); k++)
                {
                    if (k > i) column.Items.Add(new RibbonRowBreak());
                    column.Items.Add(BuildRibbonButton(smallItems[k], RibbonItemSize.Standard, showText: true));
                }
                src.Items.Add(column);
            }

            return new RibbonPanel { Source = src };
        }

        private static RibbonButton BuildRibbonButton(CommandListItem cmd, RibbonItemSize size, bool showText = true)
        {
            var btn = new RibbonButton
            {
                Text = cmd.DisplayName,
                ShowText = showText,
                ShowImage = false,
                Size = size,
                Orientation = size == RibbonItemSize.Large ? Orientation.Vertical : Orientation.Horizontal,
                Description = cmd.Tooltip,
                ToolTip = BuildTooltip(cmd),
                CommandHandler = new RibbonCommandHandler(cmd.Key),
            };
            return btn;
        }

        private static RibbonToolTip BuildTooltip(CommandListItem cmd)
        {
            return new RibbonToolTip
            {
                Title = cmd.DisplayName,
                Content = cmd.Tooltip,
                Command = cmd.Key,
                IsHelpEnabled = false,
            };
        }
    }
}
