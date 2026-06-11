using System.Windows.Controls;

namespace HyCADTool.Shell.Views
{
    /// <summary>
    /// 独立「出图比例」编辑器面板；DataContext 由 <see cref="HyBlenderPanelViewModel.PreferencesVm"/> 注入。
    /// </summary>
    public partial class ScalePanel : UserControl
    {
        public ScalePanel()
        {
            InitializeComponent();
        }
    }
}
