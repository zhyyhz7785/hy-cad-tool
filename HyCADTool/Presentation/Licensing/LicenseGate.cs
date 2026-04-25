using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Licensing;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

#if HYCAD_PRODUCTION

namespace HyCADTool.Presentation.Licensing
{
    /// <summary>生产包命令许可门闸（Freemium + Standard/Pro 分级）。</summary>
    public static class LicenseGate
    {
        private static bool _activationShown;

        public static void RunGated(string commandKey, Action run)
        {
            if (string.Equals(System.Environment.GetEnvironmentVariable("HYCAD_BYPASS_LICENSE"), "1", StringComparison.OrdinalIgnoreCase))
            {
                run();
                return;
            }

            var need = LicenseCommandTierMap.GetRequiredTier(commandKey);
            var eff = LicenseService.Instance.GetEffectiveProductTier();
            if ((int)eff >= (int)need)
            {
                run();
                return;
            }

            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            var msg = "\n[HyCAD] 此命令需更高许可（需要 " + need + "，当前 " + eff + "）。请运行 hyLicense 打开激活/导入。\n";
            ed?.WriteMessage(msg);

            if (!_activationShown)
            {
                _activationShown = true;
                try
                {
                    if (System.Windows.Application.Current != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var w = new Views.ActivationWindow { Owner = null };
                            w.Show();
                        });
                    }
                }
                catch
                {
                }
            }
        }
    }
}

#endif
