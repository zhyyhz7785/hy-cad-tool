using System.Windows.Controls;

namespace HyCADTool.Features.Settlement
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
