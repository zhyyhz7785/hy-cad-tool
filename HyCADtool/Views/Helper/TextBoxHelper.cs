using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace HyCADTool.HelpClass
{
    // 定义一个静态类 TextBoxHelper，用于为 TextBox 控件添加自定义事件处理程序
    public static class TextBoxHelper
    {
        // 定义一个附加属性 EnableCustomHandlers，用于控制是否启用自定义事件处理程序
        public static readonly DependencyProperty EnableCustomHandlersProperty =
            DependencyProperty.RegisterAttached(
                "EnableCustomHandlers",  // 附加属性的名称
                typeof(bool),            // 附加属性的类型
                typeof(TextBoxHelper),   // 定义附加属性的类的类型
                new PropertyMetadata(false, OnEnableCustomHandlersChanged)); // 默认值和属性更改回调方法
        // 获取附加属性的值
        public static bool GetEnableCustomHandlers(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableCustomHandlersProperty);
        }
        // 设置附加属性的值
        public static void SetEnableCustomHandlers(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableCustomHandlersProperty, value);
        }
        // 当 EnableCustomHandlers 属性的值发生变化时调用此方法
        private static void OnEnableCustomHandlersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 确保 d 是一个 TextBox
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue) // 如果新值为 true，启用自定义事件处理程序
                {
                    // 绑定事件处理程序
                    textBox.MouseDoubleClick += TextBox_MouseDoubleClick;
                    textBox.PreviewTextInput += TextBox_PreviewTextInput;
                    textBox.TextChanged += TextBox_TextChanged;
                    DataObject.AddPastingHandler(textBox, TextBox_Pasting);
                }
                else // 如果新值为 false，取消自定义事件处理程序
                {
                    // 解绑事件处理程序
                    textBox.MouseDoubleClick -= TextBox_MouseDoubleClick;
                    textBox.PreviewTextInput -= TextBox_PreviewTextInput;
                    textBox.TextChanged -= TextBox_TextChanged;
                    DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
                }
            }
        }
        // 当 TextBox 被双击时调用此方法
        private static void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 清空 TextBox 的内容
                textBox.Clear();
            }
        }
        // 当 TextBox 接收到文本输入时调用此方法
        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 如果输入的文本不符合要求，设置 e.Handled 为 true，阻止输入
            e.Handled = !IsTextAllowed(e.Text);
        }
        // 当 TextBox 的内容发生变化时调用此方法
        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 此处可以根据需要处理文本更改事件
        }
        // 当粘贴内容到 TextBox 时调用此方法
        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            // 检查粘贴的数据是否为字符串类型
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                // 如果粘贴的文本不符合要求，取消粘贴命令
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                // 如果粘贴的数据不是字符串类型，取消粘贴命令
                e.CancelCommand();
            }
        }
        // 检查输入的文本是否为允许的格式（仅允许数字和小数点）
        private static bool IsTextAllowed(string text)
        {
            return Regex.IsMatch(text, "^[0-9]*\\.?[0-9]*$");
        }
    }
}
