using System;
using System.Windows;
using System.Windows.Input;
using HyCAD.BlenderUI.WM.Dispatch;
using HyCAD.BlenderUI.WM.KeyMap;
using HyCAD.BlenderUI.WM.Operators;

namespace HyCADTool.Presentation.Input
{
    /// <summary>
    /// hy 面板键位映射与 Input 路由封装：
    /// - 注册 3 个 UI 层 Operator（聚焦搜索 / 清空搜索 / 切换设置）；
    /// - 用 <see cref="BlenderInputRouter"/> 把 PreviewKeyDown 桥到 Operator；
    /// - 对 HyBlenderPanel 的实例绑定：<see cref="Attach"/> / <see cref="Detach"/>。
    /// 约束：
    /// - Operator Id 全局唯一，用 "hy.ui.*" 命名空间避免和未来命令 Operator 冲突；
    /// - Poll 会检查委托非空，避免残留注册在面板卸载后触发 NRE；
    /// - Operator 返回 Finished 会令 e.Handled=true，避免 TextBox 内重复处理。
    /// </summary>
    public static class HyKeyMap
    {
        /// <summary>聚焦搜索框（Ctrl+F）。</summary>
        public const string OpFocusSearch = "hy.ui.focus_search";

        /// <summary>清空搜索（Esc）。</summary>
        public const string OpClearSearch = "hy.ui.clear_search";

        /// <summary>切换设置 Tab（Ctrl+E）。</summary>
        public const string OpTogglePreferences = "hy.ui.toggle_preferences";

        private static bool _operatorsRegistered;

        private static readonly object _sync = new object();

        /// <summary>进程级单次 Operator 注册（幂等）。</summary>
        private static void EnsureOperatorsRegistered()
        {
            if (_operatorsRegistered) return;
            lock (_sync)
            {
                if (_operatorsRegistered) return;
                OperatorRegistry.Register(OpFocusSearch, () => new DelegateOperator(OpFocusSearch));
                OperatorRegistry.Register(OpClearSearch, () => new DelegateOperator(OpClearSearch));
                OperatorRegistry.Register(OpTogglePreferences, () => new DelegateOperator(OpTogglePreferences));
                _operatorsRegistered = true;
            }
        }

        /// <summary>给 hy 面板实例附加一份独立的 KeyMap 路由。返回 detach 句柄。</summary>
        public static IDisposable Attach(UIElement root, HyKeyMapBindings bindings)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            EnsureOperatorsRegistered();

            // 每个面板各自一个 router + 自己的 bindings 容器（避免跨面板串线）
            var router = new BlenderInputRouter();
            router.KeyMap = BuildDefaultKeyMap();

            var handler = new PreviewKeyDownBridge(router, bindings);
            root.PreviewKeyDown += handler.Handle;

            return new Detach(() =>
            {
                root.PreviewKeyDown -= handler.Handle;
            });
        }

        /// <summary>
        /// 构建 KeyMap：优先从 hy-keymap.json 加载；若无、无效或 Items 为空，退回内置默认 3 键。
        /// 加载时对 Key/Modifiers 做容错解析；OperatorId 未知也保留（路由端找不到就静默忽略）。
        /// </summary>
        private static KeyMapConfig BuildDefaultKeyMap()
        {
            var data = KeyMapConfigLoader.Load();
            if (data != null && data.Items != null && data.Items.Count > 0)
            {
                var loaded = new KeyMapConfig { Name = string.IsNullOrWhiteSpace(data.Name) ? "HyUI" : data.Name };
                foreach (var it in data.Items)
                {
                    if (string.IsNullOrWhiteSpace(it.OperatorId)) continue;
                    var key = KeyMapConfigLoader.ParseKey(it.Key);
                    if (key == Key.None) continue;
                    loaded.Items.Add(new KeyMapItem
                    {
                        Key = key,
                        Modifiers = KeyMapConfigLoader.ParseModifiers(it.Modifiers),
                        OperatorId = it.OperatorId,
                    });
                }
                if (loaded.Items.Count > 0) return loaded;
            }

            var cfg = new KeyMapConfig { Name = "HyUI" };
            cfg.Items.Add(new KeyMapItem { Key = Key.F, Modifiers = ModifierKeys.Control, OperatorId = OpFocusSearch });
            cfg.Items.Add(new KeyMapItem { Key = Key.Escape, Modifiers = ModifierKeys.None, OperatorId = OpClearSearch });
            cfg.Items.Add(new KeyMapItem { Key = Key.E, Modifiers = ModifierKeys.Control, OperatorId = OpTogglePreferences });
            return cfg;
        }

        /// <summary>内置默认 3 键的可序列化副本，供 VM「恢复默认」使用。</summary>
        public static KeyMapConfigLoader.KeyMapData BuildBuiltInDefaults()
        {
            return new KeyMapConfigLoader.KeyMapData
            {
                Name = "HyUI",
                Items = new System.Collections.Generic.List<KeyMapConfigLoader.KeyMapEntry>
                {
                    new KeyMapConfigLoader.KeyMapEntry { Key = "F", Modifiers = "Control", OperatorId = OpFocusSearch },
                    new KeyMapConfigLoader.KeyMapEntry { Key = "Escape", Modifiers = "None", OperatorId = OpClearSearch },
                    new KeyMapConfigLoader.KeyMapEntry { Key = "E", Modifiers = "Control", OperatorId = OpTogglePreferences },
                },
            };
        }

        /// <summary>面板级回调集合：Operator 找不到实例时跳过（保证多面板隔离）。</summary>
        public sealed class HyKeyMapBindings
        {
            public Action FocusSearch { get; set; }
            public Action ClearSearch { get; set; }
            public Action TogglePreferences { get; set; }
        }

        /// <summary>
        /// 通过 Router → Registry 的链路在 Invoke 时没法拿到面板实例。
        /// 这里用 attached DP 式绕行：把 bindings 放在 UIElement.Tag 附近风险高，
        /// 换成在 PreviewKeyDown 外层接管，绕过 Registry 直接查表调用。
        /// </summary>
        private sealed class PreviewKeyDownBridge
        {
            private readonly BlenderInputRouter _router;
            private readonly HyKeyMapBindings _bindings;

            public PreviewKeyDownBridge(BlenderInputRouter router, HyKeyMapBindings bindings)
            {
                _router = router;
                _bindings = bindings;
            }

            public void Handle(object sender, KeyEventArgs e)
            {
                foreach (var item in _router.KeyMap.Items)
                {
                    if (item.Key != e.Key) continue;
                    if (item.Modifiers != Keyboard.Modifiers) continue;

                    Action act = null;
                    if (item.OperatorId == OpFocusSearch) act = _bindings.FocusSearch;
                    else if (item.OperatorId == OpClearSearch) act = _bindings.ClearSearch;
                    else if (item.OperatorId == OpTogglePreferences) act = _bindings.TogglePreferences;
                    if (act == null) continue;

                    act();
                    e.Handled = true;
                    return;
                }
            }
        }

        private sealed class DelegateOperator : Operator
        {
            private readonly string _id;
            public DelegateOperator(string id) { _id = id; }
            public override string Id => _id;
            public override OperatorResult Execute(OperatorContext ctx) => OperatorResult.Finished;
        }

        private sealed class Detach : IDisposable
        {
            private Action _action;
            public Detach(Action action) { _action = action; }
            public void Dispose()
            {
                var a = _action;
                _action = null;
                a?.Invoke();
            }
        }
    }
}
