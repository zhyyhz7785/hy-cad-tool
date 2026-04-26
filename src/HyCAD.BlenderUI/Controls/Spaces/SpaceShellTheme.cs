using System;
using System.Windows;

namespace HyCAD.BlenderUI.Controls.Spaces
{
    /// <summary>为独立嵌入（Area / PaletteSet）的 Space 壳合并 BlenderDark + BlenderTheme。</summary>
    internal static class SpaceShellTheme
    {
        private static readonly Uri BlenderDarkUri = new Uri(
            "pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderDark.xaml", UriKind.Absolute);
        private static readonly Uri BlenderThemeUri = new Uri(
            "pack://application:,,,/HyCAD.BlenderUI;component/Themes/BlenderTheme.xaml", UriKind.Absolute);

        internal static void MergeDefault(FrameworkElement el)
        {
            if (el == null) throw new ArgumentNullException(nameof(el));
            el.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = BlenderDarkUri });
            el.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = BlenderThemeUri });
        }
    }
}
