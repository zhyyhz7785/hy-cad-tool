using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 纵断面设计窗口（v1，B3 节点）。
    ///
    /// 由 <c>RoadProfileCommand</c> / <c>RoadProfileFgCommand</c> 通过
    /// <c>Application.ShowModalWindow</c> 打开，承载 <see cref="ProfileEditorViewModel"/>。
    ///
    /// 职责：
    /// - VM 作 DataContext；
    /// - 监听 <see cref="ProfileEditorViewModel.CloseRequested"/> 关闭窗口；
    /// - 把 VM <see cref="ProfileEditorViewModel.RenderSegments"/> 转成 <see cref="PathGeometry"/>
    ///   并按 Canvas 实际大小做世界坐标 → 屏幕坐标的等比变换；
    /// - 渲染 PVI 圆点 + 桩号文字 + 简单网格；
    /// - 注入 NonCompliantConfirm（弹 MessageBox）。
    ///
    /// 不直接引用 AutoCAD API；落盘 / 瞬态预览由命令层订阅 <see cref="ProfileEditorViewModel.Confirmed"/> 处理。
    /// </summary>
    public partial class ProfileEditorWindow : BlenderWindow
    {
        private ProfileEditorViewModel _vm;

        public ProfileEditorWindow()
        {
            InitializeComponent();
            CloseClicked += OnCloseClicked;
        }

        public ProfileEditorWindow(ProfileEditorViewModel viewModel) : this()
        {
            _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            viewModel.CloseRequested += OnCloseRequested;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.NonCompliantConfirm = summary => MessageBox.Show(
                this,
                summary,
                "存在未通过的规范项 / 几何告警",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) == MessageBoxResult.OK;

            Loaded += (_, __) => RenderProfileCanvas();
            Closed += (_, __) =>
            {
                viewModel.CloseRequested -= OnCloseRequested;
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                CloseClicked -= OnCloseClicked;
            };
        }

        // ============================ 事件 ============================

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            RequestClose(dialogResult);
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProfileEditorViewModel vm && vm.CancelCommand.CanExecute(null))
            {
                vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 任何会影响纵断面图的属性变化都重画一次：
            //   - RenderSegments 重生成 → 几何
            //   - Vertices 行内字段（PropertyChanged 转 nameof(Vertices)）→ PVI 标记
            if (e.PropertyName == nameof(ProfileEditorViewModel.RenderSegments)
                || e.PropertyName == nameof(ProfileEditorViewModel.Vertices))
            {
                RenderProfileCanvas();
            }
        }

        private void ProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderProfileCanvas();
        }

        // ============================ 渲染 ============================

        /// <summary>
        /// 把 VM 的 RenderSegments + PVI 列表渲染到 Canvas。
        ///
        /// 视口策略：
        /// - 世界 X 范围 = [minStation, maxStation]，Y 范围 = [minElev, maxElev]，
        ///   各加 5% padding；
        /// - Y 翻转（屏幕 Y 向下）；
        /// - 不强制等比，因为纵断面 X 几百米 vs Y 几米，等比会让线变成水平直线。
        /// </summary>
        private void RenderProfileCanvas()
        {
            if (_vm == null || ProfileCanvas == null || FgPath == null) return;
            double cw = ProfileCanvas.ActualWidth;
            double ch = ProfileCanvas.ActualHeight;
            if (cw < 4 || ch < 4) return;

            // === 1) 收集世界范围 ===
            var verts = _vm.SnapshotVertices();
            if (verts.Count < 2)
            {
                FgPath.Data = null;
                MarkersLayer.Children.Clear();
                LabelsLayer.Children.Clear();
                GridPath.Data = null;
                return;
            }

            double minS = verts[0].Station;
            double maxS = verts[verts.Count - 1].Station;
            double minE = double.MaxValue;
            double maxE = double.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                if (verts[i].Elevation < minE) minE = verts[i].Elevation;
                if (verts[i].Elevation > maxE) maxE = verts[i].Elevation;
            }
            // 同时把竖曲线 BCV/ECV 高程纳入视口（更鲁棒）
            if (_vm.LastFgResult != null)
            {
                foreach (var p in _vm.LastFgResult.Pvis)
                {
                    if (p.CurveLength <= 0) continue;
                    if (p.BcvElevation < minE) minE = p.BcvElevation;
                    if (p.EcvElevation > maxE) maxE = p.EcvElevation;
                }
            }

            double xRange = Math.Max(maxS - minS, 1e-6);
            double yRange = Math.Max(maxE - minE, 1.0); // 至少 1m，避免全部水平时塌成 0
            double xPad = xRange * 0.05;
            double yPad = yRange * 0.10;
            double worldMinX = minS - xPad;
            double worldMaxX = maxS + xPad;
            double worldMinY = minE - yPad;
            double worldMaxY = maxE + yPad;

            // 给左右两侧留出标签空间
            const double leftMargin = 50;   // Y 轴标签
            const double bottomMargin = 22; // X 轴标签
            const double topMargin = 8;
            const double rightMargin = 12;
            double drawW = Math.Max(cw - leftMargin - rightMargin, 1);
            double drawH = Math.Max(ch - topMargin - bottomMargin, 1);

            double sx = drawW / (worldMaxX - worldMinX);
            double sy = drawH / (worldMaxY - worldMinY);

            Point WorldToScreen(double ws, double we)
            {
                double x = leftMargin + (ws - worldMinX) * sx;
                double y = topMargin + (worldMaxY - we) * sy; // Y 翻转
                return new Point(x, y);
            }

            // === 2) 网格（每 5 个 X 间隔 / 每 ~5 个 Y 间隔）===
            var gridGeom = new PathGeometry();
            BuildGridLines(gridGeom, worldMinX, worldMaxX, worldMinY, worldMaxY, WorldToScreen);
            GridPath.Data = gridGeom;

            // === 3) FG 主线 ===
            var fgGeom = new PathGeometry();
            var rs = _vm.RenderSegments;
            if (rs.Count > 0)
            {
                var fig = new PathFigure
                {
                    StartPoint = WorldToScreen(rs[0].StartStation, rs[0].StartElevation),
                    IsClosed = false,
                    IsFilled = false,
                };
                foreach (var seg in rs)
                {
                    if (seg.Kind == ProfileRenderSegmentKind.Tangent)
                    {
                        fig.Segments.Add(new System.Windows.Media.LineSegment(
                            WorldToScreen(seg.EndStation, seg.EndElevation), true));
                    }
                    else
                    {
                        fig.Segments.Add(new QuadraticBezierSegment(
                            WorldToScreen(seg.ControlStation, seg.ControlElevation),
                            WorldToScreen(seg.EndStation, seg.EndElevation),
                            true));
                    }
                }
                fgGeom.Figures.Add(fig);
            }
            FgPath.Data = fgGeom;

            // === 4) PVI 标记 + 桩号标签 ===
            MarkersLayer.Children.Clear();
            LabelsLayer.Children.Clear();
            for (int i = 0; i < verts.Count; i++)
            {
                var p = WorldToScreen(verts[i].Station, verts[i].Elevation);
                var dot = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = (Brush)FindResource("Brush_Warning"),
                    Stroke = Brushes.White,
                    StrokeThickness = 0.8,
                };
                Canvas.SetLeft(dot, p.X - 3.5);
                Canvas.SetTop(dot, p.Y - 3.5);
                MarkersLayer.Children.Add(dot);

                // 桩号标签：每个 PVI 都标
                var label = new TextBlock
                {
                    Text = $"K{verts[i].Station.ToString("F0", CultureInfo.InvariantCulture)}",
                    Foreground = (Brush)FindResource("Brush_TextSecondary"),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                };
                Canvas.SetLeft(label, p.X + 5);
                Canvas.SetTop(label, p.Y - 14);
                LabelsLayer.Children.Add(label);
            }

            // === 5) Y 轴 / X 轴标签（左 / 下边）===
            DrawAxisLabels(worldMinX, worldMaxX, worldMinY, worldMaxY, WorldToScreen, leftMargin, bottomMargin, cw, ch);
        }

        /// <summary>简单的等距网格（约 6 段 X / 4 段 Y）。</summary>
        private static void BuildGridLines(
            PathGeometry geom,
            double wxMin, double wxMax, double wyMin, double wyMax,
            Func<double, double, Point> w2s)
        {
            int xN = 6;
            int yN = 4;
            for (int i = 0; i <= xN; i++)
            {
                double wx = wxMin + (wxMax - wxMin) * i / xN;
                var fig = new PathFigure { StartPoint = w2s(wx, wyMin), IsClosed = false, IsFilled = false };
                fig.Segments.Add(new System.Windows.Media.LineSegment(w2s(wx, wyMax), true));
                geom.Figures.Add(fig);
            }
            for (int j = 0; j <= yN; j++)
            {
                double wy = wyMin + (wyMax - wyMin) * j / yN;
                var fig = new PathFigure { StartPoint = w2s(wxMin, wy), IsClosed = false, IsFilled = false };
                fig.Segments.Add(new System.Windows.Media.LineSegment(w2s(wxMax, wy), true));
                geom.Figures.Add(fig);
            }
        }

        /// <summary>沿左侧画 Y 标签、沿底部画 X 标签。</summary>
        private void DrawAxisLabels(
            double wxMin, double wxMax, double wyMin, double wyMax,
            Func<double, double, Point> w2s,
            double leftMargin, double bottomMargin,
            double canvasW, double canvasH)
        {
            int xN = 6;
            int yN = 4;
            var brush = (Brush)FindResource("Brush_TextSecondary");

            for (int i = 0; i <= xN; i++)
            {
                double wx = wxMin + (wxMax - wxMin) * i / xN;
                var p = w2s(wx, wyMin);
                var t = new TextBlock
                {
                    Text = $"K{wx.ToString("F0", CultureInfo.InvariantCulture)}",
                    Foreground = brush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                };
                Canvas.SetLeft(t, p.X - 16);
                Canvas.SetTop(t, p.Y + 4);
                LabelsLayer.Children.Add(t);
            }
            for (int j = 0; j <= yN; j++)
            {
                double wy = wyMin + (wyMax - wyMin) * j / yN;
                var p = w2s(wxMin, wy);
                var t = new TextBlock
                {
                    Text = wy.ToString("F1", CultureInfo.InvariantCulture),
                    Foreground = brush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                };
                Canvas.SetLeft(t, 4);
                Canvas.SetTop(t, p.Y - 7);
                LabelsLayer.Children.Add(t);
            }
        }
    }
}
