using System.Windows.Controls;
using HyCADTool.Features.Tables.ViewModels;

namespace HyCADTool.Features.Tables.Views
{
    public partial class TablePanel : UserControl
    {
        public TablePanel(TablePanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public TablePanel()
        {
            InitializeComponent();
        }
    }
}
