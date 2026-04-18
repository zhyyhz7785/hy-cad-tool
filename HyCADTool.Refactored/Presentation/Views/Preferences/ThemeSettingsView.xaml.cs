using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views.Preferences
{
    /// <summary>
    /// 「界面 → 主题」二级分组视图。
    ///
    /// 与其他 Preferences 子视图一致：被实例化在 HyPreferencesView 的 ContentControl 模板里，
    /// 外层 DataContext 是 SettingsGroupVm，需要主动向上找 HySettingsViewModel 才能让
    /// {Binding Settings.Theme} 解析到真正的 SettingsPanelViewModel。
    /// </summary>
    public partial class ThemeSettingsView : UserControl
    {
        public ThemeSettingsView()
        {
            InitializeComponent();

            // #region agent log
            _DbgLog2("F", "ThemeSettingsView.ctor",
                "ctor entered",
                "{\"hash\":" + this.GetHashCode() + "}");
            // #endregion

            Loaded += (_, __) =>
            {
                RebindRootDataContext();
                // #region agent log
                _DbgLog2("F,I", "ThemeSettingsView.Loaded",
                    "Loaded fired",
                    "{\"dcType\":\"" + (DataContext == null ? "<null>" : DataContext.GetType().FullName) + "\"," +
                    "\"hash\":" + this.GetHashCode() + "}");
                HookListBoxIfReady();
                // #endregion
            };
            DataContextChanged += (_, __) => RebindRootDataContext();
        }

        // #region agent log
        private bool _hooked;
        private void HookListBoxIfReady()
        {
            if (_hooked) return;
            var lb = this.FindName("ThemeList") as System.Windows.Controls.ListBox;
            _DbgLog2("G,H", "ThemeSettingsView.HookListBox",
                "find ThemeList",
                "{\"found\":" + (lb != null ? "true" : "false") + "}");
            if (lb == null) return;

            _hooked = true;

            // ===== Fix (post H-J confirmed): code-behind 双向同步，绕开 sticky PathError binding =====
            try
            {
                var current = SettingsPanelViewModel.Current;
                if (current != null && !string.IsNullOrEmpty(current.Theme))
                {
                    lb.SelectedValue = current.Theme;
                }
                // #region agent log
                _DbgLog2("J-fix", "ThemeSettingsView.HookListBox",
                    "initial pull from Current.Theme",
                    "{\"currentNotNull\":" + (current != null ? "true" : "false") +
                    ",\"theme\":\"" + (current == null ? "<null>" : current.Theme ?? "<null>") +
                    "\",\"selValueAfterPull\":\"" + (lb.SelectedValue ?? "<null>") + "\"}");
                // #endregion
            }
            catch (System.Exception ex)
            {
                _DbgLog2("J-fix", "ThemeSettingsView.HookListBox",
                    "initial pull failed",
                    "{\"err\":\"" + ex.GetType().Name + ": " + ex.Message.Replace("\"", "'") + "\"}");
            }
            // ===== /Fix =====

            lb.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _DbgLog2("H", "ThemeList.PreviewMouseLeftButtonDown",
                    "mouse arrived",
                    "{\"src\":\"" + (e.OriginalSource == null ? "<null>" : e.OriginalSource.GetType().Name) + "\"}");
            };
            lb.SelectionChanged += (s, e) =>
            {
                // ===== Fix: push 选中值到 SettingsPanelViewModel.Current.Theme =====
                try
                {
                    var pushedKey = (lb.SelectedValue as string) ?? string.Empty;
                    if (!string.IsNullOrEmpty(pushedKey))
                    {
                        var current = SettingsPanelViewModel.Current;
                        if (current != null && !string.Equals(current.Theme, pushedKey, System.StringComparison.OrdinalIgnoreCase))
                        {
                            current.Theme = pushedKey;
                            // #region agent log
                            _DbgLog2("J-fix", "ThemeList.SelectionChanged.PushBack",
                                "push to Current.Theme",
                                "{\"pushedKey\":\"" + pushedKey + "\",\"after\":\"" + current.Theme + "\"}");
                            // #endregion
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _DbgLog2("J-fix", "ThemeList.SelectionChanged.PushBack",
                        "push failed",
                        "{\"err\":\"" + ex.GetType().Name + ": " + ex.Message.Replace("\"", "'") + "\"}");
                }
                // ===== /Fix =====


                string oldKey = "<none>";
                string newKey = "<none>";
                try
                {
                    if (e.RemovedItems != null && e.RemovedItems.Count > 0)
                    {
                        var v = e.RemovedItems[0] as ThemePresetVm; if (v != null) oldKey = v.Key;
                    }
                    if (e.AddedItems != null && e.AddedItems.Count > 0)
                    {
                        var v = e.AddedItems[0] as ThemePresetVm; if (v != null) newKey = v.Key;
                    }
                }
                catch { }

                // ----- binding diagnostics on SelectedValue -----
                string bxStatus = "<no-bx>";
                string bxHasErr = "<no-bx>";
                string bxDataItem = "<no-bx>";
                string bxResolvedSrc = "<no-bx>";
                string bxResolvedPath = "<no-bx>";
                string bxErrors = "<no-bx>";
                string settingsThemeNow = "<no-settings>";
                string settingsRefHash = "<no-settings>";
                try
                {
                    var bx = lb.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty);
                    if (bx != null)
                    {
                        bxStatus = bx.Status.ToString();
                        bxHasErr = bx.HasError ? "true" : "false";
                        bxDataItem = bx.DataItem == null ? "<null>" : bx.DataItem.GetType().FullName;
                        bxResolvedSrc = bx.ResolvedSource == null ? "<null>" : bx.ResolvedSource.GetType().FullName;
                        bxResolvedPath = bx.ResolvedSourcePropertyName ?? "<null>";
                        if (bx.HasError && bx.ValidationErrors != null)
                        {
                            var sb = new System.Text.StringBuilder();
                            foreach (var er in bx.ValidationErrors)
                            {
                                sb.Append(er == null ? "<null>" : er.ErrorContent?.ToString() ?? "<no-content>");
                                sb.Append("|");
                            }
                            bxErrors = sb.ToString();
                        }
                        else
                        {
                            bxErrors = "<no-validation-errors>";
                        }
                    }

                    var hsv = DataContext as HySettingsViewModel;
                    if (hsv != null && hsv.Settings != null)
                    {
                        settingsThemeNow = hsv.Settings.Theme ?? "<null>";
                        settingsRefHash = hsv.Settings.GetHashCode().ToString();
                    }
                }
                catch (System.Exception ex)
                {
                    bxErrors = "DIAG-EX: " + ex.GetType().Name + ":" + ex.Message;
                }

                _DbgLog2("J,K,L", "ThemeList.SelectionChanged",
                    "selection changed",
                    "{\"oldKey\":\"" + oldKey + "\",\"newKey\":\"" + newKey +
                    "\",\"selValue\":\"" + (lb.SelectedValue ?? "<null>") +
                    "\",\"dcType\":\"" + (DataContext == null ? "<null>" : DataContext.GetType().FullName) +
                    "\",\"bxStatus\":\"" + bxStatus +
                    "\",\"bxHasErr\":\"" + bxHasErr +
                    "\",\"bxDataItem\":\"" + bxDataItem +
                    "\",\"bxResolvedSrc\":\"" + bxResolvedSrc +
                    "\",\"bxResolvedPath\":\"" + bxResolvedPath +
                    "\",\"bxErrors\":\"" + (bxErrors ?? "<null>").Replace("\"","'").Replace("\\","/") +
                    "\",\"settingsThemeNow\":\"" + settingsThemeNow +
                    "\",\"settingsRefHash\":\"" + settingsRefHash + "\"}");
            };
        }

        private static void _DbgLog2(string hyp, string loc, string msg, string dataJson)
        {
            try
            {
                System.IO.File.AppendAllText(
                    @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-b2db6c.log",
                    "{\"sessionId\":\"b2db6c\",\"hypothesisId\":\"" + hyp +
                    "\",\"location\":\"" + loc + "\",\"message\":\"" + msg + "\",\"data\":" + dataJson +
                    ",\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n");
            }
            catch { }
        }
        // #endregion

        private void RebindRootDataContext()
        {
            var root = ResolveSettingsRootVm();
            // #region agent log
            var hsv = root as HySettingsViewModel;
            _DbgLog2("F", "ThemeSettingsView.RebindRootDataContext",
                "resolve result",
                "{\"rootType\":\"" + (root == null ? "<null>" : root.GetType().FullName) +
                "\",\"prevDcType\":\"" + (DataContext == null ? "<null>" : DataContext.GetType().FullName) +
                "\",\"settingsNotNull\":" + (hsv != null && hsv.Settings != null ? "true" : "false") +
                "\",\"settingsTheme\":\"" + (hsv != null && hsv.Settings != null ? hsv.Settings.Theme : "<n/a>") + "\"}");
            // #endregion
            if (root != null && !ReferenceEquals(DataContext, root))
            {
                DataContext = root;
            }
        }

        /// <summary>沿可视/逻辑树向上查找承载 HySettingsViewModel 的 DataContext。</summary>
        private object ResolveSettingsRootVm()
        {
            DependencyObject node = this;
            while (node != null)
            {
                if (node is FrameworkElement fe && fe.DataContext is HySettingsViewModel root)
                    return root;
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
            }
            return null;
        }
    }

    /// <summary>
    /// 主题预览卡片数据。在 ThemeSettingsView.xaml 内 inline 实例化为 4 项，
    /// 作为 ListBox.ItemsSource。Key 必须与 BlenderThemeManager.BlenderThemeName 枚举名一致。
    /// 4 个 Brush 对应 ItemTemplate 上的 4 个色块预览（见 XAML 注释）。
    /// </summary>
    public class ThemePresetVm
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public Brush C1 { get; set; }
        public Brush C2 { get; set; }
        public Brush C3 { get; set; }
        public Brush C4 { get; set; }
    }
}
