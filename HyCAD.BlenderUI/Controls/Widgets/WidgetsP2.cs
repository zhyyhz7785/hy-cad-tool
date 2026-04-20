using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    public class BlenderUnitVec : Control
    {
        static BlenderUnitVec() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderUnitVec), new FrameworkPropertyMetadata(typeof(BlenderUnitVec))); }
    }

    public class BlenderTrackPreview : Control
    {
        static BlenderTrackPreview() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderTrackPreview), new FrameworkPropertyMetadata(typeof(BlenderTrackPreview))); }
    }

    public class BlenderTreeRow : Border
    {
        public BlenderTreeRow() { Padding = new Thickness(4, 2, 4, 2); }
    }

    public class BlenderListRow : Border
    {
        public BlenderListRow() { Padding = new Thickness(6, 4, 6, 4); }
    }

    public class BlenderSeparatorLine : Border
    {
        public BlenderSeparatorLine()
        {
            Height = 1;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Background = Brushes.Gray;
        }
    }

    public class BlenderSeparatorSpacer : Control
    {
        public BlenderSeparatorSpacer() { Height = 8; }
    }

    public class BlenderIconSeparator : Border
    {
        public BlenderIconSeparator()
        {
            Width = 1;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = Brushes.Gray;
        }
    }

    public class BlenderBlockButton : Button
    {
        static BlenderBlockButton() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderBlockButton), new FrameworkPropertyMetadata(typeof(BlenderBlockButton))); }
    }
}
