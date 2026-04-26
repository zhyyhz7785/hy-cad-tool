using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_KEY_EVENT / HOTKEY_EVENT。</summary>
    public class BlenderKeyEventField : TextBox
    {
        static BlenderKeyEventField()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderKeyEventField),
                new FrameworkPropertyMetadata(typeof(BlenderKeyEventField)));
        }

        public BlenderKeyEventField()
        {
            IsReadOnly = true;
            Cursor = System.Windows.Input.Cursors.Hand;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            var sb = new StringBuilder();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
            if (e.Key >= Key.A && e.Key <= Key.Z)
                sb.Append((char)('A' + (e.Key - Key.A)));
            else
                sb.Append(e.Key.ToString());
            Text = sb.ToString();
        }
    }
}
