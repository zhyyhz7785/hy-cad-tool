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
using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
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
        /// <summary>与 rCs 方位指示层一致的浅紫，用于预览中北南箭线（工程图为白+图层色）。</summary>
        public static readonly Brush OrientationLineBrush = new SolidColorBrush(Color.FromRgb(0xCE, 0x93, 0xD8));
        public static readonly Brush RedAxisBrush       = new SolidColorBrush(Color.FromRgb(0xE5, 0x55, 0x55));

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
        /// <param name="layout">与出图 <c>DrawPlanStrip</c> 相同的布置；<c>null</c> 时不画平面分隔带示意，仍按 <paramref name="planStripVerticalOffsetM"/> 抬升尺寸/上排注记。</param>
        /// <param name="planStripVerticalOffsetM">与 <c>RoadStandardSectionDrawService</c> 的 <c>planStripVerticalOffsetMeters</c> 同义（图面 m，相对路顶+1.2m）。</param>
        /// <param name="drawSheetTitleBand">为 <c>false</c> 时不绘顶栏图题带（仅影响 WPF 预览）。</param>
        /// <param name="drawRoadWidthCornerBadge">为 <c>false</c> 时不绘右上角「路幅 … m」角标（仅影响 WPF 预览）。</param>
        public static void Render(
            Canvas canvas,
            CrossSectionFigure figure,
            int? scaleDenominator,
            IReadOnlyDictionary<TemplateComponentKind, Brush> panelFills = null,
            DrawingSheetTitleSpec titleSpec = null,
            CrossSectionLayout layout = null,
            double planStripVerticalOffsetM = 5.0,
            Action onOrientationToggle = null,
            bool drawSheetTitleBand = true,
            bool drawRoadWidthCornerBadge = true)
        {
            if (canvas == null) return;
            canvas.Children.Clear();
            if (figure == null) return;
            if (double.IsNaN(planStripVerticalOffsetM) || double.IsInfinity(planStripVerticalOffsetM)) return;

            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            if (cw < 20 || ch < 20) return;

            // 独立窗口预览保留较大边距与完整 rCs 竖向尺度；Palette 内嵌预览（无图题/无路幅角标）在「尽量充满」与「不裁切注记」之间折中：
            // 底排尺寸字、坡度字、顶缘方位字会占用画布像素，边距过小会被 ClipToBounds 吃掉。
            bool compactPreview = !drawSheetTitleBand && !drawRoadWidthCornerBadge;
            double padX = compactPreview ? 14.0 : 40.0;
            double padTop = compactPreview ? 16.0 : 50.0;
            double padBottom = compactPreview ? 26.0 : 60.0;
            const double rcsTopAxisAndOrientationM = 5.0;
            // 与 RoadStandardSectionDrawService 中 axisBottomY / planStrip* 计算对齐
            const double planGapAboveCrownM = 1.2;

            var vertices = figure.Vertices;
            var panels = figure.Panels;
            if (vertices.Count == 0) return;

            double xmin = vertices.Min(v => v.X);
            double xmax = vertices.Max(v => v.X);
            double ymin = vertices.Min(v => v.Y);
            double ymax = vertices.Max(v => v.Y);

            double planOffM = planStripVerticalOffsetM;
            double axisRiseM = rcsTopAxisAndOrientationM;
            if (compactPreview)
            {
                planOffM = Math.Max(1.0, planStripVerticalOffsetM * 0.52);
                axisRiseM = 3.25;
            }

            double planStripBottomY = ymax + planGapAboveCrownM + planOffM;
            double planStripTopY = planStripBottomY + Math.Max(0.5, figure.PlanStripLength);
            double axisBottomY = ymin - 2.8;
            double axisTopY = planStripTopY + axisRiseM;
            double orientationY = planStripTopY + axisRiseM;
            // 板块字改为"平面带竖向居中"（而非上方 2.5m），与用户要求一致。
            double topLabelY = 0.5 * (planStripBottomY + planStripTopY);

            double yMaxFit = Math.Max(ymax, axisTopY);
            yMaxFit = Math.Max(yMaxFit, topLabelY + 0.2);
            yMaxFit = Math.Max(yMaxFit, orientationY + 0.1);
            if (drawSheetTitleBand && !string.IsNullOrWhiteSpace(figure.Title.Text))
                yMaxFit = Math.Max(yMaxFit, figure.Title.Y + 0.1);

            double spanX = Math.Max(1e-3, xmax - xmin);

            // layout 路径：竖向包络须含底排尺寸链（模型 y 常低于 ymin）、轴线底、横坡注记，否则 sy 偏大导致底/顶字被 ClipToBounds 裁切。
            double yMinFit = ymin;
            double yMaxForScale = yMaxFit;
            if (layout != null)
            {
                yMinFit = Math.Min(ymin, axisBottomY);
                const double fitBoxPreviewTxtH = 0.3;
                const double fitBoxDimDropM = 0.5;
                double fitBoxDimRowGap = Math.Max(0.45, fitBoxPreviewTxtH * 1.9);
                foreach (var seg in figure.DimensionSegments)
                {
                    double featureY;
                    double dimOffset;
                    if (seg.Track == FigureDimensionTrack.Top)
                    {
                        featureY = planStripTopY - fitBoxDimDropM;
                        dimOffset = (seg.Tier == 0 ? fitBoxDimRowGap * 3.2 : fitBoxDimRowGap);
                    }
                    else
                    {
                        featureY = 0.0 - fitBoxDimDropM;
                        dimOffset = (seg.Tier == 0 ? -fitBoxDimRowGap * 3.2 : -fitBoxDimRowGap);
                    }
                    double yModel = featureY + dimOffset;
                    if (seg.Track == FigureDimensionTrack.Top)
                        yModel += fitBoxDimRowGap;
                    yMinFit = Math.Min(yMinFit, yModel);
                    yMaxForScale = Math.Max(yMaxForScale, yModel);
                }

                foreach (var sl in figure.SlopeLabels)
                {
                    yMinFit = Math.Min(yMinFit, sl.PositionY);
                    yMaxForScale = Math.Max(yMaxForScale, sl.PositionY);
                }

                if (compactPreview)
                {
                    yMinFit -= 0.55;
                    yMaxForScale += 0.42;
                }
            }

            double spanY = layout != null
                ? Math.Max(0.3, yMaxForScale - yMinFit)
                : Math.Max(0.3, yMaxFit - ymin);

            double sx = (cw - padX * 2) / spanX;
            double sy = (ch - padTop - padBottom) / Math.Max(1.0, spanY);
            // 有 layout 时按 1:1 几何（Y 跨度含平面带/轴线/顶排注记，已经不小，不再做视觉放大）；
            // 无 layout 的老路径（单条路面剖面）保持纵向放大看细节。
            double yExaggerate = (layout != null) ? 1.0 : 8.0;
            double s = Math.Min(sx, sy / yExaggerate);
            if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0) return;

            double ox = cw / 2.0;
            // 画面基线（model y=0）放在画布下方；layout 模式下基线所占份额 = (|ymin|/spanY)，老路径保持固定 padBottom
            double oy;
            if (layout != null)
            {
                oy = padTop + yMaxForScale * s;
                if (oy > ch - padBottom) oy = ch - padBottom;
            }
            else
            {
                oy = ch - padBottom;
            }

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

            // ============================== 2b. 平面分隔带示意（分条与 rCs 一致；仅填色、无图案块） ==============================
            if (layout != null)
            {
                foreach (var span in EnumeratePlanStripSpansForPreview(layout))
                {
                    if (span.Kind == TemplateComponentKind.MedianStrip) continue;
                    var r = new Polygon
                    {
                        Stroke = OutlineBrush,
                        StrokeThickness = 0.4,
                        Fill = (fills.TryGetValue(span.Kind, out var f) ? f : Brushes.Transparent),
                    };
                    r.Points.Add(map(span.StartX, planStripBottomY));
                    r.Points.Add(map(span.EndX, planStripBottomY));
                    r.Points.Add(map(span.EndX, planStripTopY));
                    r.Points.Add(map(span.StartX, planStripTopY));
                    canvas.Children.Add(r);
                }
            }

            // ============================== 3. 左/中/右轴线 + 轴线文字（中心竖线 x=0 无偏移；字在整段轴线的竖向中点，与 rCs 一致） ==============================
            if (figure.AxisMarkers != null && figure.AxisMarkers.Count > 0)
            {
                double yLabelFig = 0.5 * (axisBottomY + axisTopY);
                foreach (var axis in figure.AxisMarkers)
                {
                    bool isCenter = Math.Abs(axis.X) < 1e-9;
                    double xLineFig = axis.X;
                    var p0 = map(xLineFig, axisBottomY);
                    var p1 = map(xLineFig, axisTopY);
                    var brush = isCenter ? CenterLineBrush : RedAxisBrush;
                    canvas.Children.Add(new Line
                    {
                        X1 = p0.X,
                        Y1 = p0.Y,
                        X2 = p1.X,
                        Y2 = p1.Y,
                        Stroke = brush,
                        StrokeThickness = 0.9,
                        StrokeDashArray = isCenter
                            ? new DoubleCollection(new[] { 4.0, 3.0, 1.0, 3.0 })
                            : null,
                    });

                    double xTextFig = axis.X;
                    if (!isCenter)
                    {
                        bool isLeft = (axis.Label ?? string.Empty).IndexOf("左", StringComparison.Ordinal) >= 0
                                      || axis.X < -1e-6;
                        xTextFig = axis.X + (isLeft ? -0.3 : 0.3);
                    }

                    var pText = map(xTextFig, yLabelFig);
                    string label = isCenter
                        ? (string.IsNullOrWhiteSpace(axis.Label) ? "中心线" : axis.Label)
                        : axis.Label;
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        double totalLineH = label.Length * 11 * 1.05;
                        double yStart = pText.Y - totalLineH * 0.5;
                        AddVerticalText(canvas, pText.X, yStart, label, brush, 11);
                    }
                }
            }
            else
            {
                var p0 = map(0, axisBottomY);
                var p1 = map(0, axisTopY);
                canvas.Children.Add(new Line
                {
                    X1 = p0.X,
                    Y1 = p0.Y,
                    X2 = p1.X,
                    Y2 = p1.Y,
                    Stroke = CenterLineBrush,
                    StrokeThickness = 0.8,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0, 1.0, 3.0 }),
                });
                double yMid = 0.5 * (axisBottomY + axisTopY);
                var pText = map(0, yMid);
                double totalLineH = 3 * 11 * 1.05; // "中心线"
                AddVerticalText(canvas, pText.X, pText.Y - totalLineH * 0.5, "中心线", CenterLineBrush, 11);
            }

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

            // ============================== 6. 主尺寸链（与 rCs 同公式 + 0.5m 图面下移） ==============================
            // rCs 用：txtH ≈ 0.3 m（模型 m）；dimRowGap = max(0.45, txtH*1.9) = 0.57 m。
            // 预览无 ActualTextHeight 语境，直接按同一常量铺。
            const double previewTxtH = 0.3;
            const double rcsDimensionChainDropFigureM = 0.5;
            // 拉大 tier=0 与 tier=1 的间距，避免预览字体与邻近尺寸线相撞（3.2x vs 1.0x）。
            double dimRowGap = Math.Max(0.45, previewTxtH * 1.9);
            foreach (var seg in figure.DimensionSegments)
            {
                double featureY;
                double dimOffset;
                if (seg.Track == FigureDimensionTrack.Top)
                {
                    featureY = planStripTopY - rcsDimensionChainDropFigureM;
                    dimOffset = (seg.Tier == 0 ? dimRowGap * 3.2 : dimRowGap);
                }
                else
                {
                    featureY = 0.0 - rcsDimensionChainDropFigureM;
                    dimOffset = (seg.Tier == 0 ? -dimRowGap * 3.2 : -dimRowGap);
                }
                double yModel = featureY + dimOffset;
                if (seg.Track == FigureDimensionTrack.Top)
                    yModel += dimRowGap; // 平面图上侧尺寸链整体上移一个标注间距
                var p0 = map(seg.StartX, yModel);
                var p1 = map(seg.EndX, yModel);
                double yLine = 0.5 * (p0.Y + p1.Y);
                double tExt = 4;
                double x1 = p0.X;
                double x2 = p1.X;
                if (x1 > x2) (x1, x2) = (x2, x1);
                canvas.Children.Add(new Line
                {
                    X1 = x1,
                    X2 = x2,
                    Y1 = yLine,
                    Y2 = yLine,
                    Stroke = DimensionBrush,
                    StrokeThickness = 0.7,
                });
                canvas.Children.Add(new Line { X1 = p0.X, X2 = p0.X, Y1 = yLine - tExt, Y2 = yLine + tExt, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                canvas.Children.Add(new Line { X1 = p1.X, X2 = p1.X, Y1 = yLine - tExt, Y2 = yLine + tExt, Stroke = DimensionBrush, StrokeThickness = 0.7 });
                double midX = 0.5 * (p0.X + p1.X);
                // 字统一放到尺寸线「上方」（离路面/路顶一侧更远），避免下排字与上排尺寸线重叠；水平向按该段几何中点居中。
                AddCenteredTextAbove(canvas, midX, yLine - 14, seg.Text, DimensionBrush, 10);
            }

            // ============================== 7. 顶部条带名称（平面带竖向居中，竖排与 CAD 对齐） ==============================
            const double topLabelFontSize = 11.0;
            const double topLabelLineH = topLabelFontSize * 1.05;
            foreach (var top in figure.TopLabels)
            {
                var p = map(top.CenterX, topLabelY);
                int charCount = (top.Text ?? string.Empty).Length;
                double totalH = charCount * topLabelLineH;
                double yStart = p.Y - totalH * 0.5;
                AddVerticalText(canvas, p.X, yStart, top.Text, TopLabelBrush, topLabelFontSize);
            }

            // ============================== 8. 左/右方向（紫线，与 rCs 同高 = 平面带上侧+5m） ==============================
            if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel)
                || !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
            {
                DrawOrientation(canvas, figure.Orientation, map, orientationY, onOrientationToggle);
            }

            // ============================== 9. 图题（与出图 + 设置规格一致） ==============================
            if (drawSheetTitleBand)
            {
                DrawingSheetTitleWpfRenderer.DrawTopBand(
                    canvas, cw, titleSpec,
                    DrawingSheetTitleText.RemoveTrailingScaleInTitle(figure.Title.Text),
                    scaleDenominator ?? figure.ScaleDenominator);
            }

            // ============================== 10. 路幅（比例已并入图题带） ==============================
            if (drawRoadWidthCornerBadge)
            {
                var info = new TextBlock
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "路幅 {0:F2} m",
                        spanX),
                    Foreground = AnnotationBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                };
                Canvas.SetRight(info, 8);
                Canvas.SetTop(info, 8);
                canvas.Children.Add(info);
            }
        }

        /// <param name="yModel">与 rCs <c>orientationYFig = planStripTopY + 5m</c> 一致（图面坐标 y）。</param>
        /// <param name="onToggle">点击左右方位字时触发，交由窗口切换"北南 ↔ 西东"。</param>
        private static void DrawOrientation(
            Canvas canvas,
            FigureOrientation orientation,
            Func<double, double, Point> map,
            double yModel,
            Action onToggle = null)
        {
            // 与 rCs 一致：一条水平线连接 LeftX~RightX，左端/右端小三角、外侧文字
            var pL = map(orientation.LeftX, yModel);
            var pR = map(orientation.RightX, yModel);
            double y = 0.5 * (pL.Y + pR.Y);
            pL = new Point(pL.X, y);
            pR = new Point(pR.X, y);

            var main = new Line
            {
                X1 = pL.X,
                Y1 = pL.Y,
                X2 = pR.X,
                Y2 = pR.Y,
                Stroke = OrientationLineBrush,
                StrokeThickness = 0.9,
            };
            canvas.Children.Add(main);
            // 左端箭头：尖端朝左
            var lh = new Polygon { Fill = OrientationLineBrush };
            lh.Points.Add(pL);
            lh.Points.Add(new Point(pL.X + 6, pL.Y - 3.5));
            lh.Points.Add(new Point(pL.X + 6, pL.Y + 3.5));
            canvas.Children.Add(lh);
            // 右端箭头
            var rh = new Polygon { Fill = OrientationLineBrush };
            rh.Points.Add(pR);
            rh.Points.Add(new Point(pR.X - 6, pR.Y - 3.5));
            rh.Points.Add(new Point(pR.X - 6, pR.Y + 3.5));
            canvas.Children.Add(rh);
            int approxLen = (orientation.LeftLabel != null) ? orientation.LeftLabel.Length : 0;
            double leftTextX = pL.X - 8 - Math.Max(14, 6 * approxLen);
            AddClickableText(canvas, leftTextX, pL.Y - 12, orientation.LeftLabel ?? string.Empty, AnnotationBrush, 10, onToggle);
            AddClickableText(canvas, pR.X + 4, pR.Y - 12, orientation.RightLabel ?? string.Empty, AnnotationBrush, 10, onToggle);
        }

        /// <summary>可点击文字：单击触发 <paramref name="onClick"/>（用于北南 ↔ 西东 切换）。</summary>
        private static void AddClickableText(Canvas canvas, double x, double y, string text, Brush brush, double size, Action onClick)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = size,
                FontFamily = new FontFamily("Consolas"),
                ToolTip = onClick != null ? "点击切换 北南 ↔ 西东" : null,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Arrow,
                Background = Brushes.Transparent,
            };
            if (onClick != null)
            {
                tb.MouseLeftButtonUp += (s, e) =>
                {
                    try { onClick(); } catch { /* 预览点击吞异常，避免 UI 线程崩溃 */ }
                    e.Handled = true;
                };
            }
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            canvas.Children.Add(tb);
        }

        private readonly struct PlanStripSpanPreview
        {
            public double StartX { get; }
            public double EndX { get; }
            public TemplateComponentKind Kind { get; }
            public PlanStripSpanPreview(double startX, double endX, TemplateComponentKind kind)
            {
                StartX = startX;
                EndX = endX;
                Kind = kind;
            }
        }

        private static IEnumerable<PlanStripSpanPreview> EnumeratePlanStripSpansForPreview(CrossSectionLayout layout)
        {
            double cursor = -layout.TotalWidth / 2.0;
            for (int i = layout.LeftBands.Count - 1; i >= 0; i--)
            {
                var band = layout.LeftBands[i];
                double end = cursor + band.Width;
                yield return new PlanStripSpanPreview(cursor, end, band.Kind);
                cursor = end;
            }

            if (layout.CenterMedianWidth > 1e-9)
            {
                double end = cursor + layout.CenterMedianWidth;
                cursor = end;
            }

            for (int i = 0; i < layout.RightBands.Count; i++)
            {
                var band = layout.RightBands[i];
                double end = cursor + band.Width;
                yield return new PlanStripSpanPreview(cursor, end, band.Kind);
                cursor = end;
            }
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

        /// <summary>水平居中于 <paramref name="centerX"/>，顶边在 <paramref name="topY"/>（与旧版 AddText 的垂直偏移一致）。</summary>
        private static void AddCenteredTextAbove(Canvas canvas, double centerX, double topY, string text, Brush brush, double size)
        {
            if (string.IsNullOrEmpty(text)) return;
            var tb = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = size,
                FontFamily = new FontFamily("Consolas"),
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, centerX - tb.DesiredSize.Width * 0.5);
            Canvas.SetTop(tb, topY);
            canvas.Children.Add(tb);
        }

        /// <summary>竖排（逐字向下）显示，顶端以 <paramref name="xTop"/>,<paramref name="yTop"/> 为起点。</summary>
        private static void AddVerticalText(Canvas canvas, double xTop, double yTop, string text, Brush brush, double size)
        {
            if (string.IsNullOrEmpty(text)) return;
            double lineH = size * 1.05;
            double y = yTop;
            foreach (char c in text)
            {
                if (c == ' ') { y += lineH; continue; }
                var tb = new TextBlock
                {
                    Text = c.ToString(),
                    Foreground = brush,
                    FontSize = size,
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = tb.DesiredSize.Width;
                Canvas.SetLeft(tb, xTop - w * 0.5);
                Canvas.SetTop(tb, y);
                canvas.Children.Add(tb);
                y += lineH;
            }
        }
    }
}
