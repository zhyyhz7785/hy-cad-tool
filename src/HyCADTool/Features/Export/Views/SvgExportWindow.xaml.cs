using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.Export.Services.Svg;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export.Views
{
    public partial class SvgExportWindow : Window
    {
        private readonly SvgPlotSettings _settings;

        internal SvgPlotSettings ResultSettings => _settings;

        public SvgExportWindow()
        {
            InitializeComponent();
            _settings = (SvgPlotSettings.Session ?? new SvgPlotSettings()).Clone();
            cmbPaper.ItemsSource = SvgPaperCatalog.Items;
            LoadSettings();
        }

        private void LoadSettings()
        {
            int paperIndex = 7;
            for (int i = 0; i < SvgPaperCatalog.Items.Length; i++)
            {
                if (string.Equals(SvgPaperCatalog.Items[i].Id, _settings.PaperId, StringComparison.OrdinalIgnoreCase))
                {
                    paperIndex = i;
                    break;
                }
            }
            cmbPaper.SelectedIndex = paperIndex;
            txtPaperW.Text = F(_settings.CustomWidthMm);
            txtPaperH.Text = F(_settings.CustomHeightMm);

            cmbArea.SelectedIndex = (int)_settings.Area;
            UpdateWindowInfo();
            chkCenter.IsChecked = _settings.CenterPlot;
            txtOffsetX.Text = F(_settings.OffsetXMm);
            txtOffsetY.Text = F(_settings.OffsetYMm);

            chkFit.IsChecked = _settings.FitToPaper;
            SelectScale(_settings.UnitsPerMm);
            txtUnitsPerMm.Text = F(_settings.UnitsPerMm);

            cmbStyle.SelectedIndex = (int)_settings.Style;
            chkLineweight.IsChecked = _settings.PlotObjectLineweights;
            chkScaleLw.IsChecked = _settings.ScaleLineweights;
            chkWhiteBg.IsChecked = _settings.WhiteBackground;
            cmbOrient.SelectedIndex = (int)_settings.Orientation;

            chkIgnoreBlank.IsChecked = _settings.IgnoreBlank;
            chkClip.IsChecked = _settings.ClipToWindow;
            txtMargin.Text = F(_settings.MarginMm);
            chkBorder.IsChecked = _settings.DrawBorder;
            txtBorderW.Text = F(_settings.BorderWidthMm);

            UpdateEnabled();
        }

        private void SelectScale(double unitsPerMm)
        {
            string tag = "custom";
            double[] known = { 1, 2, 5, 10, 20, 50, 100, 200, 500 };
            for (int i = 0; i < known.Length; i++)
            {
                if (Math.Abs(unitsPerMm - known[i]) < 1e-6)
                {
                    tag = known[i].ToString("0", CultureInfo.InvariantCulture);
                    break;
                }
            }
            for (int i = 0; i < cmbScale.Items.Count; i++)
            {
                if (cmbScale.Items[i] is ComboBoxItem item && (item.Tag as string) == tag)
                {
                    cmbScale.SelectedIndex = i;
                    return;
                }
            }
            cmbScale.SelectedIndex = cmbScale.Items.Count - 1;
        }

        private bool CollectFromUi()
        {
            try
            {
                var paper = cmbPaper.SelectedItem as SvgPaperSize;
                _settings.PaperId = paper != null ? paper.Id : "ISO_A3_L";
                if (paper != null && paper.IsCustom)
                {
                    _settings.CustomWidthMm = ParsePositive(txtPaperW.Text, "纸张宽度");
                    _settings.CustomHeightMm = ParsePositive(txtPaperH.Text, "纸张高度");
                }

                _settings.Area = ParseArea();
                _settings.CenterPlot = chkCenter.IsChecked == true;
                _settings.OffsetXMm = ParseDouble(txtOffsetX.Text, "偏移 X");
                _settings.OffsetYMm = ParseDouble(txtOffsetY.Text, "偏移 Y");

                _settings.FitToPaper = chkFit.IsChecked == true;
                _settings.UnitsPerMm = ParsePositive(txtUnitsPerMm.Text, "1 mm 对应的图形单位");

                _settings.Style = ParseStyle();
                _settings.PlotObjectLineweights = chkLineweight.IsChecked == true;
                _settings.ScaleLineweights = chkScaleLw.IsChecked == true;
                _settings.WhiteBackground = chkWhiteBg.IsChecked == true;
                _settings.Orientation = ParseOrient();

                _settings.IgnoreBlank = chkIgnoreBlank.IsChecked == true;
                _settings.ClipToWindow = chkClip.IsChecked == true;
                _settings.MarginMm = ParseNonNegative(txtMargin.Text, "边距");
                _settings.DrawBorder = chkBorder.IsChecked == true;
                _settings.BorderWidthMm = ParsePositive(txtBorderW.Text, "边框线宽");
                return true;
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private SvgPlotArea ParseArea()
        {
            if (cmbArea.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "Window") return SvgPlotArea.Window;
                if (tag == "Display") return SvgPlotArea.Display;
                if (tag == "Selection") return SvgPlotArea.Selection;
            }
            return SvgPlotArea.Extents;
        }

        private SvgPlotStyle ParseStyle()
        {
            if (cmbStyle.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "Color") return SvgPlotStyle.Color;
                if (tag == "Grayscale") return SvgPlotStyle.Grayscale;
            }
            return SvgPlotStyle.Monochrome;
        }

        private SvgPlotOrientation ParseOrient()
        {
            if (cmbOrient.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "Portrait") return SvgPlotOrientation.Portrait;
                if (tag == "UpsideDown") return SvgPlotOrientation.UpsideDown;
            }
            return SvgPlotOrientation.Landscape;
        }

        private void UpdateEnabled()
        {
            bool custom = cmbPaper.SelectedItem is SvgPaperSize p && p.IsCustom;
            txtPaperW.IsEnabled = custom;
            txtPaperH.IsEnabled = custom;
            if (!custom && cmbPaper.SelectedItem is SvgPaperSize paper)
            {
                txtPaperW.Text = F(paper.WidthMm);
                txtPaperH.Text = F(paper.HeightMm);
            }

            bool window = ParseArea() == SvgPlotArea.Window;
            btnWindow.IsEnabled = true;
            if (!window)
                btnWindow.Opacity = 0.7;
            else
                btnWindow.Opacity = 1;

            bool center = chkCenter.IsChecked == true;
            txtOffsetX.IsEnabled = !center;
            txtOffsetY.IsEnabled = !center;

            bool fit = chkFit.IsChecked == true;
            cmbScale.IsEnabled = !fit;
            txtUnitsPerMm.IsEnabled = !fit && IsCustomScale();
        }

        private bool IsCustomScale()
        {
            return cmbScale.SelectedItem is ComboBoxItem item && (item.Tag as string) == "custom";
        }

        private void UpdateWindowInfo()
        {
            if (!_settings.HasWindow)
            {
                txtWindowInfo.Text = "未指定窗口";
                return;
            }
            var r = _settings.GetWindowRect();
            txtWindowInfo.Text = string.Format(
                CultureInfo.InvariantCulture,
                "窗口 ({0:0.##}, {1:0.##}) – ({2:0.##}, {3:0.##})",
                r.MinX, r.MinY, r.MaxX, r.MaxY);
        }

        private void CmbPaper_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEnabled();
        }

        private void CmbArea_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEnabled();
        }

        private void ChkCenter_Changed(object sender, RoutedEventArgs e)
        {
            UpdateEnabled();
        }

        private void ChkFit_Changed(object sender, RoutedEventArgs e)
        {
            UpdateEnabled();
        }

        private void CmbScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbScale.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "custom")
            {
                if (double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                    txtUnitsPerMm.Text = F(n);
            }
            UpdateEnabled();
        }

        private void BtnWindow_Click(object sender, RoutedEventArgs e)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;
            var ed = doc.Editor;
            cmbArea.SelectedIndex = 0;
            Visibility = Visibility.Hidden;
            try
            {
                var p1 = ed.GetPoint("\n指定打印窗口第一角点");
                if (p1.Status != PromptStatus.OK)
                    return;
                var p2 = ed.GetCorner("\n指定打印窗口另一角点", p1.Value);
                if (p2.Status != PromptStatus.OK)
                    return;
                _settings.HasWindow = true;
                _settings.WindowX1 = p1.Value.X;
                _settings.WindowY1 = p1.Value.Y;
                _settings.WindowX2 = p2.Value.X;
                _settings.WindowY2 = p2.Value.Y;
                UpdateWindowInfo();
            }
            finally
            {
                Visibility = Visibility.Visible;
                Activate();
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!CollectFromUi())
                return;
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                MessageBox.Show("没有活动文档。", "导出 SVG", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_settings.Area == SvgPlotArea.Window && !_settings.HasWindow)
            {
                MessageBox.Show("请先指定打印窗口。", "导出 SVG", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "hysvg-preview.svg");
            Visibility = Visibility.Hidden;
            try
            {
                if (!SvgExportRunner.TryExport(doc, _settings, path, out _, out string error))
                {
                    Visibility = Visibility.Visible;
                    Activate();
                    MessageBox.Show(error ?? "预览失败。", "导出 SVG", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            finally
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                    Activate();
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开浏览器: " + ex.Message, "导出 SVG", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!CollectFromUi())
                return;
            if (_settings.Area == SvgPlotArea.Window && !_settings.HasWindow)
            {
                MessageBox.Show("请先指定打印窗口。", "导出 SVG", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SvgPlotSettings.Session = _settings.Clone();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string F(double v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text, string name)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
                throw new FormatException(name + " 不是有效数字。");
            return v;
        }

        private static double ParsePositive(string text, string name)
        {
            double v = ParseDouble(text, name);
            if (v <= 0)
                throw new FormatException(name + " 必须大于 0。");
            return v;
        }

        private static double ParseNonNegative(string text, string name)
        {
            double v = ParseDouble(text, name);
            if (v < 0)
                throw new FormatException(name + " 不能为负。");
            return v;
        }
    }
}
