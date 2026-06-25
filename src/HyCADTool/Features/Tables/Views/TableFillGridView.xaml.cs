using System.Windows;
using System.Windows.Controls;
using HyCADTool.Features.Tables.ViewModels;

namespace HyCADTool.Features.Tables.Views
{
    public partial class TableFillGridView : UserControl
    {
        public TableFillGridView()
        {
            InitializeComponent();
        }

        private void OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row?.Item is TableFillRowVm row && row.IsReadOnly)
                e.Cancel = true;
        }
    }
}
