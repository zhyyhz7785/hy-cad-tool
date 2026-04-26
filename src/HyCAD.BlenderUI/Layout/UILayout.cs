using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Layout
{
    /// <summary>uiLayout 链式 API（Column / Row / Split）。</summary>
    public sealed class UILayout
    {
        public Panel Panel { get; }

        internal UILayout(Panel panel)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        }

        public UILayout Column(bool align = false)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            if (align) sp.HorizontalAlignment = HorizontalAlignment.Stretch;
            Panel.Children.Add(sp);
            return new UILayout(sp);
        }

        public UILayout Row()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            Panel.Children.Add(sp);
            return new UILayout(sp);
        }

        public void Label(string text)
        {
            Panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 0, 8, 0) });
        }

        public void Prop(object target, string propertyName)
        {
            if (target == null) return;
            var pd = TypeDescriptor.GetProperties(target).Find(propertyName, false);
            if (pd == null) return;
            var el = PropertyToWidget.CreateForProperty(target, pd);
            Panel.Children.Add(el);
        }
    }
}
