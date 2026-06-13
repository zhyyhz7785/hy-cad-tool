using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Licensing;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.LicenseSigner
{
    public partial class MainWindow : System.Windows.Window
    {
        private bool _perpetual;
        private DateTime _expiresUtc = DateTime.UtcNow.AddYears(1);
        private string _privateKeyXml;
        private string _generatedText;
        private string _generatedCode;

        public MainWindow()
        {
            InitializeComponent();
            DetectPrivateKey();
            SetExp(DateTime.UtcNow.AddYears(1), false);
        }

        private void DetectPrivateKey()
        {
            var path = ResolvePrivateKeyPath();
            if (path != null && File.Exists(path))
            {
                try
                {
                    _privateKeyXml = File.ReadAllText(path);
                    using (var rsa = new RSACryptoServiceProvider())
                    {
                        rsa.FromXmlString(_privateKeyXml);
                    }
                    KeyDot.Fill = (Brush)FindResource("Ok");
                    PrivateKeyStatus.Foreground = (Brush)FindResource("Text");
                    PrivateKeyStatus.Text = "私钥就绪：" + path;
                    return;
                }
                catch (Exception ex)
                {
                    _privateKeyXml = null;
                    KeyDot.Fill = (Brush)FindResource("Err");
                    PrivateKeyStatus.Foreground = (Brush)FindResource("Err");
                    PrivateKeyStatus.Text = "私钥无效：" + ex.Message;
                    return;
                }
            }

            _privateKeyXml = null;
            KeyDot.Fill = (Brush)FindResource("Err");
            PrivateKeyStatus.Foreground = (Brush)FindResource("Err");
            PrivateKeyStatus.Text = "未找到私钥。请把 build/keys/rsa-priv.xml 复制为本程序同目录的 PrivateKey.xml。";
        }

        private static string ResolvePrivateKeyPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "PrivateKey.xml"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools", "rsa-priv.xml")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "build", "keys", "rsa-priv.xml")),
            };
            foreach (var c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }
            return candidates[0];
        }

        private void OnMachineCodeChanged(object sender, TextChangedEventArgs e)
        {
            if (MachineCodeHint == null) return;
            var raw = MachineCodeBox.Text ?? "";
            var norm = MachineId.NormalizeMc(raw);
            if (string.IsNullOrEmpty(norm))
            {
                MachineCodeHint.Text = "";
            }
            else if (!MachineId.IsValidMachineCodeFormat(raw))
            {
                MachineCodeHint.Foreground = (Brush)FindResource("Err");
                MachineCodeHint.Text = norm.Length != 32
                    ? "机器码须为 8 段 × 4 位（已规范化 " + norm.Length + " 位）。"
                    : "机器码含非法字符，请确认粘贴完整。";
            }
            else
            {
                MachineCodeHint.Foreground = (Brush)FindResource("Subtle");
                MachineCodeHint.Text = "已规范化：" + norm;
            }
        }

        private void Y1(object sender, RoutedEventArgs e) => SetExp(DateTime.UtcNow.AddYears(1), false);
        private void Y3(object sender, RoutedEventArgs e) => SetExp(DateTime.UtcNow.AddYears(3), false);
        private void Perp(object sender, RoutedEventArgs e) => SetExp(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc), true);

        private void OnCustomExpiry(object sender, SelectionChangedEventArgs e)
        {
            if (CustomExpiry.SelectedDate is DateTime d)
            {
                var utc = DateTime.SpecifyKind(d.Date.AddHours(23).AddMinutes(59).AddSeconds(59), DateTimeKind.Utc);
                SetExp(utc, false);
            }
        }

        private void SetExp(DateTime exp, bool perp)
        {
            _perpetual = perp;
            _expiresUtc = perp ? new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc) : exp;
            if (ExpiryInfo == null) return;
            ExpiryInfo.Text = perp
                ? "永久授权（无到期日）"
                : "到期：" + _expiresUtc.ToLocalTime().ToString("yyyy-MM-dd") + "（UTC " + _expiresUtc.ToString("yyyy-MM-dd") + "）";
        }

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";
            CopyCodeHint.Text = "";
            SaveLicBtn.IsEnabled = false;
            CopyCodeBtn.IsEnabled = false;
            _generatedText = null;
            _generatedCode = null;

            if (string.IsNullOrEmpty(_privateKeyXml))
            {
                ErrorText.Text = "私钥未就绪，无法签发。请先放好 PrivateKey.xml。";
                return;
            }

            var rawMc = MachineCodeBox.Text ?? "";
            var mc = MachineId.NormalizeMachineCodeFromUser(rawMc);
            if (!MachineId.IsValidMachineCodeFormat(mc))
            {
                ErrorText.Text = "请填写完整机器码（8 段 × 4 位，无非法字符）。";
                return;
            }

            var ed = (EditionBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (EditionBox.IsEditable && !string.IsNullOrWhiteSpace(EditionBox.Text)) ed = EditionBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ed)) ed = "standard";

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

            try
            {
                var payload = LicenseCanonicalizer.GetUtf8SignPayloadFromJObject(j);
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
                byte[] sig;
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_privateKeyXml);
                    sig = rsa.SignHash(hash, "SHA256");
                }
                j["signature"] = Convert.ToBase64String(sig);
            }
            catch (Exception ex)
            {
                ErrorText.Text = "签名失败：" + ex.Message;
                return;
            }

            _generatedText = j.ToString(Formatting.Indented);

            if (!LicenseService.TryVerifyDocument(_generatedText, out _, out var verifyErr))
            {
                ErrorText.Text = "自校验失败（私钥与内嵌公钥可能不匹配）：" + verifyErr;
                _generatedText = null;
                return;
            }

            _generatedCode = LicenseCodeCodec.Encode(_generatedText);

            if (!LicenseCodeCodec.TryDecodeToLicenseJson(_generatedCode, out var roundTrip, out var decodeErr)
                || !LicenseService.TryVerifyDocument(roundTrip, out _, out verifyErr))
            {
                ErrorText.Text = "HYC1 编解码回环失败：" + (decodeErr ?? verifyErr);
                _generatedText = null;
                _generatedCode = null;
                return;
            }

            CodeOutputBox.Text = _generatedCode;
            SaveLicBtn.IsEnabled = true;
            CopyCodeBtn.IsEnabled = true;
            CopyCodeHint.Foreground = (Brush)FindResource("Ok");
            CopyCodeHint.Text = "已生成 HYC1 授权码。复制发给客户，或保存 license.lic 文件。";
            if (!AppendLedger(mc, ed, CustomerBox.Text?.Trim() ?? ""))
                CopyCodeHint.Text += "（台账 CSV 写入失败，请检查 exe 目录写权限。）";
        }

        private void OnSaveLic(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_generatedText)) return;
            var dlg = new SaveFileDialog
            {
                FileName = "license.lic",
                Filter = "License (*.lic)|*.lic|JSON (*.json)|*.json|所有文件|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(dlg.FileName, _generatedText, new UTF8Encoding(false));
                CopyCodeHint.Foreground = (Brush)FindResource("Ok");
                CopyCodeHint.Text = "已保存 license.lic：" + dlg.FileName;
            }
            catch (Exception ex)
            {
                ErrorText.Text = "保存失败：" + ex.Message;
            }
        }

        private void OnCopyCode(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_generatedCode)) return;
            try
            {
                Clipboard.SetText(_generatedCode);
                CopyCodeHint.Foreground = (Brush)FindResource("Ok");
                CopyCodeHint.Text = "HYC1 授权码已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                ErrorText.Text = "复制失败：" + ex.Message;
            }
        }

        private bool AppendLedger(string machineCode, string edition, string customer)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "issued-licenses.csv");
                var header = "UtcTime,Customer,MachineCode,Edition,Perpetual,ExpiresAt,CodeLength";
                var line = string.Join(",",
                    EscapeCsv(DateTime.UtcNow.ToString("o")),
                    EscapeCsv(customer),
                    EscapeCsv(machineCode),
                    EscapeCsv(edition),
                    _perpetual ? "true" : "false",
                    EscapeCsv(_expiresUtc.ToString("o")),
                    (_generatedCode?.Length ?? 0).ToString());
                if (!File.Exists(path))
                    File.WriteAllText(path, header + Environment.NewLine, new UTF8Encoding(true));
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(true));
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    var path = Path.Combine(Path.GetTempPath(), "signer-crash.log");
                    File.AppendAllText(path,
                        "--- ledger " + DateTime.UtcNow.ToString("o") + " ---\n" + ex + "\n",
                        Encoding.UTF8);
                }
                catch
                {
                }
                return false;
            }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
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
