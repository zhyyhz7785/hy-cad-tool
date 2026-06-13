using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace HyCADTool.LicenseSigner
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            MessageBox.Show(
                "HyCAD 授权签发中心发生未处理异常：\n" + e.Exception.Message + "\n\n详情见 %TEMP%\\signer-crash.log",
                "HyCAD LicenseSigner",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogCrash(ex);
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), "signer-crash.log");
                var sb = new StringBuilder();
                sb.AppendLine("--- " + DateTime.UtcNow.ToString("o") + " ---");
                sb.AppendLine(ex.ToString());
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
