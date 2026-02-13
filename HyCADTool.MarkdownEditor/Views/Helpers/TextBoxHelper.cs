using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyCADTool.MarkdownEditor.Views.Helpers
{
    /// <summary>
    /// TextBox 辅助类：全选、回车确认、滚轮±、上下键±
    /// </summary>
    public static class TextBoxHelper
    {
        #region EnableCustomHandlers

        public static readonly DependencyProperty EnableCustomHandlersProperty =
            DependencyProperty.RegisterAttached(
                "EnableCustomHandlers", typeof(bool), typeof(TextBoxHelper),
                new PropertyMetadata(false, OnEnableCustomHandlersChanged));

        public static bool GetEnableCustomHandlers(DependencyObject obj) => (bool)obj.GetValue(EnableCustomHandlersProperty);
        public static void SetEnableCustomHandlers(DependencyObject obj, bool value) => obj.SetValue(EnableCustomHandlersProperty, value);

        #endregion

        #region ScrollStep

        public static readonly DependencyProperty ScrollStepProperty =
            DependencyProperty.RegisterAttached(
                "ScrollStep", typeof(double), typeof(TextBoxHelper),
                new PropertyMetadata(0.1));

        public static double GetScrollStep(DependencyObject obj) => (double)obj.GetValue(ScrollStepProperty);
        public static void SetScrollStep(DependencyObject obj, double value) => obj.SetValue(ScrollStepProperty, value);

        #endregion

        private static void OnEnableCustomHandlersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            if ((bool)e.NewValue)
            {
                textBox.GotFocus += TextBox_GotFocus;
                textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
                textBox.PreviewMouseDoubleClick += TextBox_PreviewMouseDoubleClick;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.PreviewMouseWheel += TextBox_PreviewMouseWheel;
                textBox.LostFocus += TextBox_LostFocus;
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding != null)
                {
                    var newBinding = new System.Windows.Data.Binding(binding.ParentBinding.Path.Path)
                    {
                        Source = binding.ParentBinding.Source,
                        Mode = binding.ParentBinding.Mode,
                        UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                        StringFormat = binding.ParentBinding.StringFormat,
                        ConverterCulture = CultureInfo.InvariantCulture
                    };
                    if (binding.ParentBinding.RelativeSource != null)
                        newBinding.RelativeSource = binding.ParentBinding.RelativeSource;
                    textBox.SetBinding(TextBox.TextProperty, newBinding);
                }
            }
            else
            {
                textBox.GotFocus -= TextBox_GotFocus;
                textBox.PreviewMouseLeftButtonDown -= TextBox_PreviewMouseLeftButtonDown;
                textBox.PreviewMouseDoubleClick -= TextBox_PreviewMouseDoubleClick;
                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                textBox.PreviewMouseWheel -= TextBox_PreviewMouseWheel;
                textBox.LostFocus -= TextBox_LostFocus;
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private static void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsFocused)
            {
                tb.Focus();
                tb.SelectAll();
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb) { tb.SelectAll(); e.Handled = true; }
        }

        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (e.Key == Key.Enter)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up) { AdjustValue(tb, +1); e.Handled = true; }
            else if (e.Key == Key.Down) { AdjustValue(tb, -1); e.Handled = true; }
        }

        private static void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox tb) { AdjustValue(tb, e.Delta > 0 ? +1 : -1); e.Handled = true; }
        }

        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private static void AdjustValue(TextBox tb, int direction)
        {
            double step = GetScrollStep(tb);
            if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double current))
            {
                double newVal = Math.Round(current + step * direction, 4);
                tb.Text = newVal.ToString(CultureInfo.InvariantCulture);
                tb.SelectAll();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }
    }
}
