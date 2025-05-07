using HyCADTool.Interfaces;
using System.Windows.Controls;
namespace HyCADTool.Views
{
    public partial class PilePanel : UserControl
    {
        public PilePanel(ICadService cadService, IAreaFactory areaFactory, IConfigService configService)
        {
            InitializeComponent();
            DataContext = new ViewModels.PilePanelViewModel(cadService, areaFactory, configService);
        }
    }
}