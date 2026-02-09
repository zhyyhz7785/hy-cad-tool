using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyCADTool.Refactored.Presentation.Views.Helpers
{
    /// <summary>
    /// TextBox 辅助类：解决 WPF 绑定 double 时无法输入小数点的问题，
    /// 并提供统一的数值输入体验（全选、回车确认等）。
    /// 用法：helper:TextBoxHelper.EnableCustomHandlers="True"
    /// </summary>
    public static class TextBoxHelper
    {
        #region EnableCustomHandlers 附加属性

        public static readonly DependencyProperty EnableCustomHandlersProperty =
            DependencyProperty.RegisterAttached(
                "EnableCustomHandlers",
                typeof(bool),
                typeof(TextBoxHelper),
                new PropertyMetadata(false, OnEnableCustomHandlersChanged));

        public static bool GetEnableCustomHandlers(DependencyObject obj)
            => (bool)obj.GetValue(EnableCustomHandlersProperty);

        public static void SetEnableCustomHandlers(DependencyObject obj, bool value)
            => obj.SetValue(EnableCustomHandlersProperty, value);

        private static void OnEnableCustomHandlersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox textBox)) return;

            if ((bool)e.NewValue)
            {
                textBox.GotFocus += TextBox_GotFocus;
                textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
                textBox.PreviewMouseDoubleClick += TextBox_PreviewMouseDoubleClick;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.LostFocus += TextBox_LostFocus;
                // 关键：改用 LostFocus 触发绑定更新，而非 PropertyChanged
                // 这样输入小数点时不会被 double 解析吞掉
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
                    // 保留 RelativeSource
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
                textBox.LostFocus -= TextBox_LostFocus;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 获得焦点时全选文本（键盘 Tab 进入时生效）
        /// </summary>
        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

        /// <summary>
        /// 单击：如果 TextBox 还没有焦点，点击后全选（而非定位光标）
        /// </summary>
        private static void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsFocused)
            {
                tb.Focus();
                tb.SelectAll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 双击：全选文本并清除（用户可直接输入新值覆盖）
        /// </summary>
        private static void TextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 回车键：手动更新绑定源并移出焦点
        /// </summary>
        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 失去焦点时确保绑定更新
        /// </summary>
        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }
        }

        #endregion
    }
}
