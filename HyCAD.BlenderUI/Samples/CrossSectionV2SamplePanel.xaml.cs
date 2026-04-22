using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// rCs 横断面 v2 面板原型（Blender 样板）。
    ///
    /// <para>
    /// 三列骨架：左 BlenderOutliner（动态树：中央隔离带 + 左右侧根 + 条带 + 结构层）|
    /// 中 Canvas 轻量预览（按 Side/Kind/Width 映射色带）|
    /// 右 PropertyEditor + 三级 PropertyGroup（Level1/2/3），按节点类型切换内容。
    /// </para>
    ///
    /// <para>
    /// 本 sample 不引用 HyCADTool.Refactored / AutoCAD，命名与层级对齐
    /// <c>CrossSectionBand</c> / <c>CrossSectionDrawViewModel</c>，便于后续平移回 rCs 真窗口。
    /// </para>
    /// </summary>
    public partial class CrossSectionV2SamplePanel : BlenderWindow
    {
        private readonly V2CrossSectionSampleViewModel _viewModel;

        public CrossSectionV2SamplePanel()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);

            _viewModel = new V2CrossSectionSampleViewModel();
            DataContext = _viewModel;

            _viewModel.PreviewRequested += (_, __) => RenderPreview();
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(V2CrossSectionSampleViewModel.SelectedNode)
                    || e.PropertyName == nameof(V2CrossSectionSampleViewModel.ScaleDenominator))
                {
                    RenderPreview();
                }
            };

            Loaded += (_, __) => RenderPreview();
        }

        private void OutlinerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedNode = e.NewValue;
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderPreview();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestClose(null);
        }

        // =========================================================================
        //  轻量预览：按条带顺序 + 类型颜色渲染底部色带 + 顶部宽度标注
        // =========================================================================

        private void RenderPreview()
        {
            if (PreviewCanvas == null) return;
            PreviewCanvas.Children.Clear();

            double canvasW = PreviewCanvas.ActualWidth;
            double canvasH = PreviewCanvas.ActualHeight;
            if (canvasW < 20 || canvasH < 20) return;

            double total = _viewModel.TotalWidth;
            if (total <= 0)
            {
                RenderEmpty(canvasW, canvasH);
                return;
            }

            // 左到右物理顺序：左侧 reverse（从外到中） + 中央隔离带 + 右侧（从中到外）
            var ordered = new List<V2BandNode>();
            for (int i = _viewModel.LeftSide.Bands.Count - 1; i >= 0; i--)
                ordered.Add(_viewModel.LeftSide.Bands[i]);

            double marginH = 40;
            double marginV = 60;
            double usableW = Math.Max(10, canvasW - marginH * 2);
            double bandH = Math.Max(30, canvasH - marginV * 2);
            double scale = usableW / total;

            double x = marginH;
            double y = (canvasH - bandH) / 2;

            // 中心线占位（预留，画在最后）
            double centerlineX = 0;

            // 左半绘制
            foreach (var band in ordered)
            {
                double w = band.Width * scale;
                DrawBand(band, x, y, w, bandH);
                x += w;
            }

            // 中央隔离带
            if (_viewModel.Median.IsActive && _viewModel.Median.Width > 0)
            {
                double w = _viewModel.Median.Width * scale;
                DrawMedian(x, y, w, bandH);
                centerlineX = x + w / 2;
                x += w;
            }
            else
            {
                centerlineX = x;
            }

            // 右半绘制
            foreach (var band in _viewModel.RightSide.Bands)
            {
                double w = band.Width * scale;
                DrawBand(band, x, y, w, bandH);
                x += w;
            }

            // 中心线
            DrawCenterLine(centerlineX, y - 12, bandH + 24);

            // 顶部标题
            DrawTitle(canvasW, _viewModel.Title + $"   （1 : {_viewModel.ScaleDenominator}，总路幅 {total:F2} m）");
        }

        private void RenderEmpty(double w, double h)
        {
            var tb = new TextBlock
            {
                Text = "（当前断面为空，左侧大纲使用 ＋左 / ＋右 添加条带）",
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontSize = 13,
            };
            Canvas.SetLeft(tb, w / 2 - 170);
            Canvas.SetTop(tb, h / 2 - 8);
            PreviewCanvas.Children.Add(tb);
        }

        private void DrawBand(V2BandNode band, double x, double y, double w, double h)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(1, w),
                Height = h,
                Fill = BrushForKind(band.Kind, band.IsActive),
                Stroke = (Brush)FindResource("Brush_BorderDark"),
                StrokeThickness = 1,
                Opacity = band.IsActive ? 1 : 0.35,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);

            // 选中高亮
            if (ReferenceEquals(_viewModel.SelectedBand, band))
            {
                var outline = new Rectangle
                {
                    Width = Math.Max(1, w),
                    Height = h,
                    Stroke = (Brush)FindResource("Brush_AccentBlue"),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                };
                Canvas.SetLeft(outline, x);
                Canvas.SetTop(outline, y);
                PreviewCanvas.Children.Add(outline);
            }

            // 顶部宽度 + 名称（竖排）
            var label = new TextBlock
            {
                Text = $"{band.Name}\n{band.Width:F2} m",
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double lw = label.DesiredSize.Width;
            Canvas.SetLeft(label, x + Math.Max(0, (w - lw) / 2));
            Canvas.SetTop(label, y - 34);
            PreviewCanvas.Children.Add(label);

            // 结构层（底部堆叠示意）
            if (band.HasStructureLayers && band.StructureLayers.Count > 0)
            {
                double totalCm = 0;
                foreach (var layer in band.StructureLayers) totalCm += Math.Max(0.5, layer.ThicknessCm);
                if (totalCm <= 0) return;
                double stackH = Math.Min(h * 0.6, 80);
                double sy = y + h + 4;
                double cy = sy;
                foreach (var layer in band.StructureLayers)
                {
                    double lh = Math.Max(2, layer.ThicknessCm / totalCm * stackH);
                    var lr = new Rectangle
                    {
                        Width = Math.Max(1, w),
                        Height = lh,
                        Fill = BrushForLayer(layer.LayerKind),
                        Stroke = (Brush)FindResource("Brush_BorderDark"),
                        StrokeThickness = 0.6,
                    };
                    Canvas.SetLeft(lr, x);
                    Canvas.SetTop(lr, cy);
                    PreviewCanvas.Children.Add(lr);
                    cy += lh;
                }
            }
        }

        private void DrawMedian(double x, double y, double w, double h)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(1, w),
                Height = h,
                Fill = BrushForKind(V2BandKind.MedianStrip, true),
                Stroke = (Brush)FindResource("Brush_BorderDark"),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = $"中央隔离带\n{_viewModel.Median.Width:F2} m",
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double lw = label.DesiredSize.Width;
            Canvas.SetLeft(label, x + Math.Max(0, (w - lw) / 2));
            Canvas.SetTop(label, y - 34);
            PreviewCanvas.Children.Add(label);
        }

        private void DrawCenterLine(double x, double y, double h)
        {
            var line = new Line
            {
                X1 = x, X2 = x,
                Y1 = y, Y2 = y + h,
                Stroke = (Brush)FindResource("Brush_AccentBlue"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Opacity = 0.9,
            };
            PreviewCanvas.Children.Add(line);
        }

        private void DrawTitle(double canvasW, string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
            };
            Canvas.SetLeft(tb, 12);
            Canvas.SetTop(tb, 8);
            PreviewCanvas.Children.Add(tb);
        }

        private Brush BrushForKind(V2BandKind kind, bool active)
        {
            // 语义颜色；离线原型直接硬编码，避免引入额外资源项。
            switch (kind)
            {
                case V2BandKind.Pavement: return new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
                case V2BandKind.NonMotorized: return new SolidColorBrush(Color.FromRgb(0x4A, 0x56, 0x8C));
                case V2BandKind.Sidewalk: return new SolidColorBrush(Color.FromRgb(0x7B, 0x6A, 0x4F));
                case V2BandKind.GreenStrip: return new SolidColorBrush(Color.FromRgb(0x4A, 0x7A, 0x4C));
                case V2BandKind.Separator: return new SolidColorBrush(Color.FromRgb(0x6F, 0x8B, 0x6E));
                case V2BandKind.Kerb: return new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
                case V2BandKind.MedianStrip: return new SolidColorBrush(Color.FromRgb(0x55, 0x88, 0x55));
                default: return new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
            }
        }

        private Brush BrushForLayer(V2LayerKind kind)
        {
            switch (kind)
            {
                case V2LayerKind.Surface: return new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                case V2LayerKind.Base: return new SolidColorBrush(Color.FromRgb(0x6E, 0x55, 0x3C));
                case V2LayerKind.Subbase: return new SolidColorBrush(Color.FromRgb(0xA8, 0x98, 0x7A));
                default: return new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            }
        }
    }
}
