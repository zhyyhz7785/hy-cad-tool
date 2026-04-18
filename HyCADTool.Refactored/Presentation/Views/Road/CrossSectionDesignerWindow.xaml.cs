using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// "标准横断面图设计器"窗口。
    ///
    /// 职责：
    /// <list type="bullet">
    ///   <item>把 <see cref="CrossSectionDesignerViewModel"/> 作为 DataContext。</item>
    ///   <item>订阅 <see cref="CrossSectionDesignerViewModel.PreviewRequested"/>，将 <see cref="CrossSectionFigure"/>
    ///   投影到 WPF <see cref="Canvas"/> 上做实时预览（轮廓 / 分面填色 / 横坡 / 尺寸 / 顶部标签）。</item>
    ///   <item>把 DataGrid 选中行同步到 VM.SelectedBand。</item>
    ///   <item>注入不涉及 WPF 的 <see cref="MessageBox"/> 二次确认回调。</item>
    /// </list>
    ///
    /// 最终的 AutoCAD 出图由命令层订阅 <see cref="CrossSectionDesignerViewModel.Confirmed"/> 完成，
    /// 本窗口不直接引用 AutoCAD API。
    /// </summary>
    public partial class CrossSectionDesignerWindow : Window
    {
        private CrossSectionDesignerViewModel _vm;
        private CrossSectionFigure _currentFigure;

        private static readonly Dictionary<TemplateComponentKind, Brush> PanelFills = new Dictionary<TemplateComponentKind, Brush>
        {
            [TemplateComponentKind.Pavement]     = new SolidColorBrush(Color.FromArgb(0x60, 0x4c, 0x8a, 0xb3)),
            [TemplateComponentKind.NonMotorized] = new SolidColorBrush(Color.FromArgb(0x60, 0x9a, 0xb0, 0x73)),
            [TemplateComponentKind.Sidewalk]     = new SolidColorBrush(Color.FromArgb(0x60, 0xcd, 0xb8, 0x7f)),
            [TemplateComponentKind.GreenStrip]   = new SolidColorBrush(Color.FromArgb(0x60, 0x6f, 0xa3, 0x74)),
            [TemplateComponentKind.Kerb]         = new SolidColorBrush(Color.FromArgb(0x60, 0x5e, 0x81, 0xac)),
            [TemplateComponentKind.Shoulder]     = new SolidColorBrush(Color.FromArgb(0x60, 0xa3, 0x91, 0x74)),
            [TemplateComponentKind.MedianStrip]  = new SolidColorBrush(Color.FromArgb(0x60, 0x7a, 0x9c, 0x6b)),
        };

        private static readonly Brush OutlineBrush      = new SolidColorBrush(Color.FromRgb(0xd8, 0xde, 0xe9));
        private static readonly Brush CenterLineBrush   = new SolidColorBrush(Color.FromRgb(0xbf, 0x61, 0x6a));
        private static readonly Brush DimensionBrush    = new SolidColorBrush(Color.FromRgb(0x88, 0xc0, 0xd0));
        private static readonly Brush SlopeBrush        = new SolidColorBrush(Color.FromRgb(0xeb, 0xcb, 0x8b));
        private static readonly Brush TopLabelBrush     = new SolidColorBrush(Color.FromRgb(0xa3, 0xbe, 0x8c));
        private static readonly Brush AnnotationBrush   = new SolidColorBrush(Color.FromRgb(0xd8, 0xde, 0xe9));

        public CrossSectionDesignerWindow()
        {
            InitializeComponent();
            KeyDown += OnWindowKeyDown;
        }

        public CrossSectionDesignerWindow(CrossSectionDesignerViewModel viewModel) : this()
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _vm = viewModel;
            DataContext = viewModel;

            viewModel.PreviewRequested += OnPreviewRequested;
            viewModel.CloseRequested += OnCloseRequested;

            viewModel.NonCompliantConfirm = summary => MessageBox.Show(
                this,
                summary,
                "存在未通过的规范项",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) == MessageBoxResult.OK;

            Loaded += (_, __) =>
            {
                _currentFigure = viewModel.LastFigure;
                RedrawPreview();
            };

            Closed += (_, __) =>
            {
                viewModel.PreviewRequested -= OnPreviewRequested;
                viewModel.CloseRequested -= OnCloseRequested;
            };
        }

        // =========================================================================
        //  顶部 / 关闭
        // =========================================================================

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            try { DialogResult = dialogResult; }
            catch (InvalidOperationException) { /* ShowModalWindow 时 DialogResult 不可设 */ }
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null && _vm.CancelCommand.CanExecute(null))
            {
                _vm.CancelCommand.Execute(null);
                return;
            }
            Close();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (_vm == null) return;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_vm.ConfirmCommand.CanExecute(null)) _vm.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_vm.CancelCommand.CanExecute(null)) _vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _vm.LoadLayout(Domain.Services.Road.CrossSectionPresets.CreateCjj37UrbanArterial());
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                PresetCombo.IsDropDownOpen = true;
                PresetCombo.Focus();
                e.Handled = true;
            }
        }

        // =========================================================================
        //  预设
        // =========================================================================

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (PresetCombo.SelectedItem is Domain.Services.Road.PresetDescriptor p)
            {
                if (_vm.LoadPresetCommand.CanExecute(p)) _vm.LoadPresetCommand.Execute(p);
                // 选中后立即让下拉失焦，避免每次打开窗口都高亮
                PresetCombo.SelectedItem = null;
            }
        }

        // =========================================================================
        //  DataGrid 选中 -> VM.SelectedBand
        // =========================================================================

        private void AnyGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is DataGrid grid)
            {
                // 其他 grid 清空选择，避免双选
                if (grid != LeftGrid) LeftGrid.UnselectAll();
                if (grid != RightGrid) RightGrid.UnselectAll();

                if (grid.SelectedItem is BandRowViewModel row)
                {
                    _vm.SelectedBand = row;
                }
                else
                {
                    _vm.SelectedBand = null;
                }
            }
        }

        // =========================================================================
        //  预览
        // =========================================================================

        private void OnPreviewRequested(object sender, CrossSectionFigure figure)
        {
            _currentFigure = figure;
            RedrawPreview();
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawPreview();
        }

        private void RedrawPreview()
        {
            if (PreviewCanvas == null) return;
            PreviewCanvas.Children.Clear();
            if (_currentFigure == null) return;

            double cw = PreviewCanvas.ActualWidth;
            double ch = PreviewCanvas.ActualHeight;
            if (cw < 20 || ch < 20) return;

            // 画面边距
            const double padX = 40;
            const double padTop = 50;
            const double padBottom = 60;

            // 计算路宽 / 垂直范围
            var vertices = _currentFigure.Vertices;
            var panels = _currentFigure.Panels;
            if (vertices.Count == 0) return;

            double xmin = vertices.Min(v => v.X);
            double xmax = vertices.Max(v => v.X);
            double ymin = vertices.Min(v => v.Y);
            double ymax = vertices.Max(v => v.Y);

            // 保证有高度（平坦时给 0.3m 的像素余量）
            double spanX = Math.Max(1e-3, xmax - xmin);
            double spanY = Math.Max(0.3, ymax - ymin);

            double sx = (cw - padX * 2) / spanX;
            double sy = (ch - padTop - padBottom) / Math.Max(1.0, spanY);
            // 纵向放大系数，让平坦断面也能看出坡
            double yExaggerate = 8.0;
            double s = Math.Min(sx, sy * yExaggerate);
            if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0) return;

            double ox = cw / 2.0;
            double oy = ch - padBottom;

            Func<double, double, Point> map = (x, y) => new Point(ox + x * s, oy - y * yExaggerate * s);

            // 面填色
            foreach (var panel in panels)
            {
                int i0 = Math.Max(0, panel.StartVertexIndex);
                int i1 = Math.Min(vertices.Count - 1, panel.EndVertexIndex);
                if (i1 - i0 < 1) continue;

                var poly = new System.Windows.Shapes.Polygon
                {
                    Stroke = OutlineBrush,
                    StrokeThickness = 0.8,
                    Fill = PanelFills.TryGetValue(panel.Kind, out var f) ? f : Brushes.Transparent,
                };
                for (int i = i0; i <= i1; i++)
                {
                    var v = vertices[i];
                    poly.Points.Add(map(v.X, v.Y));
                }
                // 向下闭合到底线（y=0）
                var last = vertices[i1];
                var first = vertices[i0];
                poly.Points.Add(map(last.X, 0));
                poly.Points.Add(map(first.X, 0));
                PreviewCanvas.Children.Add(poly);
            }

            // 顶部粗轮廓线
            if (vertices.Count >= 2)
            {
                var top = new Polyline
                {
                    Stroke = OutlineBrush,
                    StrokeThickness = 1.4,
                };
                foreach (var v in vertices)
                {
                    top.Points.Add(map(v.X, v.Y));
                }
                PreviewCanvas.Children.Add(top);
            }

            // 中线（道路中心）
            var center = new Line
            {
                X1 = ox,
                X2 = ox,
                Y1 = oy + 8,
                Y2 = padTop - 10,
                Stroke = CenterLineBrush,
                StrokeThickness = 0.8,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0, 1.0, 3.0 }),
            };
            PreviewCanvas.Children.Add(center);
            AddText(ox + 4, padTop - 18, "中心线", CenterLineBrush, 10);

            // 地面线（基线）
            var baseLine = new Line
            {
                X1 = padX * 0.5,
                X2 = cw - padX * 0.5,
                Y1 = oy,
                Y2 = oy,
                Stroke = new SolidColorBrush(Color.FromRgb(0x4c, 0x56, 0x6a)),
                StrokeThickness = 0.6,
                StrokeDashArray = new DoubleCollection(new[] { 1.0, 4.0 }),
            };
            PreviewCanvas.Children.Add(baseLine);

            // 横坡标注
            foreach (var slope in _currentFigure.SlopeLabels)
            {
                var p = map(slope.PositionX, slope.PositionY);
                AddText(p.X - 15, p.Y - 16, slope.Text, SlopeBrush, 10);
            }

            // 顶部尺寸链（简化：一条横线 + 内部竖线分隔，不同段宽底部加宽度文字）
            double dimY = padTop - 2;
            double dimTickY1 = padTop + 6;
            double dimTickY2 = padTop - 4;
            double dimTextY = padTop - 16;

            var dimLine = new Line
            {
                X1 = padX * 0.7,
                X2 = cw - padX * 0.7,
                Y1 = dimY,
                Y2 = dimY,
                Stroke = DimensionBrush,
                StrokeThickness = 0.7,
            };
            PreviewCanvas.Children.Add(dimLine);

            // 只显示 Tier=1 的分段尺寸（总长 Tier=0 放底部用不到这里；Tier=2 为顶部总宽）
            foreach (var seg in _currentFigure.DimensionSegments)
            {
                if (seg.Tier != 1) continue;
                double x1 = ox + seg.StartX * s;
                double x2 = ox + seg.EndX * s;
                PreviewCanvas.Children.Add(new Line { X1 = x1, X2 = x1, Y1 = dimTickY1, Y2 = dimTickY2, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                PreviewCanvas.Children.Add(new Line { X1 = x2, X2 = x2, Y1 = dimTickY1, Y2 = dimTickY2, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                double midX = (x1 + x2) / 2;
                AddText(midX - 15, dimTextY, seg.Text, DimensionBrush, 10);
            }

            // 顶部条带标签
            foreach (var top in _currentFigure.TopLabels)
            {
                double x = ox + top.CenterX * s;
                AddText(x - 20, padTop + 10, top.Text, TopLabelBrush, 10);
            }

            // 方位：左箭头/右箭头
            if (!string.IsNullOrWhiteSpace(_currentFigure.Orientation.LeftLabel)
                || !string.IsNullOrWhiteSpace(_currentFigure.Orientation.RightLabel))
            {
                DrawOrientation(_currentFigure.Orientation);
            }

            // 图题
            if (!string.IsNullOrWhiteSpace(_currentFigure.Title.Text))
            {
                var title = new TextBlock
                {
                    Text = _currentFigure.Title.Text,
                    Foreground = AnnotationBrush,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                };
                Canvas.SetLeft(title, 8);
                Canvas.SetTop(title, 8);
                PreviewCanvas.Children.Add(title);
            }

            // 高差标注（简化文字）
            double heightCursorY = padTop + 32;
            foreach (var h in _currentFigure.HeightLabels)
            {
                AddText(10, heightCursorY, h.Text, AnnotationBrush, 10);
                heightCursorY += 14;
            }

            // 比例尺 + 路宽总长提示（右下角）
            var info = new TextBlock
            {
                Text = string.Format(CultureInfo.InvariantCulture,
                    "比例 1:{0}  路幅 {1:F2} m",
                    _vm?.ScaleDenominator ?? 100,
                    spanX),
                Foreground = AnnotationBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
            };
            Canvas.SetRight(info, 8);
            Canvas.SetTop(info, 8);
            PreviewCanvas.Children.Add(info);
        }

        private void DrawOrientation(FigureOrientation orientation)
        {
            double cw = PreviewCanvas.ActualWidth;
            double ch = PreviewCanvas.ActualHeight;
            // 左右箭头放在底部两侧
            double y = ch - 22;

            // 左方向
            AddText(10, y - 10, orientation.LeftLabel, AnnotationBrush, 10);
            var la = new Polyline
            {
                Stroke = AnnotationBrush,
                StrokeThickness = 0.9,
            };
            la.Points.Add(new Point(40, y));
            la.Points.Add(new Point(75, y));
            PreviewCanvas.Children.Add(la);
            // 左箭头头部
            var lh = new Polygon { Fill = AnnotationBrush };
            lh.Points.Add(new Point(40, y));
            lh.Points.Add(new Point(48, y - 3));
            lh.Points.Add(new Point(48, y + 3));
            PreviewCanvas.Children.Add(lh);

            // 右方向
            AddText(cw - 60, y - 10, orientation.RightLabel, AnnotationBrush, 10);
            var ra = new Polyline
            {
                Stroke = AnnotationBrush,
                StrokeThickness = 0.9,
            };
            ra.Points.Add(new Point(cw - 75, y));
            ra.Points.Add(new Point(cw - 40, y));
            PreviewCanvas.Children.Add(ra);
            var rh = new Polygon { Fill = AnnotationBrush };
            rh.Points.Add(new Point(cw - 40, y));
            rh.Points.Add(new Point(cw - 48, y - 3));
            rh.Points.Add(new Point(cw - 48, y + 3));
            PreviewCanvas.Children.Add(rh);
        }

        private void AddText(double x, double y, string text, Brush brush, double size)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = size,
                FontFamily = new FontFamily("Consolas"),
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            PreviewCanvas.Children.Add(tb);
        }
    }
}
