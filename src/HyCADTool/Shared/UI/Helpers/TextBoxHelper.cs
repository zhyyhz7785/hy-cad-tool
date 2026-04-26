using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Input;

namespace HyCADTool.Shared.UI.Helpers
{
    /// <summary>
    /// TextBox 辅助类：解决 WPF 绑定 double 时无法输入小数点的问题，
    /// 并提供统一的数值输入体验（全选、回车确认、滚轮±、上下键±）。
    /// 用法：helper:TextBoxHelper.EnableCustomHandlers="True"
    ///        helper:TextBoxHelper.ScrollStep="0.1"  (可选，默认0.1)
    /// </summary>
    public static class TextBoxHelper
    {
        /// <summary>
        /// 追踪最后一个获得焦点的 TextBox（弱引用）。
        /// PaletteSet 中 TextBox 焦点转移到 AutoCAD 命令行时，
        /// WPF LostFocus 不一定触发，需要靠此引用兜底提交。
        /// </summary>
        private static WeakReference<TextBox> _lastFocusedTextBox;

        /// <summary>
        /// 强制提交最后一个被编辑的 TextBox 的绑定值。
        /// 用于 PaletteSet 环境中焦点离开 WPF 树但 LostFocus 未触发的情况。
        /// </summary>
        public static void CommitPendingInput()
        {
            if (_lastFocusedTextBox != null && _lastFocusedTextBox.TryGetTarget(out var tb))
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                _lastFocusedTextBox = null;
            }
        }

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

        #endregion

        #region ScrollStep 附加属性

        /// <summary>滚轮/箭头键每次增减的步长（默认0.1）</summary>
        public static readonly DependencyProperty ScrollStepProperty =
            DependencyProperty.RegisterAttached(
                "ScrollStep",
                typeof(double),
                typeof(TextBoxHelper),
                new PropertyMetadata(0.1));

        public static double GetScrollStep(DependencyObject obj)
            => (double)obj.GetValue(ScrollStepProperty);

        public static void SetScrollStep(DependencyObject obj, double value)
            => obj.SetValue(ScrollStepProperty, value);

        #endregion

        private static void OnEnableCustomHandlersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox textBox)) return;

            if ((bool)e.NewValue)
            {
                textBox.Loaded += TextBox_Loaded;
                textBox.GotFocus += TextBox_GotFocus;
                textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
                textBox.PreviewMouseDoubleClick += TextBox_PreviewMouseDoubleClick;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.PreviewMouseWheel += TextBox_PreviewMouseWheel;
                textBox.LostFocus += TextBox_LostFocus;
                textBox.TextChanged += TextBox_TextChanged;
                DataObject.AddPastingHandler(textBox, OnTextBoxPasting);
                ApplyLostFocusBinding(textBox);
            }
            else
            {
                textBox.Loaded -= TextBox_Loaded;
                textBox.GotFocus -= TextBox_GotFocus;
                textBox.PreviewMouseLeftButtonDown -= TextBox_PreviewMouseLeftButtonDown;
                textBox.PreviewMouseDoubleClick -= TextBox_PreviewMouseDoubleClick;
                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                textBox.PreviewMouseWheel -= TextBox_PreviewMouseWheel;
                textBox.LostFocus -= TextBox_LostFocus;
                textBox.TextChanged -= TextBox_TextChanged;
                DataObject.RemovePastingHandler(textBox, OnTextBoxPasting);
            }
        }

        #region 事件处理

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // AutoCAD PaletteSet 中绑定表达式有时会在附加属性回调之后才真正建立。
                // 放到 Loaded + Dispatcher 再处理，可稳定把数值框切到 LostFocus 提交模式。
                tb.Dispatcher.BeginInvoke(new Action(() => ApplyLostFocusBinding(tb)), DispatcherPriority.Background);
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _lastFocusedTextBox = new WeakReference<TextBox>(tb);
                tb.SelectAll();
            }
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
            if (sender is TextBox tb)
            {
                tb.SelectAll();
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox tb)) return;

            if (e.Key == Key.Decimal || e.Key == Key.OemPeriod)
            {
                InsertText(tb, ".");
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                AdjustValue(tb, +1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                AdjustValue(tb, -1);
                e.Handled = true;
            }
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextBox tb)) return;

            string input = e.Text;
            if (string.IsNullOrEmpty(input)) return;

            if (input == "." || input == ",")
            {
                InsertText(tb, ".");
                e.Handled = true;
            }
        }

        /// <summary>鼠标滚轮：悬停在 TextBox 上即可滚轮调整数值</summary>
        private static void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox tb && tb.IsKeyboardFocusWithin)
            {
                int direction = e.Delta > 0 ? +1 : -1;
                AdjustValue(tb, direction);
                e.Handled = true;
            }
        }

        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                _lastFocusedTextBox = null;
            }
        }

        /// <summary>
        /// 文本变化时，如果内容是完整的有效数字就立即提交到 ViewModel。
        /// 不完整输入（以 "." 或 "-" 结尾、空串）跳过，防止小数点被绑定回写吞掉。
        /// 这样既保留了 LostFocus 绑定的小数点安全性，又保证了 ViewModel 即时同步。
        /// </summary>
        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            string text = (tb.Text ?? "").Trim();

            if (string.IsNullOrEmpty(text) || text.EndsWith(".") || text == "-" || text == "+")
                return;

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        private static void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text)) return;

            string text = e.SourceDataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text)) return;

            InsertText(tb, NormalizeNumberText(text));
            e.CancelCommand();
        }

        #endregion

        #region 数值增减

        private static void AdjustValue(TextBox tb, int direction)
        {
            double step = GetScrollStep(tb);
            string normalizedText = NormalizeNumberText(tb.Text);
            if (double.TryParse(normalizedText, NumberStyles.Any, CultureInfo.InvariantCulture, out double current))
            {
                double newVal = Math.Round(current + step * direction, 4);
                tb.Text = newVal.ToString(CultureInfo.InvariantCulture);
                tb.SelectAll();
                // 立即更新绑定源
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        private static void InsertText(TextBox tb, string text)
        {
            string current = tb.Text ?? string.Empty;
            int selectionStart = tb.SelectionStart;
            int selectionLength = tb.SelectionLength;

            string next = current.Remove(selectionStart, selectionLength)
                .Insert(selectionStart, text);

            tb.Text = next;
            tb.SelectionStart = selectionStart + text.Length;
            tb.SelectionLength = 0;
        }

        private static string NormalizeNumberText(string text)
        {
            return (text ?? string.Empty).Trim().Replace(',', '.');
        }

        private static void ApplyLostFocusBinding(TextBox textBox)
        {
            var parentBinding = BindingOperations.GetBinding(textBox, TextBox.TextProperty);
            if (parentBinding == null) return;

            if (parentBinding.UpdateSourceTrigger == UpdateSourceTrigger.LostFocus
                && Equals(parentBinding.ConverterCulture, CultureInfo.InvariantCulture))
            {
                return;
            }

            var newBinding = new Binding(parentBinding.Path?.Path)
            {
                Mode = parentBinding.Mode,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                StringFormat = parentBinding.StringFormat,
                ConverterCulture = CultureInfo.InvariantCulture
            };

            if (parentBinding.Source != null && parentBinding.Source != DependencyProperty.UnsetValue)
                newBinding.Source = parentBinding.Source;
            if (parentBinding.RelativeSource != null)
                newBinding.RelativeSource = parentBinding.RelativeSource;
            if (!string.IsNullOrWhiteSpace(parentBinding.ElementName))
                newBinding.ElementName = parentBinding.ElementName;
            if (parentBinding.Converter != null)
                newBinding.Converter = parentBinding.Converter;
            if (parentBinding.ConverterParameter != null)
                newBinding.ConverterParameter = parentBinding.ConverterParameter;
            if (parentBinding.FallbackValue != DependencyProperty.UnsetValue)
                newBinding.FallbackValue = parentBinding.FallbackValue;
            if (parentBinding.TargetNullValue != DependencyProperty.UnsetValue)
                newBinding.TargetNullValue = parentBinding.TargetNullValue;
            if (parentBinding.ValidatesOnDataErrors)
                newBinding.ValidatesOnDataErrors = true;
            if (parentBinding.ValidatesOnExceptions)
                newBinding.ValidatesOnExceptions = true;
            if (parentBinding.NotifyOnValidationError)
                newBinding.NotifyOnValidationError = true;

            foreach (var rule in parentBinding.ValidationRules)
                newBinding.ValidationRules.Add(rule);

            BindingOperations.SetBinding(textBox, TextBox.TextProperty, newBinding);
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        #endregion
    }
}
