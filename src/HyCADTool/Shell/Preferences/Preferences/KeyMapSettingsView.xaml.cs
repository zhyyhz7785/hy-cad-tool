using System.Windows.Controls;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 首选项「快捷键」分组 View。
    /// 每次 new 都自建一个独立 <see cref="KeyMapSettingsViewModel"/>（当前无全局共享需求；
    /// 后续若要多面板同步编辑，再改走 HySettingsViewModel 持有单例）。
    /// </summary>
    public partial class KeyMapSettingsView : UserControl
    {
        public KeyMapSettingsView()
        {
            InitializeComponent();
            DataContext = new KeyMapSettingsViewModel();
        }
    }
}
