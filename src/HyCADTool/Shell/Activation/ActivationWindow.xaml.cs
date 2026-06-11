using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HyCADTool.Licensing;
using Microsoft.Win32;
using QRCoder;

namespace HyCADTool.Shell.Views
{
    public partial class ActivationWindow : System.Windows.Window
    {
        public ActivationWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LicenseService.Instance.Refresh();
            }
            catch
            {
            }
            var mc = MachineId.GetMachineCode();
            MachineCodeText.Text = mc;
            StatusText.Text = BuildStatus();
            try
            {
                using (var gen = new QRCodeGenerator())
                {
                    var data = gen.CreateQrCode(mc, QRCodeGenerator.ECCLevel.M);
                    var pngQr = new PngByteQRCode(data);
                    var png = pngQr.GetGraphic(8);
                    var img = new BitmapImage();
                    using (var s = new MemoryStream(png))
                    {
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = s;
                        img.EndInit();
                    }
                    img.Freeze();
                    QrImage.Source = img;
                }
            }
            catch
            {
                StatusText.Text = (StatusText.Text ?? "") + "（二维码生成失败，请使用复制。）\n";
            }
        }

        private string BuildStatus()
        {
            var s = LicenseService.Instance;
            s.Refresh();
            if (s.LastStatus == null) return "状态未知。";
            if (!s.LastStatus.Ok)
                return s.LastStatus.ErrorMessage ?? "未激活（免费版）。";
            if (s.LastStatus.Tier == LicenseProductTier.Freemium)
                return "未导入 license，当前为免费版。";
            var exp = s.LastStatus.Perpetual
                ? "终身有效"
                : "有效期至: " + (s.LastStatus.ExpiresAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-");
            return "已授权: " + s.LastStatus.Tier + "； " + exp;
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(MachineCodeText.Text ?? "");
                StatusText.Text = "已复制到剪贴板。";
            }
            catch
            {
            }
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog
            {
                Filter = "License (*.lic;*.json)|*.lic;*.json|所有文件|*.*",
                Title = "选择 license.lic"
            };
            if (d.ShowDialog() != true) return;
            if (!LicenseService.TryImportToProgramData(d.FileName))
            {
                System.Windows.MessageBox.Show(this, "无法导入：文件不是有效的 license 或签名校验失败。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LicenseService.Instance.Refresh();
            StatusText.Text = BuildStatus();
            if (LicenseService.Instance.LastStatus != null && LicenseService.Instance.LastStatus.Ok && LicenseService.Instance.LastStatus.Tier > LicenseProductTier.Freemium)
                System.Windows.MessageBox.Show(this, "已导入并激活: " + LicenseService.Instance.LastStatus.Tier, "HyCAD", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                System.Windows.MessageBox.Show(this, "已导入。若仍无权限请重启 AutoCAD 后重试，或看状态行提示。", "HyCAD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
