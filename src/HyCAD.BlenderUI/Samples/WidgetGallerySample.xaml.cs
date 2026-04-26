using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    public partial class WidgetGallerySample : BlenderWindow
    {
        public WidgetGallerySample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
