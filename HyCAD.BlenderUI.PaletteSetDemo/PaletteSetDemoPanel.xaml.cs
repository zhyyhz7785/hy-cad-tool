using System.Windows.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.PaletteSetDemo
{
    public partial class PaletteSetDemoPanel : UserControl
    {
        public PaletteSetDemoPanel()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
        }
    }
}
