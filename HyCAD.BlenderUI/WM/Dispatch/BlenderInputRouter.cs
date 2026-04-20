using System.Windows;
using System.Windows.Input;
using HyCAD.BlenderUI.WM.KeyMap;
using HyCAD.BlenderUI.WM.Operators;

namespace HyCAD.BlenderUI.WM.Dispatch
{
    /// <summary>键盘 → KeyMap → Operator（阶段 6）。</summary>
    public sealed class BlenderInputRouter
    {
        public KeyMapConfig KeyMap { get; set; } = new KeyMapConfig { Name = "Default" };

        public void Attach(UIElement root)
        {
            root.PreviewKeyDown += OnPreviewKeyDown;
        }

        public void Detach(UIElement root)
        {
            root.PreviewKeyDown -= OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            foreach (var item in KeyMap.Items)
            {
                if (item.Key != e.Key) continue;
                if (item.Modifiers != Keyboard.Modifiers) continue;
                if (!OperatorRegistry.TryCreate(item.OperatorId, out var op)) continue;
                var ctx = new OperatorContext();
                if (!op.Poll(ctx)) continue;
                var r = op.Invoke(e, ctx);
                if (r != OperatorResult.PassThrough)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}
