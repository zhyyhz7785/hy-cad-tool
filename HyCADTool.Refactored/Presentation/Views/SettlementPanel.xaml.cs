using System.Windows.Controls;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views
{
    public partial class SettlementPanel : UserControl
    {
        public SettlementPanel(SettlementPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public SettlementPanel()
        {
            InitializeComponent();
        }
    }
}
