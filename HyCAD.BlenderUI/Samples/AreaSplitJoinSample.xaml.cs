using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Screen.Areas;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    public partial class AreaSplitJoinSample : BlenderWindow
    {
        private readonly AreaTreeModel _model = new AreaTreeModel();

        public AreaSplitJoinSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
            ScreenHost.Model = _model;
        }

        private void OnSplitH(object sender, RoutedEventArgs e) => _model.SplitRoot(true);

        private void OnSplitV(object sender, RoutedEventArgs e) => _model.SplitRoot(false);
    }
}
