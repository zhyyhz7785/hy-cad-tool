using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 横断面 WPF 实时预览渲染器。
    ///
    /// <para>
    /// 抽象自 <see cref="CrossSectionDesignerWindow"/> 老窗口的 <c>RedrawPreview</c>，
    /// 改为可复用的纯函数：吃 <see cref="CrossSectionFigure"/>，吐 <see cref="Canvas"/> 子图元。
    /// 让旧窗口（Obsolete）与新 <c>CrossSectionDrawWindow</c> 共用同一套绘制规则。
    /// </para>
    ///
    /// <para>
    /// v2 关键修正：对 <see cref="TemplateComponentKind.Kerb"/> 的 panel 不再额外补"Y=0 底边"，
    /// 而是用其自身 L 形顶点直接闭合——与
    /// <c>CrossSectionGeometryGenerator</c> 生成的"SurfaceOuter→KerbBaseOuter→KerbTopOuter→KerbTopInner"
    /// 序列对齐，这样路牙凸起在预览里也能像 AutoCAD 出图一样正确呈现。
    /// </para>
    /// </summary>
    internal static class CrossSectionPreviewRenderer
    {
        // 颜色对齐 BlenderUI Brush_TextPrimary / Brush_Warning / Brush_AccentBlue / Brush_Success；
        // 这里 Canvas 内部图元用裸色（避免每次重绘从资源解析），与 BlenderUI 调色板手对齐：
        //   #E6E6E6 ≈ Brush_TextPrimary
        //   #E5734E ≈ Brush_Warning
        //   #4772B3 ≈ Brush_AccentBlue
        //   #A3BE8C ≈ Brush_Success
        public static readonly Brush OutlineBrush      = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        public static readonly Brush CenterLineBrush   = new SolidColorBrush(Color.FromRgb(0xE5, 0x73, 0x4E));
        public static readonly Brush DimensionBrush    = new SolidColorBrush(Color.FromRgb(0x47, 0x72, 0xB3));
        public static readonly Brush SlopeBrush        = new SolidColorBrush(Color.FromRgb(0xA3, 0xBE, 0x8C));
        public static readonly Brush TopLabelBrush     = new SolidColorBrush(Color.FromRgb(0xA3, 0xBE, 0x8C));
        public static readonly Brush AnnotationBrush   = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        public static readonly Brush BaseLineBrush     = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x23));

        /// <summary>七大条带类型在 Canvas 中的填色（半透明）。</summary>
        public static readonly IReadOnlyDictionary<TemplateComponentKind, Brush> DefaultPanelFills =
            new Dictionary<TemplateComponentKind, Brush>
            {
                [TemplateComponentKind.Pavement]     = new SolidColorBrush(Color.FromArgb(0x60, 0x4c, 0x8a, 0xb3)),
                [TemplateComponentKind.NonMotorized] = new SolidColorBrush(Color.FromArgb(0x60, 0x9a, 0xb0, 0x73)),
                [TemplateComponentKind.Sidewalk]     = new SolidColorBrush(Color.FromArgb(0x60, 0xcd, 0xb8, 0x7f)),
                [TemplateComponentKind.GreenStrip]   = new SolidColorBrush(Color.FromArgb(0x60, 0x6f, 0xa3, 0x74)),
                [TemplateComponentKind.Kerb]         = new SolidColorBrush(Color.FromArgb(0x90, 0x5e, 0x81, 0xac)),
                [TemplateComponentKind.Shoulder]     = new SolidColorBrush(Color.FromArgb(0x60, 0xa3, 0x91, 0x74)),
                [TemplateComponentKind.MedianStrip]  = new SolidColorBrush(Color.FromArgb(0x60, 0x7a, 0x9c, 0x6b)),
            };

        /// <summary>
        /// 将 <paramref name="figure"/> 投影到 <paramref name="canvas"/>。
        /// 调用方需在 <c>SizeChanged</c> / 数据变更时重新调用本方法（renderer 不持久订阅）。
        /// </summary>
        /// <param name="canvas">目标 Canvas，本方法会先 Clear 再绘制。</param>
        /// <param name="figure">待绘制的横断面图形对象，<c>null</c> 时仅 Clear 后返回。</param>
        /// <param name="scaleDenominator">右上角"比例 1:N"中的 N，用于信息标签；不影响实际投影。</param>
        /// <param name="panelFills">条带 Kind→Brush 的填色映射；<c>null</c> 时使用 <see cref="DefaultPanelFills"/>。</param>
        public static void Render(
            Canvas canvas,
            CrossSectionFigure figure,
            int? scaleDenominator,
            IReadOnlyDictionary<TemplateComponentKind, Brush> panelFills = null)
        {
            if (canvas == null) return;
            canvas.Children.Clear();
            if (figure == null) return;

            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            if (cw < 20 || ch < 20) return;

            const double padX = 40;
            const double padTop = 50;
            const double padBottom = 60;

            var vertices = figure.Vertices;
            var panels = figure.Panels;
            if (vertices.Count == 0) return;

            double xmin = vertices.Min(v => v.X);
            double xmax = vertices.Max(v => v.X);
            double ymin = vertices.Min(v => v.Y);
            double ymax = vertices.Max(v => v.Y);

            double spanX = Math.Max(1e-3, xmax - xmin);
            double spanY = Math.Max(0.3, ymax - ymin);

            double sx = (cw - padX * 2) / spanX;
            double sy = (ch - padTop - padBottom) / Math.Max(1.0, spanY);
            double yExaggerate = 8.0;
            double s = Math.Min(sx, sy * yExaggerate);
            if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0) return;

            double ox = cw / 2.0;
            double oy = ch - padBottom;

            Func<double, double, Point> map = (x, y) => new Point(ox + x * s, oy - y * yExaggerate * s);

            var fills = panelFills ?? DefaultPanelFills;

            // ============================== 1. 各条带 panel ==============================
            //   - 路牙 panel 顶点序列已经自闭合（L 形），直接闭合即可；
            //   - 其他 panel 用"顶部 + 底部 Y=0"两段闭合成实心分面。
            foreach (var panel in panels)
            {
                int i0 = Math.Max(0, panel.StartVertexIndex);
                int i1 = Math.Min(vertices.Count - 1, panel.EndVertexIndex);
                if (i1 - i0 < 1) continue;

                bool isKerbPanel = panel.Kind == TemplateComponentKind.Kerb;

                var poly = new Polygon
                {
                    Stroke = OutlineBrush,
                    StrokeThickness = 0.8,
                    Fill = fills.TryGetValue(panel.Kind, out var f) ? f : Brushes.Transparent,
                };
                for (int i = i0; i <= i1; i++)
                {
                    var v = vertices[i];
                    poly.Points.Add(map(v.X, v.Y));
                }
                if (!isKerbPanel)
                {
                    var last = vertices[i1];
                    var first = vertices[i0];
                    poly.Points.Add(map(last.X, 0));
                    poly.Points.Add(map(first.X, 0));
                }
                canvas.Children.Add(poly);
            }

            // ============================== 2. 顶部主轮廓折线 ==============================
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
                canvas.Children.Add(top);
            }

            // ============================== 3. 中心线 ==============================
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
            canvas.Children.Add(center);
            AddText(canvas, ox + 4, padTop - 18, "中心线", CenterLineBrush, 10);

            // ============================== 4. Y=0 基线 ==============================
            var baseLine = new Line
            {
                X1 = padX * 0.5,
                X2 = cw - padX * 0.5,
                Y1 = oy,
                Y2 = oy,
                Stroke = BaseLineBrush,
                StrokeThickness = 0.6,
                StrokeDashArray = new DoubleCollection(new[] { 1.0, 4.0 }),
            };
            canvas.Children.Add(baseLine);

            // ============================== 5. 横坡标签 ==============================
            foreach (var slope in figure.SlopeLabels)
            {
                var p = map(slope.PositionX, slope.PositionY);
                AddText(canvas, p.X - 15, p.Y - 16, slope.Text, SlopeBrush, 10);
            }

            // ============================== 6. 主尺寸链 ==============================
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
            canvas.Children.Add(dimLine);

            foreach (var seg in figure.DimensionSegments)
            {
                if (seg.Tier != 1) continue;
                double x1 = ox + seg.StartX * s;
                double x2 = ox + seg.EndX * s;
                canvas.Children.Add(new Line { X1 = x1, X2 = x1, Y1 = dimTickY1, Y2 = dimTickY2, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                canvas.Children.Add(new Line { X1 = x2, X2 = x2, Y1 = dimTickY1, Y2 = dimTickY2, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                double midX = (x1 + x2) / 2;
                AddText(canvas, midX - 15, dimTextY, seg.Text, DimensionBrush, 10);
            }

            // ============================== 7. 顶部条带名称 ==============================
            foreach (var top in figure.TopLabels)
            {
                double x = ox + top.CenterX * s;
                AddText(canvas, x - 20, padTop + 10, top.Text, TopLabelBrush, 10);
            }

            // ============================== 8. 左/右方向箭头 ==============================
            if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel)
                || !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
            {
                DrawOrientation(canvas, figure.Orientation);
            }

            // ============================== 9. 标题 ==============================
            if (!string.IsNullOrWhiteSpace(figure.Title.Text))
            {
                var title = new TextBlock
                {
                    Text = figure.Title.Text,
                    Foreground = AnnotationBrush,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                };
                Canvas.SetLeft(title, 8);
                Canvas.SetTop(title, 8);
                canvas.Children.Add(title);
            }

            // ============================== 10. 高度标签列 ==============================
            double heightCursorY = padTop + 32;
            foreach (var h in figure.HeightLabels)
            {
                AddText(canvas, 10, heightCursorY, h.Text, AnnotationBrush, 10);
                heightCursorY += 14;
            }

            // ============================== 11. 比例 / 路幅 信息 ==============================
            var info = new TextBlock
            {
                Text = string.Format(CultureInfo.InvariantCulture,
                    "比例 1:{0}  路幅 {1:F2} m",
                    scaleDenominator ?? 100,
                    spanX),
                Foreground = AnnotationBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
            };
            Canvas.SetRight(info, 8);
            Canvas.SetTop(info, 8);
            canvas.Children.Add(info);
        }

        private static void DrawOrientation(Canvas canvas, FigureOrientation orientation)
        {
            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            double y = ch - 22;

            AddText(canvas, 10, y - 10, orientation.LeftLabel, AnnotationBrush, 10);
            var la = new Polyline { Stroke = AnnotationBrush, StrokeThickness = 0.9 };
            la.Points.Add(new Point(40, y));
            la.Points.Add(new Point(75, y));
            canvas.Children.Add(la);
            var lh = new Polygon { Fill = AnnotationBrush };
            lh.Points.Add(new Point(40, y));
            lh.Points.Add(new Point(48, y - 3));
            lh.Points.Add(new Point(48, y + 3));
            canvas.Children.Add(lh);

            AddText(canvas, cw - 60, y - 10, orientation.RightLabel, AnnotationBrush, 10);
            var ra = new Polyline { Stroke = AnnotationBrush, StrokeThickness = 0.9 };
            ra.Points.Add(new Point(cw - 75, y));
            ra.Points.Add(new Point(cw - 40, y));
            canvas.Children.Add(ra);
            var rh = new Polygon { Fill = AnnotationBrush };
            rh.Points.Add(new Point(cw - 40, y));
            rh.Points.Add(new Point(cw - 48, y - 3));
            rh.Points.Add(new Point(cw - 48, y + 3));
            canvas.Children.Add(rh);
        }

        private static void AddText(Canvas canvas, double x, double y, string text, Brush brush, double size)
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
            canvas.Children.Add(tb);
        }
    }
}
