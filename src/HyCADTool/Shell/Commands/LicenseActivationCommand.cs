using HyCADTool.Shell.Views;

namespace HyCADTool.Shell.Commands
{
    /// <summary>打开「许可与激活」窗口（机器码、二维码、导入 license.lic）。</summary>
    public class LicenseActivationCommand
    {
        public void Execute()
        {
            var w = new ActivationWindow();
            w.Show();
        }
    }
}
