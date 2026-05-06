using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace HyCAD.BlenderUI.Theming
{
    /// <summary>
    /// Blender 风格主题切换中心（v3 — Mutable Brush Facade）。
    ///
    /// ─────────────────────────────────────────────────────────────────────────
    ///  设计灵感与历史
    /// ─────────────────────────────────────────────────────────────────────────
    /// Blender 4.x 主题切换的本质是：所有 UI 颜色引用同一个全局可变对象
    /// （<c>bpy.context.preferences.themes[0]</c>），运行时修改该对象的字段
    /// 即触发即时模式重绘 → 所有面板瞬间换肤，无需任何"通知传播"。
    ///
    /// WPF 是保留模式 GUI，无即时重绘，但 <see cref="DependencyProperty"/>
    /// 系统提供了等价的"对象属性改变即广播"语义。本类利用此机制：
    ///
    /// 1. 应用首次加载时，每个 <see cref="Themes.ColorsHost"/>（即 Colors.xaml
    ///    实例）通过 <see cref="PopulateAndRegister"/> 把一组 *unfrozen*
    ///    <see cref="SolidColorBrush"/> 实例写入自身字典；
    /// 2. 任何 UI 控件 <c>{DynamicResource Brush_xxx}</c> 解析后绑定到这些
    ///    brush 实例；
    /// 3. <see cref="Apply"/> 切换时遍历所有 host 中的 brush 实例，仅修改
    ///    <see cref="SolidColorBrush.Color"/> —— Color 是 DP，变化通知自动
    ///    沿绑定链传播，所有引用方必然刷新。
    ///
    /// ─────────────────────────────────────────────────────────────────────────
    ///  v1 / v2 失败回顾（保留以避免重蹈覆辙）
    /// ─────────────────────────────────────────────────────────────────────────
    ///
    /// v1（修改 Application.Current.Resources）：失败。WPF DynamicResource 在
    /// UserControl 本地 Merge 链就找到 brush，永不冒泡到 Application 顶层。
    ///
    /// v2（host-replace：每个 ColorsHost 的 MergedDictionaries 整体替换）：失败。
    /// host 字典层颜色已正确更新（日志证明），但 grand-parent 字典持有的
    /// host 引用没变，DynamicResource 引用方未收到"已找到的资源已变"的通知。
    ///
    /// v2.5（host self-key overlay：把 newPalette 的 keys 直接写入 host 自身）：
    /// 仍失败。日志证明 <c>host[key]</c> 已 set 为新 brush，但 UI 没刷新。
    /// 根因：通过 <c>&lt;ResourceDictionary Source="..."/&gt;</c> 加载的字典
    /// 在 WPF 内部被视为不可变；事后修改其 entry 不广播 ResourcesChanged。
    ///
    /// v3（本实现 — brush facade）：成功。绕开 ResourceDictionary 的通知问题，
    /// 转用 SolidColorBrush.Color DP 的天然通知机制。
    /// </summary>
    public static class BlenderThemeManager
    {
        /// <summary>5 个内置主题。和 SettingsPanelViewModel.Theme 字符串值一一对应。</summary>
        public enum BlenderThemeName
        {
            BlenderDark = 0,
            BlenderLight = 1,
            AcadLight = 2,
            AcadDark = 3,
            AcadBlue = 4,
        }

        private static readonly Dictionary<BlenderThemeName, Uri> _paletteUris =
            new Dictionary<BlenderThemeName, Uri>
            {
                { BlenderThemeName.BlenderDark,  new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderDark.xaml",  UriKind.Absolute) },
                { BlenderThemeName.BlenderLight, new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderLight.xaml", UriKind.Absolute) },
                { BlenderThemeName.AcadLight,    new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/AcadLight.xaml",    UriKind.Absolute) },
                { BlenderThemeName.AcadDark,     new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/AcadDark.xaml",     UriKind.Absolute) },
                { BlenderThemeName.AcadBlue,     new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/AcadBlue.xaml",     UriKind.Absolute) },
            };

        /// <summary>主题→颜色映射缓存。第一次读取主题时从 XAML 解析，之后复用。</summary>
        private static readonly Dictionary<BlenderThemeName, Dictionary<string, Color>> _paletteCache =
            new Dictionary<BlenderThemeName, Dictionary<string, Color>>();

        /// <summary>
        /// 已注册的 brush facade host —— 每条记录持有一个 ColorsHost 实例上承载的全部 Brush_* 实例。
        ///
        /// 【H10 修复】此前用 WeakReference&lt;ResourceDictionary&gt; 跟踪 host，但日志证明
        /// 38 个 ColorsHost 实例的 TryGetTarget 在 1ms 内全部返回 false（与控件树持有矛盾，
        /// GC 行为不可预期）。现改为直接 strong reference Brushes 字典 —— 我们只需 brush 实例
        /// 还活着即可：WPF DynamicResource 解析后控件 DP 系统会强引用 brush，
        /// 因此 brushes 与 UI 寿命一致；strong ref 避免 brush 被错误判定为死。
        /// 每 host ≈ 37 个 SolidColorBrush ≈ 1.5KB，永驻 _hosts 列表的内存代价完全可接受。
        /// </summary>
        private sealed class HostEntry
        {
            public Dictionary<string, SolidColorBrush> Brushes;
        }

        private static readonly List<HostEntry> _hosts = new List<HostEntry>();
        private static readonly object _sync = new object();

        /// <summary>当前应用中的主题。未调用过 Apply 时为 BlenderDark（即编译期默认）。</summary>
        public static BlenderThemeName Current { get; private set; } = BlenderThemeName.BlenderDark;

        /// <summary>主题变更广播。订阅方可在此刷新自定义不走 DynamicResource 的视觉。</summary>
        public static event EventHandler<BlenderThemeName> ThemeChanged;

        /// <summary>
        /// 解析字符串到主题枚举。空 / 未知 → BlenderDark。
        /// 支持的字符串：枚举名（"BlenderDark" / "BlenderLight" / "AcadLight" / "AcadDark"）
        /// 或简写（"Dark" / "Light"）。大小写不敏感。
        /// </summary>
        public static BlenderThemeName Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return BlenderThemeName.BlenderDark;

            switch (text.Trim().ToLowerInvariant())
            {
                case "blenderdark":
                case "dark":
                case "0":
                    return BlenderThemeName.BlenderDark;
                case "blenderlight":
                case "light":
                case "1":
                    return BlenderThemeName.BlenderLight;
                case "acadlight":
                case "autocadlight":
                case "2":
                    return BlenderThemeName.AcadLight;
                case "acaddark":
                case "autocaddark":
                case "3":
                    return BlenderThemeName.AcadDark;
                case "acadblue":
                case "autocadblue":
                case "blue":
                case "4":
                    return BlenderThemeName.AcadBlue;
                default:
                    return BlenderThemeName.BlenderDark;
            }
        }

        // ====================================================================
        //  Public API
        // ====================================================================

        /// <summary>
        /// 由 <see cref="Themes.ColorsHost"/> 构造时调用。
        ///
        /// 行为：
        /// 1. 加载 <see cref="Current"/> 主题的颜色表；
        /// 2. 为每个 Brush_* key 创建一个 *unfrozen* <see cref="SolidColorBrush"/> 实例；
        /// 3. 写入 host 自身字典（<c>host[key] = brush</c>）；
        /// 4. 把 host + brush 实例集合记入内部表，供 <see cref="Apply"/> 后续修改 .Color。
        /// </summary>
        internal static void PopulateAndRegister(ResourceDictionary host)
        {
            if (host == null) return;

            var palette = LoadPaletteColors(Current);
            var brushes = new Dictionary<string, SolidColorBrush>(palette.Count);

            foreach (var kv in palette)
            {
                var brush = new SolidColorBrush(kv.Value);

                // 【根因修复 H-A-confirmed】WPF 的 ResourceDictionary 在 add 一个 Freezable
                // 资源时，会调 StyleHelper.SealIfSealable(value) → 当 dict 被 marked
                // `IsThemeDictionary` / `_ownerApps != null` / `IsReadOnly`（任何通过
                // `<ResourceDictionary Source="..."/>` 加载的字典都满足这些条件之一），
                // 内部会 `if (sealable.CanSeal) sealable.Seal();` 强行把 brush freeze。
                //
                // 一旦 brush 被 freeze，v3 设计核心 "brush.Color = newColor 触发 DP 通知"
                // 全部抛 InvalidOperationException 失败，主题切换无效，且累积异常会污染
                // AutoCAD 自家 Ribbon Badge 等控件渲染 → 升级 e0434352 native fatal。
                //
                // 绕过办法：让 brush.CanFreeze 返回 false。Freezable.CanFreeze 在 brush
                // 持有任何 binding/animation/dynamic resource expression 时为 false，
                // SealIfSealable 的 `if (CanSeal)` 条件 short-circuit，brush 保持 unfrozen。
                // 这里给 OpacityProperty 加一个 self-binding（Opacity 默认 1.0，binding 不
                // 改变值），仅作 "freeze blocker"。后续 Apply() 改的是 ColorProperty，
                // 不与 OpacityProperty 的 binding 冲突。
                BindingOperations.SetBinding(brush, SolidColorBrush.OpacityProperty,
                    new Binding(".") { Source = 1.0, Mode = BindingMode.OneWay });

                host[kv.Key] = brush;
                brushes[kv.Key] = brush;
            }

            lock (_sync)
            {
                _hosts.Add(new HostEntry { Brushes = brushes });
            }
        }

        /// <summary>
        /// 应用主题。多次调用幂等：相同主题直接返回。
        /// 自动切换到 UI 线程执行（SolidColorBrush.Color 修改必须在创建该对象的线程，
        /// 通常即 UI 线程）。
        /// </summary>
        public static void Apply(BlenderThemeName name)
        {
            if (Current == name) return;

            Current = name;

            var app = Application.Current;
            if (app != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() => ApplyToAllHostsCore(name)),
                                           DispatcherPriority.Normal);
            }
            else
            {
                ApplyToAllHostsCore(name);
            }

            try { ThemeChanged?.Invoke(null, name); } catch { /* 订阅方异常不能影响主题切换 */ }
        }

        /// <summary>字符串重载，便于从配置文件直接驱动。</summary>
        public static void Apply(string themeName) => Apply(Parse(themeName));

        /// <summary>
        /// 强制按当前主题刷新所有 host —— 启动兜底用：在文档资源初始化完成 / 主题字符串首次确定后
        /// 调一次，避免"用户从未点过主题选项"时 host 一直停留在编译期默认 BlenderDark。
        /// </summary>
        public static void Refresh() => ApplyToAllHostsCore(Current);

        // ====================================================================
        //  内部：palette 读取与 host 遍历
        // ====================================================================

        /// <summary>
        /// 解析 palette XAML，提取每个 Brush_* key 的 Color 字面值。
        /// XAML 仅作为颜色字面值的 source of truth，不再用于持有 brush 实例。
        /// </summary>
        private static Dictionary<string, Color> LoadPaletteColors(BlenderThemeName name)
        {
            lock (_sync)
            {
                if (_paletteCache.TryGetValue(name, out var cached)) return cached;
            }

            var result = new Dictionary<string, Color>(64);
            try
            {
                if (_paletteUris.TryGetValue(name, out var uri))
                {
                    var dict = new ResourceDictionary { Source = uri };
                    foreach (var key in dict.Keys)
                    {
                        var keyStr = key as string;
                        if (string.IsNullOrEmpty(keyStr)) continue;
                        if (dict[key] is SolidColorBrush scb)
                        {
                            result[keyStr] = scb.Color;
                        }
                    }
                }
            }
            catch
            {
                // 调色板加载失败时返回空字典；调用方对 missing key 已有兜底（不更新该 brush）。
            }

            lock (_sync)
            {
                _paletteCache[name] = result;
            }
            return result;
        }

        private static void ApplyToAllHostsCore(BlenderThemeName name)
        {
            var palette = LoadPaletteColors(name);

            // 【H10 修复】不再 TryGetTarget pruning（WeakReference 已移除）。直接拍 _hosts 快照
            // 后枚举所有 brushes —— brushes 与控件 DP 同寿命，永远有效。
            List<HostEntry> snapshot;
            lock (_sync)
            {
                snapshot = new List<HostEntry>(_hosts);
            }

            foreach (var entry in snapshot)
            {
                foreach (var bk in entry.Brushes)
                {
                    if (palette.TryGetValue(bk.Key, out var newColor))
                    {
                        try
                        {
                            // 关键：只修改 Color 属性，不替换 brush 实例。
                            // 由于 SolidColorBrush.Color 是 DP，会自动通知所有 binding。
                            // PopulateAndRegister 已经给 OpacityProperty 挂 self-binding，
                            // brush 不会被 ResourceDictionary.Seal 强冻，这里 set Color 必成功。
                            bk.Value.Color = newColor;
                        }
                        catch
                        {
                            // 兜底：理论上不该走到这里。若极端场景 brush 仍被 freeze，
                            // 单个 brush 异常不阻塞其他 brush 的更新。
                        }
                    }
                }
            }
        }
    }
}
