using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Theming
{
    /// <summary>
    /// 将 hy 面板 <c>Metric_*</c> 按三类比例（字号 / 行高与间距 / 输入与列宽）从
    /// Metrics.xaml 基值派生并直接写入每个注册根元素的 <see cref="FrameworkElement.Resources"/>。
    ///
    /// 为什么写根 <c>Resources</c> 本体，而不是 <c>MergedDictionaries</c>：
    /// WPF 里同名 key 在 <c>MergedDictionaries</c> 中 <em>后入为大</em>（后合并的优先），
    /// 我们早先尝试 <c>Insert(0, overlay)</c> 会被真正包含 Metrics 的 BlenderTheme 字典覆盖；
    /// 而写入 <c>fe.Resources</c> 本体（不进 merged）天然优先级最高，且任何修改都会触发
    /// <c>ResourcesChanged</c>，让已缓存的 DynamicResource 重新解析到新值。
    /// </summary>
    public static class BlenderMetricsScaleManager
    {
        private static readonly object _sync = new object();
        private static readonly Dictionary<string, object> _baseline = new Dictionary<string, object>(StringComparer.Ordinal);
        private static readonly List<WeakReference<FrameworkElement>> _overlayRoots = new List<WeakReference<FrameworkElement>>();
        private static readonly ConditionalWeakTable<FrameworkElement, object> _rootsWithOverlay = new ConditionalWeakTable<FrameworkElement, object>();

        private static bool _baselineCaptured;
        private static double _fontScale = 1.0;
        private static double _densityScale = 1.0;
        private static double _inputWidthScale = 1.0;

        private static readonly Uri MetricsDictionaryUri = new Uri(
            "pack://application:,,,/HyCAD.BlenderUI;component/Themes/Metrics.xaml",
            UriKind.Absolute);

        public static double FontScale => _fontScale;
        public static double DensityScale => _densityScale;
        public static double InputWidthScale => _inputWidthScale;

        /// <summary>从 Metrics.xaml 快照基值（幂等）。</summary>
        public static void CaptureBaselineIfNeeded()
        {
            lock (_sync)
            {
                if (_baselineCaptured) return;
                try
                {
                    var dict = new ResourceDictionary { Source = MetricsDictionaryUri };
                    foreach (var keyObj in dict.Keys)
                    {
                        if (!(keyObj is string key) || !key.StartsWith("Metric_", StringComparison.Ordinal))
                            continue;
                        var val = dict[key];
                        if (val is double || val is Thickness || val is CornerRadius)
                            _baseline[key] = CloneMetricValue(val);
                    }
                    _baselineCaptured = true;
                }
                catch
                {
                    /* 设计器或过早：保持未捕获，下次再试 */
                }
            }
        }

        public static void Apply(double fontScale, double densityScale, double inputWidthScale)
        {
            CaptureBaselineIfNeeded();
            lock (_sync)
            {
                _fontScale = ClampScale(fontScale);
                _densityScale = ClampScale(densityScale);
                _inputWidthScale = ClampScale(inputWidthScale);
                RefreshAllRoots();
            }
        }

        /// <summary>登记根元素，并立刻写入当前派生值。</summary>
        public static void EnsureOverlayFirstMerged(FrameworkElement fe)
        {
            if (fe == null) return;
            CaptureBaselineIfNeeded();
            lock (_sync)
            {
                if (_rootsWithOverlay.TryGetValue(fe, out _))
                {
                    WriteMetricsToRoot(fe);
                    return;
                }
                _overlayRoots.Add(new WeakReference<FrameworkElement>(fe));
                _rootsWithOverlay.Add(fe, null);
                WriteMetricsToRoot(fe);
                PruneDead();
            }
        }

        private static void RefreshAllRoots()
        {
            PruneDead();
            foreach (var wr in _overlayRoots)
            {
                if (!wr.TryGetTarget(out var fe) || fe == null) continue;
                void Write()
                {
                    try
                    {
                        WriteMetricsToRoot(fe);
                        InvalidateMetricSubtree(fe);
                    }
                    catch { /* 单根失败忽略 */ }
                }
                if (fe.Dispatcher.CheckAccess()) Write();
                else fe.Dispatcher.BeginInvoke((Action)Write);
            }
        }

        private static void WriteMetricsToRoot(FrameworkElement fe)
        {
            if (!_baselineCaptured || _baseline.Count == 0) return;
            var res = fe.Resources;
            foreach (var kv in _baseline)
            {
                if (!TryGetScaleFactor(kv.Key, kv.Value, out var factor)) continue;
                var scaled = ScaleValue(kv.Value, factor);
                if (scaled != null) res[kv.Key] = scaled;
            }
        }

        private static void InvalidateMetricSubtree(DependencyObject root)
        {
            if (root is FrameworkElement fe)
            {
                fe.InvalidateMeasure();
                fe.InvalidateArrange();
            }
            try
            {
                var n = VisualTreeHelper.GetChildrenCount(root);
                for (var i = 0; i < n; i++)
                    InvalidateMetricSubtree(VisualTreeHelper.GetChild(root, i));
            }
            catch { /* 部分节点不可遍历 */ }
        }

        private static void PruneDead()
        {
            _overlayRoots.RemoveAll(w => !w.TryGetTarget(out var t) || t == null);
        }

        private static bool TryGetScaleFactor(string key, object baselineValue, out double factor)
        {
            factor = 1.0;
            if (ShouldSkipKey(key, baselineValue))
                return false;

            if (key.StartsWith("Metric_Font", StringComparison.Ordinal))
            {
                factor = _fontScale;
                return true;
            }

            if (IsInputWidthKey(key))
            {
                factor = _inputWidthScale;
                return true;
            }

            if (IsDensityKey(key))
            {
                factor = _densityScale;
                return true;
            }

            return false;
        }

        private static bool IsInputWidthKey(string key)
        {
            if (key.StartsWith("Metric_InputWidth_", StringComparison.Ordinal)) return true;
            if (key.StartsWith("Metric_NumInputWidth_", StringComparison.Ordinal)) return true;
            if (key.StartsWith("Metric_LabelWidth_", StringComparison.Ordinal)) return true;
            if (key.StartsWith("Metric_ToolButtonMinWidth_", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_DialogButtonWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_HeaderComboMinWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_FilterLabelColumnWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_RoadCheckNameColumn", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_HelpTextMaxWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_ButtonMinWidth_Small", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsDensityKey(string key)
        {
            if (key.StartsWith("Metric_Row", StringComparison.Ordinal)) return true;
            if (key.StartsWith("Metric_Expander", StringComparison.Ordinal)) return true;
            if (key.IndexOf("Padding", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (key.IndexOf("Inset", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (key.IndexOf("Margin", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (key.IndexOf("Height", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (key.IndexOf("Indent", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool ShouldSkipKey(string key, object baselineValue)
        {
            if (baselineValue is CornerRadius) return true;
            if (key.StartsWith("Metric_CornerRadius", StringComparison.Ordinal)) return true;

            // 装饰/布局常量：不缩放，避免错位
            if (string.Equals(key, "Metric_IconBarWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_TabAccentBarWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_ClearButtonSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_SelectionIndicatorSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_ThemeSwatchSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_IconSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_PrefsSideIconColumn", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_CheckDotSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_ToolbarPathIconSize", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_SplitterThickness", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_ToolbarWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_NPanelMinWidth", StringComparison.Ordinal)) return true;
            if (string.Equals(key, "Metric_OutlinerMinWidth", StringComparison.Ordinal)) return true;
            return false;
        }

        private static object ScaleValue(object value, double factor)
        {
            switch (value)
            {
                case double d:
                    return RoundDim(d * factor);
                case Thickness t:
                    return new Thickness(
                        RoundDim(t.Left * factor),
                        RoundDim(t.Top * factor),
                        RoundDim(t.Right * factor),
                        RoundDim(t.Bottom * factor));
                default:
                    return null;
            }
        }

        private static double RoundDim(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

        private static object CloneMetricValue(object value)
        {
            switch (value)
            {
                case double d: return d;
                case Thickness t: return t;
                case CornerRadius c: return c;
                default: return value;
            }
        }

        private static double ClampScale(double v)
        {
            if (double.IsNaN(v) || v <= 0) return 1.0;
            if (v < 0.5) return 0.5;
            if (v > 2.0) return 2.0;
            return v;
        }
    }
}
