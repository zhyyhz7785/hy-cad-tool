using System;
using System.Collections.Generic;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 横断面绘图指令集合（与 AutoCAD / WPF 无关的纯数据）。
    ///
    /// <b>为什么脱离 Band？</b>
    /// - 条带是"用户语义"；绘图层要的是"几何 + 文字 + 尺寸链"。
    /// - 如果绘图直接吃 <see cref="CrossSectionLayout"/>，WPF Canvas 预览和 AutoCAD
    ///   <c>RoadStandardSectionDrawService</c> 会重复两套"条带 → 折线"的变换。
    /// - 通过 <see cref="Services.Road.CrossSectionLayoutBuilder.ToFigure"/> 一次铺平，
    ///   两个渲染目标只需遍历本 VO 的列表即可。
    ///
    /// 本 VO 坐标以 <b>米（m）</b> 为单位；竖向标高为相对 <b>道路中心线 x=0 处</b> 顶面，该处为 <c>±0.000</c>（
    /// 非最内车道与缘/中分内缘的原始 0 点）。
    /// 渲染端按 <see cref="ScaleDenominator"/> 自行缩放模型空间尺寸。
    /// </summary>
    public sealed class CrossSectionFigure
    {
        /// <summary>所有几何顶点（按绘制顺序），轮廓折线逐对相连构成。</summary>
        public IReadOnlyList<FigureVertex> Vertices { get; }

        /// <summary>面板：逐段填色（沥青 / 人行道 / 绿化 / 分隔带），按 Kind 路由到对应图层。</summary>
        public IReadOnlyList<FigurePanel> Panels { get; }

        /// <summary>底部尺寸链分段（Tier=0 下侧在中心线拆两条总长；上侧仍一条；Tier=1 分段）。</summary>
        public IReadOnlyList<FigureDimensionSegment> DimensionSegments { get; }

        /// <summary>横坡标注（每条带中央一个）。</summary>
        public IReadOnlyList<FigureSlopeLabel> SlopeLabels { get; }

        /// <summary>标高标注（主要断点处的海拔文本）。</summary>
        public IReadOnlyList<FigureHeightLabel> HeightLabels { get; }

        /// <summary>顶部文字标签排（竖写，每条带一个；Y 在顶栏）。</summary>
        public IReadOnlyList<FigureTopLabel> TopLabels { get; }

        /// <summary>左红线 / 中心线 / 右红线等竖向轴线标记。</summary>
        public IReadOnlyList<FigureAxisMarker> AxisMarkers { get; }

        /// <summary>方位指示（例如左"北"右"南"）。</summary>
        public FigureOrientation Orientation { get; }

        /// <summary>标题（底部居中文本）。</summary>
        public FigureTitle Title { get; }

        /// <summary>红线总宽（m），仅为渲染端快速访问。</summary>
        public double TotalWidth { get; }

        /// <summary>比例尺分母（1:100 → 100），仅为渲染端快速访问。</summary>
        public int ScaleDenominator { get; }

        /// <summary>顶部平面带沿道路方向长度（m）。</summary>
        public double PlanStripLength { get; }

        public CrossSectionFigure(
            IReadOnlyList<FigureVertex> vertices,
            IReadOnlyList<FigurePanel> panels,
            IReadOnlyList<FigureDimensionSegment> dimensionSegments,
            IReadOnlyList<FigureSlopeLabel> slopeLabels,
            IReadOnlyList<FigureHeightLabel> heightLabels,
            IReadOnlyList<FigureTopLabel> topLabels,
            IReadOnlyList<FigureAxisMarker> axisMarkers,
            FigureOrientation orientation,
            FigureTitle title,
            double totalWidth,
            int scaleDenominator,
            double planStripLength)
        {
            Vertices = vertices ?? Array.Empty<FigureVertex>();
            Panels = panels ?? Array.Empty<FigurePanel>();
            DimensionSegments = dimensionSegments ?? Array.Empty<FigureDimensionSegment>();
            SlopeLabels = slopeLabels ?? Array.Empty<FigureSlopeLabel>();
            HeightLabels = heightLabels ?? Array.Empty<FigureHeightLabel>();
            TopLabels = topLabels ?? Array.Empty<FigureTopLabel>();
            AxisMarkers = axisMarkers ?? Array.Empty<FigureAxisMarker>();
            Orientation = orientation;
            Title = title;
            TotalWidth = totalWidth;
            ScaleDenominator = scaleDenominator;
            PlanStripLength = planStripLength;
        }
    }

    /// <summary>
    /// 绘图顶点。
    ///
    /// 外轮廓按 <see cref="CrossSectionFigure.Vertices"/> 顺序连线；
    /// Name 用于命令行诊断与反向编辑（Phase 2），v1 渲染可忽略。
    /// </summary>
    public readonly struct FigureVertex
    {
        public double X { get; }
        public double Y { get; }
        public string Name { get; }

        public FigureVertex(double x, double y, string name = null)
        {
            X = x;
            Y = y;
            Name = name ?? string.Empty;
        }

        public override string ToString() => string.IsNullOrEmpty(Name)
            ? $"({X:F3}, {Y:F3})"
            : $"{Name}({X:F3}, {Y:F3})";
    }

    /// <summary>
    /// 一段面板（用于填色 / 图层分类 / Hatch pattern 路由）。
    /// </summary>
    public readonly struct FigurePanel
    {
        public TemplateComponentKind Kind { get; }
        public int StartVertexIndex { get; }
        public int EndVertexIndex { get; }

        /// <summary>条带显示名（用于 hover 诊断）。</summary>
        public string Name { get; }

        public FigurePanel(TemplateComponentKind kind, int startVertexIndex, int endVertexIndex, string name = null)
        {
            Kind = kind;
            StartVertexIndex = startVertexIndex;
            EndVertexIndex = endVertexIndex;
            Name = name ?? string.Empty;
        }
    }

    /// <summary>
    /// 尺寸链的一段。
    ///
    /// 同一 <see cref="Track"/> + <see cref="Tier"/> 的各段在渲染时各建一条尺寸线（<c>Bottom + Tier=0</c> 在中心线拆为两条）。
    /// <list type="bullet">
    ///   <item><c>Tier=0</c>：总宽；下侧为左半、右半各一条。</item>
    ///   <item><c>Tier=1</c>：分段链。</item>
    ///   <item><see cref="Track"/>：顶部 / 底部分开。</item>
    /// </list>
    /// </summary>
    public enum FigureDimensionTrack
    {
        Top = 0,
        Bottom = 1,
    }

    public readonly struct FigureDimensionSegment
    {
        public double StartX { get; }
        public double EndX { get; }
        public string Text { get; }
        public int Tier { get; }
        public FigureDimensionTrack Track { get; }

        public FigureDimensionSegment(double startX, double endX, string text, int tier, FigureDimensionTrack track = FigureDimensionTrack.Bottom)
        {
            StartX = startX;
            EndX = endX;
            Text = text ?? string.Empty;
            Tier = tier;
            Track = track;
        }

        public double Length => Math.Abs(EndX - StartX);
    }

    /// <summary>横坡标注：位置 + 文本（带"%"）+ 水平箭头方向。</summary>
    public readonly struct FigureSlopeLabel
    {
        public double PositionX { get; }
        public double PositionY { get; }
        public string Text { get; }

        /// <summary>水平箭头指向：+1 向右（下坡方向），-1 向左，0 无箭头。</summary>
        public int DirectionSign { get; }

        public FigureSlopeLabel(double positionX, double positionY, string text)
            : this(positionX, positionY, text, 0)
        {
        }

        public FigureSlopeLabel(double positionX, double positionY, string text, int directionSign)
        {
            PositionX = positionX;
            PositionY = positionY;
            Text = text ?? string.Empty;
            DirectionSign = directionSign == 0 ? 0 : (directionSign > 0 ? 1 : -1);
        }
    }

    /// <summary>断点标高：位置 + 文本。</summary>
    public readonly struct FigureHeightLabel
    {
        public double PositionX { get; }
        public double PositionY { get; }
        public string Text { get; }

        public FigureHeightLabel(double positionX, double positionY, string text)
        {
            PositionX = positionX;
            PositionY = positionY;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// 顶部文字标签（竖写）：每条带中点一个。
    ///
    /// 渲染端根据 <see cref="Vertical"/> 决定是否旋转 -90° / 逐字换行。
    /// </summary>
    public readonly struct FigureTopLabel
    {
        public double CenterX { get; }
        public double CenterY { get; }
        public string Text { get; }
        public bool Vertical { get; }

        public FigureTopLabel(double centerX, double centerY, string text, bool vertical = true)
        {
            CenterX = centerX;
            CenterY = centerY;
            Text = text ?? string.Empty;
            Vertical = vertical;
        }
    }

    /// <summary>竖向轴线标记（如道路左红线 / 中心线 / 道路右红线）。</summary>
    public readonly struct FigureAxisMarker
    {
        public double X { get; }
        public string Label { get; }

        public FigureAxisMarker(double x, string label)
        {
            X = x;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// 方位指示：
    /// <list type="bullet">
    ///   <item>左右端点连线，中点绘制一个箭头；两端文字标注（例如"北 / 南"或"左 / 右"）。</item>
    ///   <item>v1 默认"北（左）/ 南（右）"。</item>
    /// </list>
    /// </summary>
    public readonly struct FigureOrientation
    {
        public double LeftX { get; }
        public double RightX { get; }
        public double Y { get; }
        public string LeftLabel { get; }
        public string RightLabel { get; }

        public FigureOrientation(double leftX, double rightX, double y, string leftLabel = "北", string rightLabel = "南")
        {
            LeftX = leftX;
            RightX = rightX;
            Y = y;
            LeftLabel = leftLabel ?? string.Empty;
            RightLabel = rightLabel ?? string.Empty;
        }
    }

    /// <summary>标题（底部居中文本）。</summary>
    public readonly struct FigureTitle
    {
        public double CenterX { get; }
        public double Y { get; }
        public string Text { get; }

        public FigureTitle(double centerX, double y, string text)
        {
            CenterX = centerX;
            Y = y;
            Text = text ?? string.Empty;
        }
    }
}
