using System;
using System.Linq;
using System.Windows.Controls;
using Autodesk.Windows;
using HyCADTool.Shell.Commands;

namespace HyCADTool.Shell.Ribbon
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
        /// 跨 C2 复用：进程级环境变量记录"上一次构建对应的 commands.json mtime ticks"。
        /// C2 后 Refactored 类型重建，但 AutoCAD 进程不变；环境变量随进程存活，
        /// 未来 C2 可凭此跳过 144+ 个 RibbonButton/RibbonToolTip 的 WPF 重建。
        /// </summary>
        private const string EnvLastInstalledMtimeTicks = "HYCAD_RIBBON_INSTALLED_MTIME_TICKS";
        /// <summary>关闭跨 C2 跳过策略（强制每次 C2 重挂 Ribbon）。默认 0。</summary>
        private const string EnvDisableSkip = "HYCAD_RIBBON_NO_SKIP";

        /// <summary>
        /// 自愈订阅句柄。Ribbon 在以下场景会被 AutoCAD 重建（旧 Tab 全部失效）：
        /// - 用户输 RIBBONCLOSE 后再开 RIBBON
        /// - 切换工作区（WSCURRENT）
        /// - 加载新的 cuix
        /// 监听 ComponentManager.ItemInitialized，发现 Ribbon 重建时自动重挂选项卡。
        /// </summary>
        private static EventHandler<RibbonItemEventArgs> _itemInitializedHandler;

        /// <summary>
        /// 构建并挂载 Ribbon 选项卡。可重复调用（重入时先清理旧标签），C2 热重载友好。
        /// 性能优化：commands.json mtime 未变 + Tab 已存在 → 立即返回（≈0ms）。
        /// </summary>
        public static void Build()
        {
            EnsureSelfHealSubscribed();

            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            if (CanSkipBecauseAlreadyInstalled(ribbon))
                return;

            BuildOnto(ribbon);
            RememberInstalledMtime();
        }

        /// <summary>当前 Tab 已挂载且 commands.json mtime 与上次记录一致 → C2 重挂可省略。</summary>
        private static bool CanSkipBecauseAlreadyInstalled(RibbonControl ribbon)
        {
            var disable = Environment.GetEnvironmentVariable(EnvDisableSkip);
            if (string.Equals(disable, "1", StringComparison.Ordinal)) return false;

            if (!ribbon.Tabs.Any(t => t.Id == TabId)) return false;

            var curMtime = CommandCatalog.GetFileMtimeUtc();
            if (!curMtime.HasValue) return true; // 没有 json mtime，沿用既有 Tab 即可

            var raw = Environment.GetEnvironmentVariable(EnvLastInstalledMtimeTicks);
            if (string.IsNullOrEmpty(raw)) return false;
            if (!long.TryParse(raw, out var ticks)) return false;
            return ticks == curMtime.Value.Ticks;
        }

        private static void RememberInstalledMtime()
        {
            var curMtime = CommandCatalog.GetFileMtimeUtc();
            if (!curMtime.HasValue) return;
            try
            {
                Environment.SetEnvironmentVariable(EnvLastInstalledMtimeTicks, curMtime.Value.Ticks.ToString());
            }
            catch { }
        }

        /// <summary>
        /// 从 Ribbon 上移除 HyCAD 选项卡，并解绑自愈事件。**仅 AutoCAD 进程退出**前调用；
        /// C2 热重载请改用 <see cref="UnsubscribeSelfHealOnly"/>，保留 Tab 以省下重挂开销。
        /// </summary>
        public static void Teardown()
        {
            UnsubscribeSelfHeal();

            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            Teardown(ribbon);

            try { Environment.SetEnvironmentVariable(EnvLastInstalledMtimeTicks, null); } catch { }
        }

        /// <summary>仅解绑自愈事件（避免 C2 累积 N 份 ItemInitialized handler），保留 Ribbon Tab 给下次 C2 直接复用。</summary>
        public static void UnsubscribeSelfHealOnly()
        {
            UnsubscribeSelfHeal();
        }

        private static void Teardown(RibbonControl ribbon)
        {
            var existing = ribbon.Tabs.FirstOrDefault(t => t.Id == TabId);
            if (existing != null) ribbon.Tabs.Remove(existing);
        }

        private static void BuildOnto(RibbonControl ribbon)
        {
            Teardown(ribbon);

            var tab = new RibbonTab
            {
                Title = TabTitle,
                Id = TabId,
            };

            try
            {
                var groups = CommandCatalog.GroupByCategory();
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

        private static void EnsureSelfHealSubscribed()
        {
            if (_itemInitializedHandler != null) return;

            // 事件只作触发器：Ribbon 任何子项初始化都尝试核对一次自家 Tab 是否还在。
            // RibbonItemEventArgs.Item 是 RibbonItem 基类，无法与 RibbonControl 直接比较，
            // 所以这里不依赖事件参数，统一由 Tabs.Any(...) 做幂等判断。
            _itemInitializedHandler = (sender, e) =>
            {
                try
                {
                    var ribbon = ComponentManager.Ribbon;
                    if (ribbon == null) return;
                    if (ribbon.Tabs.Any(t => t.Id == TabId)) return;
                    BuildOnto(ribbon);
                }
                catch
                {
                    // 自愈失败不应影响 AutoCAD 主循环。
                }
            };

            try { ComponentManager.ItemInitialized += _itemInitializedHandler; }
            catch { _itemInitializedHandler = null; }
        }

        private static void UnsubscribeSelfHeal()
        {
            if (_itemInitializedHandler == null) return;
            try { ComponentManager.ItemInitialized -= _itemInitializedHandler; } catch { }
            _itemInitializedHandler = null;
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
