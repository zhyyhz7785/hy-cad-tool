using System.ComponentModel;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Layout;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    public class DemoPoco : INotifyPropertyChanged
    {
        private bool _visible = true;
        private double _density = 1.25;
        private string _name = "demo";

        public bool Visible
        {
            get => _visible;
            set { _visible = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible))); }
        }

        [BlenderProp(Min = 0, Max = 10, Precision = 2)]
        public double Density
        {
            get => _density;
            set { _density = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Density))); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class AutoPropertySample : BlenderWindow
    {
        public AutoPropertySample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);

            var model = new DemoPoco();
            var block = new UIBlock();
            var col = block.Root.Column();
            col.Prop(model, nameof(DemoPoco.Visible));
            col.Prop(model, nameof(DemoPoco.Density));
            col.Prop(model, nameof(DemoPoco.Name));
            Host.Content = block.RootPanel;
        }
    }
}
