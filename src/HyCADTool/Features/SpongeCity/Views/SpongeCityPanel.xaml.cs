using System.Windows.Controls;

namespace HyCADTool.Features.SpongeCity.Views
{
    /// <summary>
    /// 海绵城市面板（HyB 内嵌 UserControl，绑定 SpongeCityPanelViewModel）。
    /// 通过 HyBlenderPanel.xaml 模式 4 DataTrigger 在 IsSpongeCityMode=True 时可见。
    /// </summary>
    public partial class SpongeCityPanel : UserControl
    {
        public SpongeCityPanel()
        {
            InitializeComponent();
        }
    }
}
