using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// 纵断面 v2 面板原型（Blender 样板）。
    ///
    /// <para>
    /// 四区布局：左 Outliner | 中上 Profile View（Canvas）| 中下 Band Set（Canvas）| 右 PropertyEditor。
    /// 不引用 HyCADTool.Refactored / AutoCAD；几何仅骨架（折线直连 PVI，不画抛物线）。
    /// </para>
    ///
    /// <para>命名 / 层级对齐 Profile / ProfileVertex / ProfileEditorViewModel，便于后续平移回真窗口。</para>
    /// </summary>
    public partial class ProfileV2SamplePanel : BlenderWindow
    {
        private readonly V2ProfileSampleViewModel _viewModel;

        // Profile View 几何边距（像素）
        private const double ProfileMarginLeft = 48;
        private const double ProfileMarginRight = 16;
        private const double ProfileMarginTop = 28;
        private const double ProfileMarginBottom = 28;

        // BandSet 行高（像素）
        private const double BandRowHeight = 22;
        private const double BandLabelWidth = 110;

        public ProfileV2SamplePanel()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);

            _viewModel = new V2ProfileSampleViewModel();
            DataContext = _viewModel;

            _viewModel.PreviewRequested += (_, __) => RenderAll();

            Loaded += (_, __) => RenderAll();
        }

        private void OutlinerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedNode = e.NewValue;
        }

        private void ProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderProfileView();
        }

        private void BandSetCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderBandSet();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestClose(null);
        }

        // =========================================================================
        //  顶级入口
        // =========================================================================

        private void RenderAll()
        {
            RenderProfileView();
            RenderBandSet();
        }

        // =========================================================================
        //  Profile View：网格 + EG(虚线) + FG(实线) + PVI(红点)
        // =========================================================================

        private void RenderProfileView()
        {
            if (ProfileCanvas == null) return;
            ProfileCanvas.Children.Clear();

            double canvasW = ProfileCanvas.ActualWidth;
            double canvasH = ProfileCanvas.ActualHeight;
            if (canvasW < 80 || canvasH < 80) return;

            double totalLen = _viewModel.AlignmentLength;
            if (totalLen <= 0)
            {
                DrawEmptyHint(ProfileCanvas, canvasW, canvasH, "（路线长度为 0，请在大纲选中路线后设置总长度）");
                return;
            }

            // 计算高程范围（EG ∪ FG 上下各留 1m 余量）
            var allElevs = new List<double>();
            foreach (var p in _viewModel.DesignProfile.Pvis) allElevs.Add(p.Elevation);
            foreach (var s in _viewModel.ExistingGround.Samples) allElevs.Add(s.Elevation);
            if (allElevs.Count == 0)
            {
                DrawEmptyHint(ProfileCanvas, canvasW, canvasH, "（无 PVI / EG 数据）");
                return;
            }
            double hMin = allElevs.Min() - 1.0;
            double hMax = allElevs.Max() + 1.0;
            if (hMax - hMin < 1e-3) hMax = hMin + 1.0;

            double startK = _viewModel.Alignment.StartStation;
            double endK = _viewModel.Alignment.EndStation;

            double plotW = Math.Max(10, canvasW - ProfileMarginLeft - ProfileMarginRight);
            double plotH = Math.Max(10, canvasH - ProfileMarginTop - ProfileMarginBottom);

            // 桩号 -> 像素 X
            double SxOfK(double k)
                => ProfileMarginLeft + (k - startK) / (endK - startK) * plotW;

            // 高程 -> 像素 Y（Y 反转）
            double SyOfH(double h)
                => ProfileMarginTop + (hMax - h) / (hMax - hMin) * plotH;

            // 1) 网格
            DrawProfileGrid(startK, endK, hMin, hMax, plotW, plotH, SxOfK, SyOfH);

            // 2) EG 黑虚线
            DrawEg(SxOfK, SyOfH);

            // 3) FG 蓝实线（直连 PVI）
            DrawFg(SxOfK, SyOfH);

            // 4) PVI 红点 + 标签
            DrawPviMarkers(SxOfK, SyOfH);

            // 5) 标题
            DrawTitleBar(canvasW, _viewModel.Title + $"   （1 : {_viewModel.ScaleDenominator}，L = {totalLen:F2} m，V = {_viewModel.DesignSpeed} km/h）");
        }

        private void DrawProfileGrid(double startK, double endK, double hMin, double hMax,
            double plotW, double plotH, Func<double, double> SxOfK, Func<double, double> SyOfH)
        {
            var minorBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80));
            var majorBrush = (Brush)FindResource("Brush_BorderDark");
            var axisBrush = (Brush)FindResource("Brush_BorderMid");

            double yTop = ProfileMarginTop;
            double yBottom = ProfileMarginTop + plotH;
            double xLeft = ProfileMarginLeft;
            double xRight = ProfileMarginLeft + plotW;

            // 次网格：每 20 m
            for (double k = Math.Ceiling(startK / 20) * 20; k <= endK; k += 20)
            {
                double x = SxOfK(k);
                if (Math.Abs((k / 100) - Math.Round(k / 100)) < 1e-6) continue;
                var line = new Line { X1 = x, X2 = x, Y1 = yTop, Y2 = yBottom, Stroke = minorBrush, StrokeThickness = 0.5 };
                ProfileCanvas.Children.Add(line);
            }
            // 主网格：每 100 m + 桩号标签
            for (double k = Math.Ceiling(startK / 100) * 100; k <= endK + 1e-3; k += 100)
            {
                double x = SxOfK(k);
                var line = new Line { X1 = x, X2 = x, Y1 = yTop, Y2 = yBottom, Stroke = majorBrush, StrokeThickness = 0.6 };
                ProfileCanvas.Children.Add(line);
                var lbl = new TextBlock
                {
                    Text = FormatStation(k),
                    Foreground = (Brush)FindResource("Brush_TextSecondary"),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, x - lbl.DesiredSize.Width / 2);
                Canvas.SetTop(lbl, yBottom + 2);
                ProfileCanvas.Children.Add(lbl);
            }
            // 高程水平网格：每 1 m
            double hStep = (hMax - hMin) > 20 ? 2.0 : 1.0;
            for (double h = Math.Ceiling(hMin / hStep) * hStep; h <= hMax + 1e-6; h += hStep)
            {
                double y = SyOfH(h);
                var line = new Line { X1 = xLeft, X2 = xRight, Y1 = y, Y2 = y, Stroke = majorBrush, StrokeThickness = 0.5, Opacity = 0.7 };
                ProfileCanvas.Children.Add(line);
                var lbl = new TextBlock
                {
                    Text = h.ToString("F1", CultureInfo.InvariantCulture),
                    Foreground = (Brush)FindResource("Brush_TextSecondary"),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, xLeft - lbl.DesiredSize.Width - 4);
                Canvas.SetTop(lbl, y - lbl.DesiredSize.Height / 2);
                ProfileCanvas.Children.Add(lbl);
            }

            // 坐标轴框
            var frame = new Rectangle
            {
                Width = plotW,
                Height = plotH,
                Stroke = axisBrush,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
            };
            Canvas.SetLeft(frame, xLeft);
            Canvas.SetTop(frame, yTop);
            ProfileCanvas.Children.Add(frame);
        }

        private void DrawEg(Func<double, double> SxOfK, Func<double, double> SyOfH)
        {
            var samples = _viewModel.ExistingGround.Samples;
            if (samples.Count < 2) return;

            var poly = new Polyline
            {
                Stroke = (Brush)FindResource("Brush_TextSecondary"),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Opacity = 0.95,
            };
            var pts = new PointCollection(samples.Count);
            foreach (var s in samples) pts.Add(new Point(SxOfK(s.Station), SyOfH(s.Elevation)));
            poly.Points = pts;
            ProfileCanvas.Children.Add(poly);

            // 图例（右上）
            var legend = new TextBlock
            {
                Text = "—— EG 地面线（黑虚）",
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontSize = 10,
            };
            Canvas.SetLeft(legend, ProfileCanvas.ActualWidth - 170);
            Canvas.SetTop(legend, ProfileMarginTop - 4);
            ProfileCanvas.Children.Add(legend);
        }

        private void DrawFg(Func<double, double> SxOfK, Func<double, double> SyOfH)
        {
            var pvis = _viewModel.DesignProfile.Pvis;
            if (pvis.Count < 2) return;

            var sorted = pvis.OrderBy(p => p.Station).ToList();
            var poly = new Polyline
            {
                Stroke = (Brush)FindResource("Brush_AccentBlue"),
                StrokeThickness = 2,
                Opacity = 0.95,
            };
            var pts = new PointCollection(sorted.Count);
            foreach (var p in sorted) pts.Add(new Point(SxOfK(p.Station), SyOfH(p.Elevation)));
            poly.Points = pts;
            ProfileCanvas.Children.Add(poly);

            var legend = new TextBlock
            {
                Text = "—— FG 设计线（蓝实）",
                Foreground = (Brush)FindResource("Brush_AccentBlue"),
                FontSize = 10,
            };
            Canvas.SetLeft(legend, ProfileCanvas.ActualWidth - 170);
            Canvas.SetTop(legend, ProfileMarginTop + 10);
            ProfileCanvas.Children.Add(legend);
        }

        private void DrawPviMarkers(Func<double, double> SxOfK, Func<double, double> SyOfH)
        {
            var pvis = _viewModel.DesignProfile.Pvis.OrderBy(p => p.Station).ToList();
            if (pvis.Count == 0) return;

            var redBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x50, 0x50));
            var accentBrush = (Brush)FindResource("Brush_AccentBlue");

            foreach (var p in pvis)
            {
                double x = SxOfK(p.Station);
                double y = SyOfH(p.Elevation);

                // 选中外框
                if (ReferenceEquals(_viewModel.SelectedPvi, p))
                {
                    var outline = new Ellipse
                    {
                        Width = 14, Height = 14,
                        Stroke = accentBrush,
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent,
                    };
                    Canvas.SetLeft(outline, x - 7);
                    Canvas.SetTop(outline, y - 7);
                    ProfileCanvas.Children.Add(outline);
                }

                var dot = new Ellipse
                {
                    Width = 7, Height = 7,
                    Fill = redBrush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5,
                };
                Canvas.SetLeft(dot, x - 3.5);
                Canvas.SetTop(dot, y - 3.5);
                ProfileCanvas.Children.Add(dot);

                // 标签：K桩号 / H=... / R=...（R>0 才显示）
                string lblText = p.CurveRadius > 0
                    ? $"{FormatStation(p.Station)}\nH={p.Elevation:F2}  R={p.CurveRadius:F0}"
                    : $"{FormatStation(p.Station)}\nH={p.Elevation:F2}";

                var tb = new TextBlock
                {
                    Text = lblText,
                    Foreground = (Brush)FindResource("Brush_TextPrimary"),
                    Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x20, 0x20, 0x20)),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(3, 1, 3, 1),
                    TextAlignment = TextAlignment.Left,
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double tbX = x + 6;
                double tbY = y - tb.DesiredSize.Height - 6;
                // 边界保护
                double rightLimit = ProfileCanvas.ActualWidth - tb.DesiredSize.Width - 4;
                if (tbX > rightLimit) tbX = rightLimit;
                if (tbY < 2) tbY = y + 6;
                Canvas.SetLeft(tb, tbX);
                Canvas.SetTop(tb, tbY);
                ProfileCanvas.Children.Add(tb);
            }
        }

        // =========================================================================
        //  BandSet Canvas：仅渲染 IsEnabled=true 的行，按桩号主格（每 100m）列对齐
        // =========================================================================

        private void RenderBandSet()
        {
            if (BandSetCanvas == null) return;
            BandSetCanvas.Children.Clear();

            double canvasW = BandSetCanvas.ActualWidth;
            double canvasH = BandSetCanvas.ActualHeight;
            if (canvasW < 80 || canvasH < 30) return;

            double totalLen = _viewModel.AlignmentLength;
            if (totalLen <= 0)
            {
                DrawEmptyHint(BandSetCanvas, canvasW, canvasH, "（路线长度为 0）");
                return;
            }

            var enabledRows = _viewModel.BandSet.Bands.Where(b => b.IsEnabled).ToList();
            if (enabledRows.Count == 0)
            {
                DrawEmptyHint(BandSetCanvas, canvasW, canvasH, "（数据条全部关闭，在右侧「数据条配置」勾选行）");
                return;
            }

            double startK = _viewModel.Alignment.StartStation;
            double endK = _viewModel.Alignment.EndStation;

            double labelW = BandLabelWidth;
            double dataX = labelW + 8;
            double dataW = Math.Max(20, canvasW - dataX - 8);

            double SxOfK(double k)
                => dataX + (k - startK) / (endK - startK) * dataW;

            // 行标背景（左侧 labelBg）
            var labelBg = new Rectangle
            {
                Width = labelW + 4,
                Height = canvasH,
                Fill = (Brush)FindResource("Brush_HeaderBack"),
            };
            Canvas.SetLeft(labelBg, 0);
            Canvas.SetTop(labelBg, 0);
            BandSetCanvas.Children.Add(labelBg);

            // 桩号分隔竖线 + 顶部桩号
            var gridBrush = (Brush)FindResource("Brush_BorderDark");
            for (double k = Math.Ceiling(startK / 100) * 100; k <= endK + 1e-3; k += 100)
            {
                double x = SxOfK(k);
                var v = new Line
                {
                    X1 = x, X2 = x, Y1 = 0, Y2 = canvasH,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5,
                    Opacity = 0.6,
                };
                BandSetCanvas.Children.Add(v);
            }

            // 逐行渲染
            double rowY = 0;
            for (int i = 0; i < enabledRows.Count; i++)
            {
                var row = enabledRows[i];

                // 行分隔线（底部）
                var sep = new Line
                {
                    X1 = 0, X2 = canvasW,
                    Y1 = rowY + BandRowHeight, Y2 = rowY + BandRowHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5,
                    Opacity = 0.7,
                };
                BandSetCanvas.Children.Add(sep);

                // 行标（左）
                var labelTb = new TextBlock
                {
                    Text = row.DisplayTitle,
                    Foreground = (Brush)FindResource("Brush_TextSecondary"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                };
                labelTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(labelTb, 6);
                Canvas.SetTop(labelTb, rowY + (BandRowHeight - labelTb.DesiredSize.Height) / 2);
                BandSetCanvas.Children.Add(labelTb);

                // 行内容（按行类型分派）
                RenderBandRowContent(row, SxOfK, rowY, startK, endK);

                rowY += BandRowHeight;
                if (rowY > canvasH) break;
            }
        }

        private void RenderBandRowContent(V2BandRowNode row, Func<double, double> SxOfK,
            double rowY, double startK, double endK)
        {
            switch (row.Kind)
            {
                case V2BandRowKind.StationLabel:
                    RenderStationLabelBand(SxOfK, rowY, startK, endK);
                    break;
                case V2BandRowKind.DesignElevation:
                    RenderElevationBand(_viewModel.DesignProfile.Pvis
                        .OrderBy(p => p.Station)
                        .Select(p => (p.Station, p.Elevation)), SxOfK, rowY,
                        (Brush)FindResource("Brush_TextPrimary"));
                    break;
                case V2BandRowKind.ExistingElevation:
                    RenderElevationBand(_viewModel.ExistingGround.Samples
                        .Where((s, idx) => idx % 2 == 0)
                        .Select(s => (s.Station, s.Elevation)), SxOfK, rowY,
                        (Brush)FindResource("Brush_TextSecondary"));
                    break;
                case V2BandRowKind.DesignGradeAndVerticalCurve:
                    RenderGradeBand(SxOfK, rowY);
                    break;
                case V2BandRowKind.HorizontalCurve:
                    RenderHorizontalCurveStub(SxOfK, rowY, startK, endK);
                    break;
                case V2BandRowKind.Intersection:
                    RenderIntersectionStub(SxOfK, rowY);
                    break;
                case V2BandRowKind.Superelevation:
                case V2BandRowKind.PavementRise:
                case V2BandRowKind.ServiceRoadElevation:
                    RenderPlaceholderBand(SxOfK, rowY, startK, endK, "（原型占位）");
                    break;
            }
        }

        private void RenderStationLabelBand(Func<double, double> SxOfK, double rowY, double startK, double endK)
        {
            var brush = (Brush)FindResource("Brush_TextPrimary");
            for (double k = Math.Ceiling(startK / 100) * 100; k <= endK + 1e-3; k += 100)
            {
                double x = SxOfK(k);
                var tb = new TextBlock
                {
                    Text = FormatStation(k),
                    Foreground = brush,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, rowY + (BandRowHeight - tb.DesiredSize.Height) / 2);
                BandSetCanvas.Children.Add(tb);
            }
        }

        private void RenderElevationBand(IEnumerable<(double Station, double Elevation)> points,
            Func<double, double> SxOfK, double rowY, Brush textBrush)
        {
            foreach (var (s, h) in points)
            {
                double x = SxOfK(s);
                var tb = new TextBlock
                {
                    Text = h.ToString("F2", CultureInfo.InvariantCulture),
                    Foreground = textBrush,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, rowY + (BandRowHeight - tb.DesiredSize.Height) / 2);
                BandSetCanvas.Children.Add(tb);
            }
        }

        private void RenderGradeBand(Func<double, double> SxOfK, double rowY)
        {
            var pvis = _viewModel.DesignProfile.Pvis.OrderBy(p => p.Station).ToList();
            if (pvis.Count < 2) return;

            var accent = (Brush)FindResource("Brush_AccentBlue");
            var text = (Brush)FindResource("Brush_TextPrimary");

            // 逐段：[PVI_i, PVI_{i+1}]
            for (int i = 0; i < pvis.Count - 1; i++)
            {
                var a = pvis[i];
                var b = pvis[i + 1];
                double x1 = SxOfK(a.Station);
                double x2 = SxOfK(b.Station);
                double midX = (x1 + x2) / 2;
                double midY = rowY + BandRowHeight / 2;

                // 坡度线（水平）
                var seg = new Line
                {
                    X1 = x1 + 2, X2 = x2 - 2,
                    Y1 = midY, Y2 = midY,
                    Stroke = accent,
                    StrokeThickness = 1.5,
                };
                BandSetCanvas.Children.Add(seg);

                // 端部小竖线
                foreach (double xe in new[] { x1 + 2, x2 - 2 })
                {
                    var tick = new Line
                    {
                        X1 = xe, X2 = xe,
                        Y1 = midY - 4, Y2 = midY + 4,
                        Stroke = accent,
                        StrokeThickness = 1,
                    };
                    BandSetCanvas.Children.Add(tick);
                }

                double dx = b.Station - a.Station;
                double grade = dx > 1e-6 ? (b.Elevation - a.Elevation) / dx : 0;
                double L = dx;
                string lblText = string.Format(CultureInfo.InvariantCulture,
                    "i={0:+0.00%;-0.00%;0.00%} L={1:F2}", grade, L);

                var tb = new TextBlock
                {
                    Text = lblText,
                    Foreground = text,
                    Background = new SolidColorBrush(Color.FromArgb(0x90, 0x20, 0x20, 0x20)),
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(3, 0, 3, 0),
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, midX - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, midY - tb.DesiredSize.Height - 1);
                BandSetCanvas.Children.Add(tb);
            }
        }

        private void RenderHorizontalCurveStub(Func<double, double> SxOfK, double rowY, double startK, double endK)
        {
            // 占位：画一条水平细线 + 中间注"平曲线 R=3000 L=180"
            var brush = (Brush)FindResource("Brush_TextSecondary");
            double x1 = SxOfK(startK);
            double x2 = SxOfK(endK);
            double midX = (x1 + x2) / 2;
            double midY = rowY + BandRowHeight / 2;

            var line = new Line
            {
                X1 = x1 + 2, X2 = x2 - 2, Y1 = midY, Y2 = midY,
                Stroke = brush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
            };
            BandSetCanvas.Children.Add(line);

            var tb = new TextBlock
            {
                Text = "R=3000  L=180  α=30.6°",
                Foreground = brush,
                Background = new SolidColorBrush(Color.FromArgb(0x90, 0x20, 0x20, 0x20)),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Padding = new Thickness(3, 0, 3, 0),
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, midX - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, midY - tb.DesiredSize.Height / 2);
            BandSetCanvas.Children.Add(tb);
        }

        private void RenderIntersectionStub(Func<double, double> SxOfK, double rowY)
        {
            // 占位：在 K0+531.65 处一个交叉口箭头 + 标注
            double x = SxOfK(531.65);
            var brush = (Brush)FindResource("Brush_Warning");

            var arrow = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(x, rowY + 4),
                    new Point(x - 4, rowY + BandRowHeight - 4),
                    new Point(x + 4, rowY + BandRowHeight - 4),
                },
                Fill = brush,
                Stroke = brush,
                StrokeThickness = 0.5,
            };
            BandSetCanvas.Children.Add(arrow);

            var tb = new TextBlock
            {
                Text = "K0+531.65  交叉口",
                Foreground = brush,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x + 6);
            Canvas.SetTop(tb, rowY + (BandRowHeight - tb.DesiredSize.Height) / 2);
            BandSetCanvas.Children.Add(tb);
        }

        private void RenderPlaceholderBand(Func<double, double> SxOfK, double rowY, double startK, double endK, string hintText)
        {
            var brush = (Brush)FindResource("Brush_TextSecondary");
            double midX = (SxOfK(startK) + SxOfK(endK)) / 2;
            double midY = rowY + BandRowHeight / 2;
            var tb = new TextBlock
            {
                Text = hintText,
                Foreground = brush,
                FontSize = 10,
                Opacity = 0.6,
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, midX - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, midY - tb.DesiredSize.Height / 2);
            BandSetCanvas.Children.Add(tb);
        }

        // =========================================================================
        //  公用绘制
        // =========================================================================

        private void DrawTitleBar(double canvasW, string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            };
            Canvas.SetLeft(tb, 8);
            Canvas.SetTop(tb, 4);
            ProfileCanvas.Children.Add(tb);
        }

        private void DrawEmptyHint(Canvas canvas, double w, double h, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontSize = 12,
                Opacity = 0.7,
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, Math.Max(8, (w - tb.DesiredSize.Width) / 2));
            Canvas.SetTop(tb, Math.Max(8, (h - tb.DesiredSize.Height) / 2));
            canvas.Children.Add(tb);
        }

        /// <summary>
        /// 按 `K0+405.23` 的形式格式化桩号（水平桩号轴与 PVI 标签统一使用）。
        /// </summary>
        private static string FormatStation(double station)
        {
            if (station < 0) station = 0;
            int km = (int)(station / 1000);
            double rest = station - km * 1000;
            return string.Format(CultureInfo.InvariantCulture, "K{0}+{1:000.00}", km, rest);
        }
    }
}
