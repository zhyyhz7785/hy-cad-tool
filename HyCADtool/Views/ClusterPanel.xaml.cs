using HyCADTool.Interfaces;
using HyCADTool.Services;
using System.Windows.Controls;

namespace HyCADTool.Views
{
    /// <summary>
    /// ClusterPanel.xaml 的交互逻辑
    /// </summary>
    public partial class ClusterPanel : UserControl
    {
        public ClusterPanel()
        {
            InitializeComponent();
            DataContext = new ViewModels.ClusterPanelViewModel();
        }

    }
}
