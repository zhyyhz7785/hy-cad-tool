using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 单个板块顶点的功能角色。便于绘图层把不同部分挂到不同图层（路面 / 路牙 / 抛物线插值）。
    /// </summary>
    public enum BandVertexRole
    {
        /// <summary>路面内部抛物线/折线插值点（仅当 CrownProfile != Linear 时存在）。</summary>
        SurfaceInterior = 0,

        /// <summary>路面外缘点（板块"内端 → 外端"路径的终点；后接路牙或下一板块）。</summary>
        SurfaceOuter = 1,

        /// <summary>路牙底外（路面顶面沿外侧延伸 KerbWidth 之处，路牙凸起的"L"底端）。</summary>
        KerbBaseOuter = 2,

        /// <summary>路牙顶外（路牙凸起的"L"上外角）。</summary>
        KerbTopOuter = 3,

        /// <summary>路牙顶内（路牙凸起的"L"上内角，与下一板块的内端重合）。</summary>
        KerbTopInner = 4,
    }

    /// <summary>
    /// 板块在生成器中产出的一个顶点。
    /// </summary>
    public readonly struct BandVertex
    {
        public double X { get; }
        public double Y { get; }
        public string Name { get; }
        public BandVertexRole Role { get; }

        public BandVertex(double x, double y, string name, BandVertexRole role)
        {
            X = x;
            Y = y;
            Name = name ?? string.Empty;
            Role = role;
        }

        public override string ToString() => $"{Role} {Name}({X:F3}, {Y:F3})";
    }

    /// <summary>
    /// 一个板块"从内端起向外推进"的几何结果。
    /// <list type="bullet">
    ///   <item><see cref="Vertices"/>：从内向外的所有附加顶点（不含起点本身）。</item>
    ///   <item><see cref="SurfaceOuterIndex"/>：路面外缘点在 <see cref="Vertices"/> 中的索引。</item>
    ///   <item><see cref="HasOuterKerb"/>：是否生成了外侧路牙的"L 型"凸起。</item>
    ///   <item>最后一个顶点（<c>Vertices[Count-1]</c>）即"下一板块的内端"位置（无路牙时 = 路面外缘；有路牙时 = 路牙顶内）。</item>
    /// </list>
    /// </summary>
    public sealed class BandGeometry
    {
        public CrossSectionBand SourceBand { get; }
        public BandSide Side { get; }
        public double StartX { get; }
        public double StartY { get; }

        public IReadOnlyList<BandVertex> Vertices { get; }
        public int SurfaceOuterIndex { get; }
        public bool HasOuterKerb { get; }

        public int NextInnerIndex => Vertices.Count - 1;
        public double NextInnerX => Vertices.Count > 0 ? Vertices[Vertices.Count - 1].X : StartX;
        public double NextInnerY => Vertices.Count > 0 ? Vertices[Vertices.Count - 1].Y : StartY;

        /// <summary>板块路面外缘的绝对坐标（X）。</summary>
        public double SurfaceOuterX => Vertices[SurfaceOuterIndex].X;

        /// <summary>板块路面外缘的绝对坐标（Y）。</summary>
        public double SurfaceOuterY => Vertices[SurfaceOuterIndex].Y;

        public BandGeometry(
            CrossSectionBand sourceBand,
            BandSide side,
            double startX,
            double startY,
            IReadOnlyList<BandVertex> vertices,
            int surfaceOuterIndex,
            bool hasOuterKerb)
        {
            SourceBand = sourceBand;
            Side = side;
            StartX = startX;
            StartY = startY;
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count == 0) throw new ArgumentException("板块必须至少产生一个外缘顶点。", nameof(vertices));
            SurfaceOuterIndex = surfaceOuterIndex;
            HasOuterKerb = hasOuterKerb;
        }
    }

    /// <summary>
    /// 横断面几何生成器。把 <see cref="CrossSectionBand"/> 的板块参数（宽 / 横坡 / 路牙 / 路拱）
    /// 转换为内→外的顶点序列。
    ///
    /// <para>核心几何模型：</para>
    /// <list type="bullet">
    ///   <item>路面内部按 <see cref="RoadCrownProfile"/> 插值（Linear 不插值；Parabolic 用 N 等分二次曲线；Folded 用单一折点）。</item>
    ///   <item>路牙凸起（仅 <see cref="CrossSectionBand.OuterKerb"/>.IsPresent 时）按"路面外缘 → 路牙底外 → 路牙顶外 → 路牙顶内"产生 3 个新顶点；
    ///       下一板块内端 = 路牙顶内（X 与路面外缘相同，Y 抬升 KerbHeight）。</item>
    ///   <item>横坡符号：内高外低 → <c>Pct &gt; 0</c>，对路面/非机动/人行/路肩生效；缘石/中分带/绿化带视为水平。</item>
    /// </list>
    /// </summary>
    public static class CrossSectionGeometryGenerator
    {
        /// <summary>抛物线路拱的等分段数。N=4 → 中间插入 3 个内部顶点。</summary>
        public const int ParabolicSegmentCount = 4;

        /// <summary>
        /// 折线路拱的折点位置参数。
        /// 折点位置 t=0.5（板块中点），y 偏移占总 dy 的比例 =0.40
        ///（即"先缓后陡"，路面在中点上方多保留 60% × dy 的下降量）。
        /// </summary>
        private const double FoldedKneeT = 0.5;
        private const double FoldedKneeYRatio = 0.4;

        /// <summary>
        /// 板块路面横坡的"外缘 y 相对内端 y 的符号"：
        /// 返回 -1 表示外缘 y 比内端 y 低（路面/非机动/人行/路肩，"内高外低"）；
        /// 返回 0 表示水平段（缘石/中分带/绿化带/边坡，v1 不算斜坡）。
        /// </summary>
        public static int SurfaceSlopeSign(TemplateComponentKind kind)
        {
            switch (kind)
            {
                case TemplateComponentKind.Pavement:
                case TemplateComponentKind.NonMotorized:
                case TemplateComponentKind.Sidewalk:
                case TemplateComponentKind.Shoulder:
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 从 <paramref name="startX"/>, <paramref name="startY"/> 出发，按 <paramref name="band"/> 向外推进，
        /// 产生该板块内→外的全部新顶点。
        /// </summary>
        /// <param name="side">所在侧别，决定 dx 的符号（Left → −；Right → +）。</param>
        public static BandGeometry GenerateBand(
            double startX,
            double startY,
            CrossSectionBand band,
            BandSide side)
        {
            int dirSign = side == BandSide.Right ? +1 : -1;
            int slopeSign = SurfaceSlopeSign(band.Kind);
            double dx = band.Width * dirSign;
            double totalDy = band.Width * (band.CrossSlopePct / 100.0) * slopeSign;

            var verts = new List<BandVertex>();

            // ---- 1) 路面内部插值（仅当横坡非 0 且为路面类、且选择了非线性路拱）----
            bool needInterior = band.CrownProfile != RoadCrownProfile.Linear
                                && slopeSign != 0
                                && Math.Abs(band.CrossSlopePct) > 1e-6;
            if (needInterior)
            {
                if (band.CrownProfile == RoadCrownProfile.Parabolic)
                {
                    int n = ParabolicSegmentCount;
                    for (int k = 1; k < n; k++)
                    {
                        double t = (double)k / n;
                        double xMid = startX + dx * t;
                        // y(t) = startY + totalDy * t^2  → 内端附近接近水平、外缘附近最陡
                        // 这是城市道路抛物线路拱"半幅"的常见近似（中央高、边缘低）
                        double yMid = startY + totalDy * t * t;
                        verts.Add(new BandVertex(xMid, yMid, $"{band.Name}内{k}", BandVertexRole.SurfaceInterior));
                    }
                }
                else // Folded
                {
                    double xKnee = startX + dx * FoldedKneeT;
                    double yKnee = startY + totalDy * FoldedKneeYRatio;
                    verts.Add(new BandVertex(xKnee, yKnee, $"{band.Name}折点", BandVertexRole.SurfaceInterior));
                }
            }

            // ---- 2) 路面外缘 ----
            double xOuter = startX + dx;
            double yOuter = startY + totalDy;
            int surfaceOuterIndex = verts.Count;
            verts.Add(new BandVertex(xOuter, yOuter, $"{band.Name}外缘", BandVertexRole.SurfaceOuter));

            // ---- 3) 外侧路牙凸起（L 型）----
            // 顶点序列：路牙底外（沿外侧延伸 KerbWidth）→ 路牙顶外（向上 KerbHeight）→ 路牙顶内（水平回到 SurfaceOuterX）
            // 下一板块内端 = 路牙顶内，X 与路面外缘相同，Y 抬升 KerbHeight。
            bool hasKerb = band.OuterKerb.IsPresent;
            if (hasKerb)
            {
                double kw = band.OuterKerb.Width * dirSign;
                double kh = band.OuterKerb.Height;
                verts.Add(new BandVertex(xOuter + kw, yOuter, $"{band.Name}路牙底外", BandVertexRole.KerbBaseOuter));
                verts.Add(new BandVertex(xOuter + kw, yOuter + kh, $"{band.Name}路牙顶外", BandVertexRole.KerbTopOuter));
                verts.Add(new BandVertex(xOuter, yOuter + kh, $"{band.Name}路牙顶内", BandVertexRole.KerbTopInner));
            }

            return new BandGeometry(band, side, startX, startY, verts, surfaceOuterIndex, hasKerb);
        }
    }
}
