using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace HyCAD.BlenderUI.Theming
{
    /// <summary>
    /// Blender 风格主题切换中心（v2 — host-replace 实现）。
    ///
    /// 背景与 v1 失败原因：
    /// WPF DynamicResource 解析优先本控件的 <c>Resources</c> 及其 MergedDictionaries 链，
    /// 命中即停。每个 UserControl 都 Merge 了 BlenderTheme.xaml → Colors.xaml → BlenderDark.xaml，
    /// Brush_* key 在本地 Merge 链就被命中，<b>永远不会</b>冒泡到 Application.Current.Resources。
    /// 所以 v1 那种"在 Application 顶层 Merge 调色板"的做法对面板毫无影响。
    ///
    /// v2 改为 host-replace：
    /// 1. <see cref="Themes.ColorsHost"/>（即 Themes/Colors.xaml 的代码后置类型）在构造时把自己
    ///    通过 <see cref="RegisterPaletteHost"/> 登记到本管理器；
    /// 2. <see cref="Apply"/> 切换时遍历所有已登记 host，<c>Clear</c> 其 MergedDictionaries 后
    ///    Add 新调色板字典 —— 这是 host 内部唯一一层 Merge，原 Brush_* key 全部由新字典提供，
    ///    DynamicResource 引用方收到 ResourceDictionaryChanged 通知后自动刷新。
    ///
    /// 设计要点：
    /// - WeakReference 防止 host 被永久强引用（UserControl 关闭/PaletteSet 销毁后允许回收）；
    /// - 切换在 UI 线程执行（DynamicResource 重新解析必须在 UI 线程）；
    /// - 即便 Application.Current 为 null（设计器 / 单元测试场景），也能完成 host 替换，
    ///   只是无法 Dispatcher.Invoke 时退化为同线程执行。
    /// </summary>
    public static class BlenderThemeManager
    {
        /// <summary>4 个内置主题。和 SettingsPanelViewModel.Theme 字符串值一一对应。</summary>
        public enum BlenderThemeName
        {
            BlenderDark = 0,
            BlenderLight = 1,
            AcadLight = 2,
            AcadDark = 3,
        }

        private static readonly Dictionary<BlenderThemeName, Uri> _paletteUris =
            new Dictionary<BlenderThemeName, Uri>
            {
                { BlenderThemeName.BlenderDark,  new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderDark.xaml",  UriKind.Absolute) },
                { BlenderThemeName.BlenderLight, new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderLight.xaml", UriKind.Absolute) },
                { BlenderThemeName.AcadLight,    new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/AcadLight.xaml",    UriKind.Absolute) },
                { BlenderThemeName.AcadDark,     new Uri("pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/AcadDark.xaml",     UriKind.Absolute) },
            };

        /// <summary>已注册的调色板 host（即 ColorsHost 实例）。WeakRef 避免泄漏。</summary>
        private static readonly List<WeakReference<ResourceDictionary>> _paletteHosts =
            new List<WeakReference<ResourceDictionary>>();

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
                default:
                    return BlenderThemeName.BlenderDark;
            }
        }

        /// <summary>
        /// 由 <see cref="Themes.ColorsHost"/> 构造时调用，登记自己以便后续 Apply 替换其 MergedDictionaries。
        /// 注册即触发一次"按当前主题刷新"，避免 host 在切换之后才创建仍显示默认 BlenderDark。
        /// </summary>
        internal static void RegisterPaletteHost(ResourceDictionary host)
        {
            if (host == null) return;

            int countAfter;
            lock (_sync)
            {
                _paletteHosts.Add(new WeakReference<ResourceDictionary>(host));
                countAfter = _paletteHosts.Count;
            }

            // #region agent log
            _DbgLog.W("A,B", "BlenderThemeManager.RegisterPaletteHost",
                "host registered",
                "{\"hostHash\":" + host.GetHashCode() +
                ",\"totalHosts\":" + countAfter +
                ",\"current\":\"" + Current + "\"}");
            // #endregion

            if (Current != BlenderThemeName.BlenderDark)
            {
                ReplaceHostPalette(host, Current);
            }
        }

        /// <summary>
        /// 应用主题。多次调用幂等：相同主题 + 已有 host 时直接返回。
        /// 自动切换到 UI 线程执行（host 替换会触发 DynamicResource 重新解析）。
        /// </summary>
        public static void Apply(BlenderThemeName name)
        {
            // #region agent log
            int hostCountSnap;
            lock (_sync) { hostCountSnap = _paletteHosts.Count; }
            _DbgLog.W("D", "BlenderThemeManager.Apply",
                "Apply enter",
                "{\"requested\":\"" + name +
                "\",\"current\":\"" + Current +
                "\",\"hostCount\":" + hostCountSnap +
                ",\"appNull\":" + (Application.Current == null ? "true" : "false") + "}");
            // #endregion

            if (Current == name)
            {
                // #region agent log
                _DbgLog.W("D", "BlenderThemeManager.Apply",
                    "Apply early-return: same theme",
                    "{\"name\":\"" + name + "\"}");
                // #endregion
                return;
            }

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

        // --------------------------------------------------------------------
        //  内部：host 遍历与单 host 替换
        // --------------------------------------------------------------------

        private static void ApplyToAllHostsCore(BlenderThemeName name)
        {
            List<ResourceDictionary> snapshot;
            lock (_sync)
            {
                snapshot = new List<ResourceDictionary>(_paletteHosts.Count);
                var dead = new List<WeakReference<ResourceDictionary>>();
                foreach (var wr in _paletteHosts)
                {
                    if (wr.TryGetTarget(out var host)) snapshot.Add(host);
                    else dead.Add(wr);
                }
                foreach (var d in dead) _paletteHosts.Remove(d);
            }

            foreach (var host in snapshot)
            {
                ReplaceHostPalette(host, name);
            }
        }

        private static void ReplaceHostPalette(ResourceDictionary host, BlenderThemeName name)
        {
            if (host == null) return;
            if (!_paletteUris.TryGetValue(name, out var uri)) return;

            ResourceDictionary newPalette;
            try { newPalette = new ResourceDictionary { Source = uri }; }
            catch (Exception ex)
            {
                // #region agent log
                _DbgLog.W("C", "BlenderThemeManager.ReplaceHostPalette",
                    "load palette failed",
                    "{\"uri\":\"" + uri + "\",\"err\":\"" + ex.GetType().Name + ": " + ex.Message.Replace("\"","'") + "\"}");
                // #endregion
                return;
            }

            int beforeCount = host.MergedDictionaries.Count;
            int beforeKeys = 0;
            try { foreach (var k in host.Keys) beforeKeys++; } catch { }

            try
            {
                host.MergedDictionaries.Clear();
                host.MergedDictionaries.Add(newPalette);

                int afterKeys = 0;
                try { foreach (var k in host.Keys) afterKeys++; } catch { }
                object probe = null;
                try { probe = host["Brush_WindowBack"]; } catch { }

                // #region agent log
                _DbgLog.W("C", "BlenderThemeManager.ReplaceHostPalette",
                    "replace done",
                    "{\"hostHash\":" + host.GetHashCode() +
                    ",\"theme\":\"" + name +
                    "\",\"mergedBefore\":" + beforeCount +
                    ",\"mergedAfter\":" + host.MergedDictionaries.Count +
                    ",\"keysBefore\":" + beforeKeys +
                    ",\"keysAfter\":" + afterKeys +
                    ",\"brushWinBackType\":\"" + (probe == null ? "<null>" : probe.GetType().Name) + "\"}");
                // #endregion
            }
            catch (Exception ex)
            {
                // #region agent log
                _DbgLog.W("C", "BlenderThemeManager.ReplaceHostPalette",
                    "replace failed",
                    "{\"err\":\"" + ex.GetType().Name + ": " + ex.Message.Replace("\"","'") + "\"}");
                // #endregion
            }
        }
    }
}
