using System.Windows;
using System.Windows.Media;

namespace HyCADTool.MarkdownEditor.Services
{
    internal class ThemeManager
    {
        public bool IsDark { get; private set; } = true;

        public void ApplyTheme(ResourceDictionary resources, bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                SetThemeColor(resources, "ThemeBgRootBrush", "#111418");
                SetThemeColor(resources, "ThemeBgPanelBrush", "#161b22");
                SetThemeColor(resources, "ThemeBgSurfaceBrush", "#1c2128");
                SetThemeColor(resources, "ThemeBgInputBrush", "#0d1117");
                SetThemeColor(resources, "ThemeBorderBrush", "#24292e");
                SetThemeColor(resources, "ThemeBorderStrongBrush", "#30363d");
                SetThemeColor(resources, "ThemeTextPrimaryBrush", "#c9d1d9");
                SetThemeColor(resources, "ThemeTextSecondaryBrush", "#8b949e");
                SetThemeColor(resources, "ThemeTextMutedBrush", "#6e7681");
                SetThemeColor(resources, "ThemeAccentBrush", "#0078D4");
                SetThemeColor(resources, "ThemeAccentHoverBrush", "#1a8cef");
                SetThemeColor(resources, "ThemeOnAccentBrush", "#ffffff");
                SetThemeColor(resources, "ThemeHoverBrush", "#30363d");
                SetThemeColor(resources, "ThemeSelectionBrush", "#263545");
                SetThemeColor(resources, "ThemeFocusBorderBrush", "#007FD4");
                SetThemeColor(resources, "ThemeMenuPopupBgBrush", "#1a2028");
                SetThemeColor(resources, "ThemeMenuPopupBorderBrush", "#39424d");
                SetThemeColor(resources, "ThemeMenuPopupHoverBrush", "#2c3948");
                SetThemeColor(resources, "ThemeMenuPopupSelectionBrush", "#1f4f7a");

                SetThemeColor(resources, SystemColors.MenuBrushKey, "#1a2028");
                SetThemeColor(resources, SystemColors.MenuTextBrushKey, "#c9d1d9");
                SetThemeColor(resources, SystemColors.MenuHighlightBrushKey, "#2c3948");
                SetThemeColor(resources, SystemColors.HighlightBrushKey, "#1f4f7a");
                SetThemeColor(resources, SystemColors.HighlightTextBrushKey, "#ffffff");
                SetThemeColor(resources, SystemColors.MenuBarBrushKey, "#161b22");
                SetThemeColor(resources, SystemColors.WindowBrushKey, "#1a2028");
                SetThemeColor(resources, SystemColors.WindowTextBrushKey, "#c9d1d9");
            }
            else
            {
                SetThemeColor(resources, "ThemeBgRootBrush", "#f5f7fa");
                SetThemeColor(resources, "ThemeBgPanelBrush", "#eef2f6");
                SetThemeColor(resources, "ThemeBgSurfaceBrush", "#ffffff");
                SetThemeColor(resources, "ThemeBgInputBrush", "#ffffff");
                SetThemeColor(resources, "ThemeBorderBrush", "#d0d7de");
                SetThemeColor(resources, "ThemeBorderStrongBrush", "#b6c2cf");
                SetThemeColor(resources, "ThemeTextPrimaryBrush", "#24292f");
                SetThemeColor(resources, "ThemeTextSecondaryBrush", "#57606a");
                SetThemeColor(resources, "ThemeTextMutedBrush", "#6e7781");
                SetThemeColor(resources, "ThemeAccentBrush", "#0078D4");
                SetThemeColor(resources, "ThemeAccentHoverBrush", "#1a8cef");
                SetThemeColor(resources, "ThemeOnAccentBrush", "#ffffff");
                SetThemeColor(resources, "ThemeHoverBrush", "#dde6ef");
                SetThemeColor(resources, "ThemeSelectionBrush", "#dbeafe");
                SetThemeColor(resources, "ThemeFocusBorderBrush", "#0078D4");
                SetThemeColor(resources, "ThemeMenuPopupBgBrush", "#f8fafc");
                SetThemeColor(resources, "ThemeMenuPopupBorderBrush", "#c7d1db");
                SetThemeColor(resources, "ThemeMenuPopupHoverBrush", "#e8f2ff");
                SetThemeColor(resources, "ThemeMenuPopupSelectionBrush", "#dbeafe");

                SetThemeColor(resources, SystemColors.MenuBrushKey, "#f8fafc");
                SetThemeColor(resources, SystemColors.MenuTextBrushKey, "#24292f");
                SetThemeColor(resources, SystemColors.MenuHighlightBrushKey, "#e8f2ff");
                SetThemeColor(resources, SystemColors.HighlightBrushKey, "#dbeafe");
                SetThemeColor(resources, SystemColors.HighlightTextBrushKey, "#24292f");
                SetThemeColor(resources, SystemColors.MenuBarBrushKey, "#eef2f6");
                SetThemeColor(resources, SystemColors.WindowBrushKey, "#f8fafc");
                SetThemeColor(resources, SystemColors.WindowTextBrushKey, "#24292f");
            }
        }

        public Brush GetBrush(FrameworkElement element, string key)
        {
            if (element.TryFindResource(key) is Brush brush)
                return brush;
            return BrushFromHex("#c9d1d9");
        }

        private static void SetThemeColor(ResourceDictionary resources, string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        private static void SetThemeColor(ResourceDictionary resources, object key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        private static SolidColorBrush BrushFromHex(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
