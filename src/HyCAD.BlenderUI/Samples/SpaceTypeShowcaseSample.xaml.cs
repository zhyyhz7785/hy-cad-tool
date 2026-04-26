using System;
using System.Windows.Controls;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Screen.Areas;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    public partial class SpaceTypeShowcaseSample : BlenderWindow
    {
        public SpaceTypeShowcaseSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);
            SpaceCombo.ItemsSource = Enum.GetValues(typeof(SpaceTypeId));
            SpaceCombo.SelectedIndex = 0;
            if (SpaceCombo.SelectedItem is SpaceTypeId initial)
                AreaHost.SpaceType = initial;
        }

        private void OnSpaceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpaceCombo.SelectedItem is SpaceTypeId id)
                AreaHost.SpaceType = id;
        }
    }
}
