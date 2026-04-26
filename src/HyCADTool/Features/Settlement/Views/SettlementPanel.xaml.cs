using System.Windows.Controls;
using HyCADTool.Features.Settlement.ViewModels;

namespace HyCADTool.Features.Settlement.Views
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
