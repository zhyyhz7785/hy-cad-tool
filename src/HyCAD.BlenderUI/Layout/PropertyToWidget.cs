using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Controls.Widgets;

namespace HyCAD.BlenderUI.Layout
{
    /// <summary>PropertyDescriptor → 控件工厂（阶段 3）。</summary>
    public static class PropertyToWidget
    {
        public static UIElement CreateForProperty(object target, PropertyDescriptor pd)
        {
            if (target == null || pd == null) return new TextBlock { Text = "?" };

            var attr = pd.Attributes[typeof(BlenderPropAttribute)] as BlenderPropAttribute;
            var t = pd.PropertyType;

            if (t == typeof(bool))
            {
                var cb = new CheckBox { IsChecked = (bool)(pd.GetValue(target) ?? false) };
                TryStyle(cb, "BlenderCheckBox");
                cb.Checked += (_, __) => pd.SetValue(target, true);
                cb.Unchecked += (_, __) => pd.SetValue(target, false);
                return cb;
            }

            if (t == typeof(double) || t == typeof(float) || t == typeof(int) || t == typeof(long))
            {
                double min = attr != null && !double.IsNaN(attr.Min) ? attr.Min : 0;
                double max = attr != null && !double.IsNaN(attr.Max) ? attr.Max : 100;
                var ns = new NumericSlider
                {
                    Minimum = min,
                    Maximum = max,
                    Decimals = attr?.Precision ?? 3,
                };
                TryStyle(ns, null);
                ns.SetBinding(NumericSlider.ValueProperty, new Binding(pd.Name) { Source = target, Mode = BindingMode.TwoWay });
                return ns;
            }

            if (t == typeof(string))
            {
                var tb = new TextBox();
                TryStyle(tb, "BlenderTextBox");
                tb.SetBinding(TextBox.TextProperty, new Binding(pd.Name) { Source = target, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                return tb;
            }

            if (t == typeof(Color) || t == typeof(Color?))
            {
                var c = pd.GetValue(target) is Color cc ? cc : Colors.Gray;
                var fld = new BlenderColorField { SelectedColor = c };
                fld.SetBinding(BlenderColorField.SelectedColorProperty, new Binding(pd.Name) { Source = target, Mode = BindingMode.TwoWay });
                return fld;
            }

            var fallback = new TextBox { Text = pd.GetValue(target)?.ToString() ?? "" };
            TryStyle(fallback, "BlenderTextBox");
            return fallback;
        }

        private static void TryStyle(FrameworkElement fe, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var app = Application.Current;
            if (app?.Resources.Contains(key) == true)
                fe.Style = app.Resources[key] as Style;
        }
    }
}
