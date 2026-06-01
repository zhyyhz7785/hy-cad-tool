using System.Windows;
using System.Windows.Controls;

namespace HyCADTool.Features.Fem.Views
{
    public partial class BeamMvpPanel : UserControl
    {
        public BeamMvpPanel()
        {
            InitializeComponent();
        }

        public BeamMvpPanel(BeamMvpPanelViewModel vm)
            : this()
        {
            DataContext = vm;
        }

        public BeamMvpPanelViewModel ViewModel
        {
            get => DataContext as BeamMvpPanelViewModel;
            set => DataContext = value;
        }
    }
}
