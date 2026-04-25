using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using HyCADTool.Licensing;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.LicenseSigner
{
    public partial class MainWindow : System.Windows.Window
    {
        private bool _perpetual = true;
        private DateTime _expiresUtc = DateTime.UtcNow.AddYears(100);

        public MainWindow()
        {
            InitializeComponent();
            SetExp(DateTime.UtcNow.AddYears(1), false);
        }

        private void Y1(object sender, RoutedEventArgs e) => SetExp(DateTime.UtcNow.AddYears(1), false);
        private void Y3(object sender, RoutedEventArgs e) => SetExp(DateTime.UtcNow.AddYears(3), false);
        private void Perp(object sender, RoutedEventArgs e) => SetExp(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc), true);

        private void SetExp(DateTime exp, bool perp)
        {
            _expiresUtc = perp ? new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc) : exp;
            _perpetual = perp;
            ExpiryInfo.Text = "expires_at(UTC) = " + _expiresUtc.ToString("o") + " ； perpetual = " + _perpetual;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";
            var mc = (MachineCodeBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(mc) || LicenseService.NormalizeMc(mc).Length < 8)
            {
                ErrorText.Text = "请填写有效机器码。";
                return;
            }
            mc = HyCADTool.Licensing.MachineId.NormalizeMachineCodeFromUser(mc);
            var ed = (EditionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "standard";
            if (EditionBox.IsEditable) ed = (EditionBox.Text ?? "standard").Trim();
            if (string.IsNullOrEmpty(ed)) ed = "standard";

            var privPath = Path.Combine(AppContext.BaseDirectory, "PrivateKey.xml");
            if (!File.Exists(privPath))
            {
                var tryTools = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools", "rsa-priv.xml"));
                if (File.Exists(tryTools)) privPath = tryTools;
            }
            if (!File.Exists(privPath))
            {
                ErrorText.Text = "找不到私钥。请将 tools/_gen-rsa-keys.ps1 生成的 rsa-priv.xml 复制为输出目录的 PrivateKey.xml。";
                return;
            }
            var priv = File.ReadAllText(privPath);

            var j = new JObject
            {
                ["v"] = 1,
                ["customer"] = CustomerBox.Text?.Trim() ?? "",
                ["machine_code"] = mc,
                ["edition"] = ed,
                ["features"] = new JArray(DefaultFeaturesForEdition(ed)),
                ["issued_at"] = DateTime.UtcNow,
                ["expires_at"] = _expiresUtc,
                ["perpetual"] = _perpetual,
                ["max_devices"] = 1
            };
            var payload = LicenseCanonicalizer.GetUtf8SignPayloadFromJObject(j);
            byte[] sig;
            try
            {
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(priv);
                    sig = rsa.SignHash(hash, "SHA256");
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = "签名失败: " + ex.Message;
                return;
            }
            j["signature"] = Convert.ToBase64String(sig);

            var d = new SaveFileDialog
            {
                FileName = "license.lic",
                Filter = "License (*.lic)|*.lic|JSON (*.json)|*.json|所有文件|*.*"
            };
            if (d.ShowDialog() != true) return;
            var text = j.ToString(Formatting.Indented);
            File.WriteAllText(d.FileName, text, new UTF8Encoding(false));
            MessageBox.Show("已保存: " + d.FileName, "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string[] DefaultFeaturesForEdition(string ed)
        {
            var e = (ed ?? "").ToLowerInvariant();
            if (e.Contains("standard"))
                return new[] { "rein", "elevation", "dim", "anchor", "equip" };
            if (e.Contains("professional") || e.Contains("enterprise"))
                return new[] { "rein", "elevation", "dim", "anchor", "equip", "road", "pile", "basere", "settle" };
            return Array.Empty<string>();
        }
    }
}
