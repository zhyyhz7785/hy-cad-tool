using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using HyCADTool.Licensing;
using HyCADTool.Shell.Licensing;
using Microsoft.Win32;
using QRCoder;

namespace HyCADTool.Shell.Views
{
    public partial class ActivationWindow : System.Windows.Window
    {
        private const string ImportPlaceholder = "（已从文件导入 license.lic）";

        public ActivationWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Topmost = true;
            RefreshStatusUi();
            LoadMachineCodeAndQr();
        }

        private void LoadMachineCodeAndQr()
        {
            var mc = MachineId.GetMachineCode();
            MachineCodeText.Text = mc;
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
            catch (Exception ex)
            {
                StatusText.Text = "（二维码生成失败：" + ex.Message + "，请使用复制机器码。）";
            }
        }

        private void RefreshStatusUi(bool forceRefresh = false)
        {
            try
            {
                LicenseService.Instance.Refresh(forceRefresh);
            }
            catch (Exception ex)
            {
                StatusText.Text = "读取许可状态失败：" + ex.Message;
                return;
            }
            StatusText.Text = BuildStatus();
        }

        private static string BuildStatus()
        {
            var st = LicenseService.Instance.LastStatus;
            if (st.ClockRollBackLocked && st.RecognizedTier > LicenseProductTier.Freemium)
                return st.ErrorMessage ?? LicenseService.StateRecoveryHint;
            if (!st.Ok)
                return st.ErrorMessage ?? "未激活（免费版）。";
            if (st.Tier == LicenseProductTier.Freemium)
                return "未导入 license，当前为免费版。";
            var exp = st.Perpetual
                ? "终身有效"
                : "有效期至: " + (st.ExpiresAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-");
            return "已授权: " + st.Tier + "；" + exp;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(MachineCodeText.Text ?? "");
                StatusText.Text = "机器码已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                StatusText.Text = "复制失败：" + ex.Message;
            }
        }

        private void OnActivate(object sender, RoutedEventArgs e)
        {
            var text = LicenseCodeBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || text == ImportPlaceholder)
            {
                MessageBox.Show(this, "请粘贴 HYC1 授权码，或使用「从文件导入」。", "激活", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!LicenseService.TryImportLicenseText(text, out var err))
            {
                MessageBox.Show(this, err ?? "授权无效", "激活失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            FinishImportSuccess();
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog
            {
                Filter = "License (*.lic;*.json)|*.lic;*.json|所有文件|*.*",
                Title = "选择 license.lic"
            };
            if (d.ShowDialog() != true) return;

            string text;
            try
            {
                text = File.ReadAllText(d.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法读取文件：" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!LicenseService.TryImportLicenseText(text, out var err))
            {
                MessageBox.Show(this, err ?? "文件不是有效的 license 或签名校验失败。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LicenseCodeBox.Text = ImportPlaceholder;
            FinishImportSuccess();
        }

        private void FinishImportSuccess()
        {
            RefreshStatusUi(forceRefresh: true);
            LicenseGate.ResetBlockMessage();
            var st = LicenseService.Instance.LastStatus;
            if (st != null && st.Ok && st.Tier > LicenseProductTier.Freemium)
            {
                MessageBox.Show(this, "已激活：" + st.Tier, "HyCAD", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (st != null && st.ClockRollBackLocked && st.RecognizedTier > LicenseProductTier.Freemium)
            {
                MessageBox.Show(this,
                    "授权已写入，识别为 " + st.RecognizedTier + "。\n\n"
                    + LicenseService.StateRecoveryHint,
                    "HyCAD", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBox.Show(this,
                st?.ErrorMessage ?? "已写入 license.lic。若仍无权限请重启 AutoCAD 后重试。",
                "HyCAD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
