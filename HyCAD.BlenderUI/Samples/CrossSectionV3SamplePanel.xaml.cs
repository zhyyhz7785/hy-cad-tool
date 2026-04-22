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
    /// rCs 横断面 v3 建模工作台原型。
    ///
    /// <para>
    /// 相对 v2，这个样板刻意把左侧大纲重构成三层建模树：
    /// 板块（第一层） -> 结构组（第二层） -> 结构层（第三层）。
    /// 右侧属性面板也改成"按当前节点上下文切换"的工作台，而不是把全局和局部字段混在一起。
    /// </para>
    ///
    /// <para>
    /// 当前仍是 BlenderUI 样板，不直接连 AutoCAD；模型空间预览、图上拾取、AutoCAD 标注与标高引线
    /// 只先在 UI 上预留语义与字段。
    /// </para>
    /// </summary>
    public partial class CrossSectionV3SamplePanel : BlenderWindow
    {
        private readonly V3CrossSectionSampleViewModel _viewModel;

        public CrossSectionV3SamplePanel()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);

            _viewModel = new V3CrossSectionSampleViewModel();
            DataContext = _viewModel;

            _viewModel.PreviewRequested += (_, __) => RenderPreview();
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(V3CrossSectionSampleViewModel.SelectedNode)
                    || e.PropertyName == nameof(V3CrossSectionSampleViewModel.ScaleDenominator)
                    || e.PropertyName == nameof(V3CrossSectionSampleViewModel.PreferModelSpacePreview)
                    || e.PropertyName == nameof(V3CrossSectionSampleViewModel.UseAutoCadDimension)
                    || e.PropertyName == nameof(V3CrossSectionSampleViewModel.UseElevationLeader))
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

        private void RenderPreview()
        {
            if (PreviewCanvas == null) return;
            PreviewCanvas.Children.Clear();

            double canvasW = PreviewCanvas.ActualWidth;
            double canvasH = PreviewCanvas.ActualHeight;
            if (canvasW < 24 || canvasH < 24) return;

            double total = _viewModel.TotalWidth;
            if (total <= 0)
            {
                RenderEmpty(canvasW, canvasH);
                return;
            }

            double marginX = 40;
            double marginTop = 74;
            double marginBottom = 42;
            double usableW = Math.Max(20, canvasW - marginX * 2);
            double bandTop = marginTop;
            double bandHeight = Math.Max(58, Math.Min(120, canvasH * 0.22));
            double structureGap = 12;
            double structureTop = bandTop + bandHeight + structureGap;
            double structureHeight = Math.Max(48, canvasH - structureTop - marginBottom - 18);
            double scale = usableW / total;

            DrawTitle(canvasW);

            var orderedBands = new List<V3BandNode>();
            for (int i = _viewModel.LeftSide.Bands.Count - 1; i >= 0; i--)
                orderedBands.Add(_viewModel.LeftSide.Bands[i]);

            double x = marginX;
            double centerlineX;

            foreach (var band in orderedBands)
            {
                double width = band.WidthM * scale;
                DrawBand(band, x, bandTop, width, bandHeight, structureTop, structureHeight);
                x += width;
            }

            if (_viewModel.Median.IsActive && _viewModel.Median.WidthM > 0)
            {
                double width = _viewModel.Median.WidthM * scale;
                DrawMedian(x, bandTop, width, bandHeight);
                centerlineX = x + width / 2;
                x += width;
            }
            else
            {
                centerlineX = x;
            }

            foreach (var band in _viewModel.RightSide.Bands)
            {
                double width = band.WidthM * scale;
                DrawBand(band, x, bandTop, width, bandHeight, structureTop, structureHeight);
                x += width;
            }

            DrawCenterLine(centerlineX, bandTop - 10, structureTop + structureHeight - bandTop + 14);
            DrawSelectionTag(canvasW);
            DrawFooterHint(canvasW, canvasH);
        }

        private void RenderEmpty(double width, double height)
        {
            var tip = new TextBlock
            {
                Text = "（当前断面为空，左侧使用 ＋左 / ＋右 或右侧『快速插入板块』开始建模）",
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontSize = 13,
            };
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tip, Math.Max(12, width / 2 - tip.DesiredSize.Width / 2));
            Canvas.SetTop(tip, Math.Max(12, height / 2 - tip.DesiredSize.Height / 2));
            PreviewCanvas.Children.Add(tip);
        }

        private void DrawBand(
            V3BandNode band,
            double x,
            double bandTop,
            double width,
            double bandHeight,
            double structureTop,
            double structureHeight)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(1, width),
                Height = bandHeight,
                Fill = BrushForKind(band.Kind, band.IsActive),
                Stroke = (Brush)FindResource("Brush_BorderDark"),
                StrokeThickness = 1,
                Opacity = band.IsActive ? 1.0 : 0.35,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, bandTop);
            PreviewCanvas.Children.Add(rect);

            if (ReferenceEquals(_viewModel.ActiveBand, band))
            {
                var outline = new Rectangle
                {
                    Width = Math.Max(1, width),
                    Height = bandHeight,
                    Stroke = (Brush)FindResource("Brush_AccentBlue"),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                };
                Canvas.SetLeft(outline, x);
                Canvas.SetTop(outline, bandTop);
                PreviewCanvas.Children.Add(outline);
            }

            DrawBandLabel(band, x, bandTop, width, bandHeight);
            DrawStructureStack(band, x, structureTop, width, structureHeight);
        }

        private void DrawBandLabel(V3BandNode band, double x, double y, double width, double height)
        {
            var label = new TextBlock
            {
                Text = string.Format("{0}{1}  {2:F2}m", band.KindShortLabel, band.OrderIndex, band.WidthM),
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x + 4);
            Canvas.SetTop(label, y + 4);
            PreviewCanvas.Children.Add(label);

            if (width < 46) return;

            var name = new TextBlock
            {
                Text = band.Name,
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(24, width - 8),
            };
            Canvas.SetLeft(name, x + 4);
            Canvas.SetTop(name, y + 24);
            PreviewCanvas.Children.Add(name);

            var hint = new TextBlock
            {
                Text = string.Format("{0:F1}% | {1}", band.SlopePct, band.GeometrySourceLabel),
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontSize = 9,
                Width = Math.Max(24, width - 8),
                TextWrapping = TextWrapping.Wrap,
            };
            Canvas.SetLeft(hint, x + 4);
            Canvas.SetTop(hint, y + height - 26);
            PreviewCanvas.Children.Add(hint);
        }

        private void DrawStructureStack(V3BandNode band, double x, double top, double width, double height)
        {
            if (!band.HasStructureGroups || band.TotalStructureThicknessM <= 0) return;

            double totalThickness = band.TotalStructureThicknessM;
            double currentY = top;

            foreach (var group in band.LayerGroups)
            {
                double groupThickness = Math.Max(0.001, group.TotalThicknessM);
                double groupHeight = Math.Max(6, height * groupThickness / totalThickness);
                double groupStartY = currentY;
                double layerOffset = 0;

                for (int i = 0; i < group.Layers.Count; i++)
                {
                    var layer = group.Layers[i];
                    double layerHeight = i == group.Layers.Count - 1
                        ? Math.Max(4, groupHeight - layerOffset)
                        : Math.Max(4, groupHeight * layer.ThicknessM / groupThickness);

                    var rect = new Rectangle
                    {
                        Width = Math.Max(1, width),
                        Height = layerHeight,
                        Fill = BrushForGroup(group.GroupKind),
                        Stroke = (Brush)FindResource("Brush_BorderDark"),
                        StrokeThickness = 0.6,
                        Opacity = band.IsActive ? 1.0 : 0.35,
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, currentY);
                    PreviewCanvas.Children.Add(rect);

                    if (ReferenceEquals(_viewModel.SelectedLayer, layer))
                    {
                        var outline = new Rectangle
                        {
                            Width = Math.Max(1, width),
                            Height = layerHeight,
                            Stroke = (Brush)FindResource("Brush_AccentBlue"),
                            StrokeThickness = 2,
                            Fill = Brushes.Transparent,
                        };
                        Canvas.SetLeft(outline, x);
                        Canvas.SetTop(outline, currentY);
                        PreviewCanvas.Children.Add(outline);
                    }

                    if (width >= 64 && layerHeight >= 18)
                    {
                        var text = new TextBlock
                        {
                            Text = layer.Name,
                            Foreground = (Brush)FindResource("Brush_TextPrimary"),
                            FontSize = 9,
                            Width = Math.Max(24, width - 8),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        };
                        Canvas.SetLeft(text, x + 4);
                        Canvas.SetTop(text, currentY + 1);
                        PreviewCanvas.Children.Add(text);
                    }

                    currentY += layerHeight;
                    layerOffset += layerHeight;
                }

                if (ReferenceEquals(_viewModel.ActiveGroup, group)
                    || ReferenceEquals(_viewModel.SelectedLayer?.ParentGroup, group))
                {
                    var groupOutline = new Rectangle
                    {
                        Width = Math.Max(1, width),
                        Height = Math.Max(4, currentY - groupStartY),
                        Stroke = (Brush)FindResource("Brush_AccentBlue"),
                        StrokeThickness = 1.4,
                        Fill = Brushes.Transparent,
                    };
                    Canvas.SetLeft(groupOutline, x);
                    Canvas.SetTop(groupOutline, groupStartY);
                    PreviewCanvas.Children.Add(groupOutline);
                }
            }
        }

        private void DrawMedian(double x, double y, double width, double height)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(1, width),
                Height = height,
                Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x88, 0x55)),
                Stroke = (Brush)FindResource("Brush_BorderDark"),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            PreviewCanvas.Children.Add(rect);

            if (ReferenceEquals(_viewModel.SelectedMedian, _viewModel.Median))
            {
                var outline = new Rectangle
                {
                    Width = Math.Max(1, width),
                    Height = height,
                    Stroke = (Brush)FindResource("Brush_AccentBlue"),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                };
                Canvas.SetLeft(outline, x);
                Canvas.SetTop(outline, y);
                PreviewCanvas.Children.Add(outline);
            }

            var label = new TextBlock
            {
                Text = string.Format("中央隔离带\n{0:F2} m", _viewModel.Median.WidthM),
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Width = Math.Max(40, width),
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y + 8);
            PreviewCanvas.Children.Add(label);
        }

        private void DrawCenterLine(double x, double y, double height)
        {
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = y,
                Y2 = y + height,
                Stroke = (Brush)FindResource("Brush_AccentBlue"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Opacity = 0.9,
            };
            PreviewCanvas.Children.Add(line);
        }

        private void DrawTitle(double canvasWidth)
        {
            string previewTarget = _viewModel.PreferModelSpacePreview ? "目标预览：模型空间" : "目标预览：WPF";
            string annotation = _viewModel.UseAutoCadDimension ? "AutoCAD 标注" : "手绘尺寸链";
            string elevation = _viewModel.UseElevationLeader ? "标高引线" : "纯文本标高";

            var title = new TextBlock
            {
                Text = string.Format(
                    "{0}   |   1:{1}   |   总路幅 {2:F2} m   |   {3} / {4} / {5}",
                    _viewModel.Title,
                    _viewModel.ScaleDenominator,
                    _viewModel.TotalWidth,
                    previewTarget,
                    annotation,
                    elevation),
                Foreground = (Brush)FindResource("Brush_TextPrimary"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
            };
            Canvas.SetLeft(title, 12);
            Canvas.SetTop(title, 10);
            PreviewCanvas.Children.Add(title);
        }

        private void DrawSelectionTag(double canvasWidth)
        {
            var tag = new TextBlock
            {
                Text = _viewModel.SelectionPath,
                Foreground = (Brush)FindResource("Brush_AccentBlue"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
            };
            tag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tag, Math.Max(12, canvasWidth - tag.DesiredSize.Width - 12));
            Canvas.SetTop(tag, 34);
            PreviewCanvas.Children.Add(tag);
        }

        private void DrawFooterHint(double canvasWidth, double canvasHeight)
        {
            var tip = new TextBlock
            {
                Text = "v3 重点：左树内联宽度 + 右侧按节点编辑 + 第一/第二/第三层显式拆开",
                Foreground = (Brush)FindResource("Brush_TextSecondary"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
            };
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tip, Math.Max(12, canvasWidth - tip.DesiredSize.Width - 12));
            Canvas.SetTop(tip, Math.Max(12, canvasHeight - tip.DesiredSize.Height - 10));
            PreviewCanvas.Children.Add(tip);
        }

        private Brush BrushForKind(V3BandKind kind, bool active)
        {
            Brush brush;
            switch (kind)
            {
                case V3BandKind.Pavement:
                    brush = new SolidColorBrush(Color.FromRgb(0x36, 0x36, 0x36));
                    break;
                case V3BandKind.NonMotorized:
                    brush = new SolidColorBrush(Color.FromRgb(0x4B, 0x5F, 0x95));
                    break;
                case V3BandKind.Sidewalk:
                    brush = new SolidColorBrush(Color.FromRgb(0x80, 0x6B, 0x4A));
                    break;
                case V3BandKind.GreenStrip:
                    brush = new SolidColorBrush(Color.FromRgb(0x4A, 0x7A, 0x4C));
                    break;
                case V3BandKind.Separator:
                    brush = new SolidColorBrush(Color.FromRgb(0x6B, 0x7B, 0x82));
                    break;
                default:
                    brush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                    break;
            }

            if (!active) brush.Opacity = 0.45;
            return brush;
        }

        private Brush BrushForGroup(V3LayerGroupKind kind)
        {
            switch (kind)
            {
                case V3LayerGroupKind.Surface:
                    return new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                case V3LayerGroupKind.Base:
                    return new SolidColorBrush(Color.FromRgb(0x7B, 0x5C, 0x3A));
                case V3LayerGroupKind.Subbase:
                    return new SolidColorBrush(Color.FromRgb(0xA7, 0x96, 0x73));
                default:
                    return new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            }
        }
    }
}
