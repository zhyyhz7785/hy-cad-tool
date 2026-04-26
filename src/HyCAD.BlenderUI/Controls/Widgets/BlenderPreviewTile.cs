using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_PREVIEW_TILE。</summary>
    public class BlenderPreviewTile : ContentControl
    {
        static BlenderPreviewTile()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderPreviewTile),
                new FrameworkPropertyMetadata(typeof(BlenderPreviewTile)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(BlenderPreviewTile), new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }
}
