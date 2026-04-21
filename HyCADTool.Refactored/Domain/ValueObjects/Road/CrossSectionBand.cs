using System;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 横断面条带（Band）所在的侧别。
    ///
    /// - <see cref="Left"/>：中心线向左（x 负半轴）。
    /// - <see cref="Right"/>：中心线向右（x 正半轴）。
    /// - <see cref="Center"/>：跨越中心线对称（用于中央分隔带本身的占位描述）。
    /// </summary>
    public enum BandSide
    {
        Left = -1,
        Center = 0,
        Right = 1,
    }

    /// <summary>
    /// 横断面条带（Band）——用户编辑模型中的最小单位。
    ///
    /// 一个"条带"代表从内向外的一段固定功能面：
    /// 例如"机动车道 3.7m 横坡 1.5%"、"绿化带 3.5m"、"人行道 6.5m"。
    ///
    /// 相较于 <see cref="Models.Road.Template"/> 使用的点+段表示：
    /// - 点+段对 Domain 是更稳定的持久形态；
    /// - 条带是交互友好的表意形态（用户想的是"加一条人行道"而非"多两个点加一条段"）。
    /// 两者通过 <see cref="Services.Road.CrossSectionLayoutBuilder"/> 互转。
    ///
    /// 本值对象不可变；任何"修改"都应构造新实例（<see cref="WithWidth"/> / <see cref="WithSlope"/> 等）。
    ///
    /// <para>
    /// v2 扩展（路牙、坡型、路拱、路面结构、车道数）：
    /// 旧的 5 参数构造函数转发到完整构造函数，新字段使用"无路牙 / 单坡 / 直线型 / 无铺装 / 无车道数"默认值，
    /// 旧调用方与旧 JSON 模板均保持兼容。
    /// </para>
    /// </summary>
    public readonly struct CrossSectionBand : IEquatable<CrossSectionBand>
    {
        /// <summary>条带显示名（用于预览顶部文字标签与 TemplatePoint.Name 前缀）。</summary>
        public string Name { get; }

        /// <summary>对应 <see cref="TemplateComponentKind"/>，驱动绘图图层与填充路由。</summary>
        public TemplateComponentKind Kind { get; }

        /// <summary>条带宽度（m）。必须 &gt; 0。</summary>
        public double Width { get; }

        /// <summary>
        /// 横坡百分比（%）。正值表示"内高外低"（机动车道/人行道的雨水向外排水的典型形式）。
        ///
        /// <list type="bullet">
        ///   <item>机动车道 / 非机动车道：1.0% ~ 2.0%（CJJ 37 §6.2.3）。</item>
        ///   <item>人行道：1.0% ~ 2.0%。</item>
        ///   <item>缘石 / 中央分隔带：0%（本条带自身无横坡）。</item>
        ///   <item>边沟：取负值表示"外高内低"，仅作为扩展占位，本 v1 不画。</item>
        /// </list>
        /// </summary>
        public double CrossSlopePct { get; }

        /// <summary>条带所在的侧别，用于 Builder 推导 <c>HorizontalOffset</c> 的符号。</summary>
        public BandSide Side { get; }

        // ==================================== v2 新增字段 ====================================

        /// <summary>
        /// 外侧路牙规格。生效时会在板块外缘叠加竖向凸起几何（参见 <see cref="KerbSpec"/>）。
        /// 默认 <see cref="KerbSpec.None"/>。
        /// </summary>
        public KerbSpec OuterKerb { get; }

        /// <summary>
        /// 内侧路牙规格。绝大多数场景为 <see cref="KerbSpec.None"/>，
        /// 仅当板块为机动车道与中分带相邻、需要在中分带侧加缘石时启用。
        /// </summary>
        public KerbSpec InnerKerb { get; }

        /// <summary>
        /// 坡型。<see cref="RoadSlopeType.Single"/> 单坡（外低）；
        /// <see cref="RoadSlopeType.Double"/> 双坡（板块中央高、两侧低）。
        /// </summary>
        public RoadSlopeType SlopeType { get; }

        /// <summary>
        /// 路拱形式。<see cref="RoadCrownProfile.Linear"/> 直线型（默认，最常用）；
        /// <see cref="RoadCrownProfile.Parabolic"/> 抛物线；<see cref="RoadCrownProfile.Folded"/> 折线。
        /// </summary>
        public RoadCrownProfile CrownProfile { get; }

        /// <summary>
        /// 路面结构（铺装类型）。决定填色与未来分层结构线绘制。默认 <see cref="RoadSurfaceLayer.None"/>。
        /// </summary>
        public RoadSurfaceLayer SurfaceLayer { get; }

        /// <summary>
        /// 车道数（仅 <see cref="TemplateComponentKind.Pavement"/> / <see cref="TemplateComponentKind.NonMotorized"/>
        /// 类型有意义）。0 表示"未划分"，几何不画分车道线；&gt;0 时按等分绘制 N 条车道线。
        /// </summary>
        public int LaneCount { get; }

        /// <summary>
        /// 路面结构层方案（可空）。承载"面层 / 基层 / 垫层"多层分层构造（M7+）。
        /// 运行时由上层 UI（`BandRowViewModel`）根据 <see cref="Kind"/> 自动注入默认方案；
        /// 当前 Phase 1 的 <c>CrossSectionLayoutBuilder</c> 不把本字段写入 <see cref="Models.Road.Template"/>
        /// （Template JSON Schema 保持不变），Phase 2 再做持久化。绿化带 / 中分带 / 缘石不挂方案。
        /// </summary>
        public StructureLayerScheme StructureScheme { get; }

        // ==================================== 构造函数 ====================================

        /// <summary>
        /// 旧 5 参数构造函数。新字段使用安全默认值（无路牙 / 单坡 / 直线型 / 无铺装 / 0 车道 / 无结构方案）。
        /// </summary>
        public CrossSectionBand(string name, TemplateComponentKind kind, double width, double crossSlopePct, BandSide side)
            : this(name, kind, width, crossSlopePct, side,
                   KerbSpec.None, KerbSpec.None,
                   RoadSlopeType.Single, RoadCrownProfile.Linear, RoadSurfaceLayer.None, 0,
                   structureScheme: null)
        {
        }

        /// <summary>
        /// 完整构造函数。所有字段一次性初始化。
        /// </summary>
        public CrossSectionBand(
            string name,
            TemplateComponentKind kind,
            double width,
            double crossSlopePct,
            BandSide side,
            KerbSpec outerKerb,
            KerbSpec innerKerb,
            RoadSlopeType slopeType,
            RoadCrownProfile crownProfile,
            RoadSurfaceLayer surfaceLayer,
            int laneCount,
            StructureLayerScheme structureScheme = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("条带名称不能为空。", nameof(name));
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"条带宽度必须 > 0，当前 {width}。");
            if (double.IsNaN(crossSlopePct) || double.IsInfinity(crossSlopePct))
                throw new ArgumentOutOfRangeException(nameof(crossSlopePct), $"横坡必须为有限值，当前 {crossSlopePct}。");
            if (Math.Abs(crossSlopePct) > 20)
                throw new ArgumentOutOfRangeException(nameof(crossSlopePct), $"横坡绝对值不得超过 20%，当前 {crossSlopePct}。");
            if (laneCount < 0)
                throw new ArgumentOutOfRangeException(nameof(laneCount), $"车道数必须 ≥ 0，当前 {laneCount}。");

            Name = name.Trim();
            Kind = kind;
            Width = width;
            CrossSlopePct = crossSlopePct;
            Side = side;
            OuterKerb = outerKerb;
            InnerKerb = innerKerb;
            SlopeType = slopeType;
            CrownProfile = crownProfile;
            SurfaceLayer = surfaceLayer;
            LaneCount = laneCount;
            StructureScheme = structureScheme;
        }

        // ==================================== With* 方法 ====================================
        // 注意：每个 With* 都把 v2 字段一并带过去，避免镜像/侧别变更等操作丢失路牙等信息。

        /// <summary>返回相同字段、只改变宽度的新条带。</summary>
        public CrossSectionBand WithWidth(double width)
            => new CrossSectionBand(Name, Kind, width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变横坡的新条带。</summary>
        public CrossSectionBand WithSlope(double crossSlopePct)
            => new CrossSectionBand(Name, Kind, Width, crossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变类型的新条带（横坡不变）。</summary>
        public CrossSectionBand WithKind(TemplateComponentKind kind)
            => new CrossSectionBand(Name, kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变侧别的新条带（镜像时用）。</summary>
        public CrossSectionBand WithSide(BandSide side)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变名称的新条带。</summary>
        public CrossSectionBand WithName(string name)
            => new CrossSectionBand(name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变外侧路牙规格的新条带。</summary>
        public CrossSectionBand WithOuterKerb(KerbSpec kerb)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, kerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变内侧路牙规格的新条带。</summary>
        public CrossSectionBand WithInnerKerb(KerbSpec kerb)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, kerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变坡型的新条带。</summary>
        public CrossSectionBand WithSlopeType(RoadSlopeType slopeType)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, slopeType, CrownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变路拱形式的新条带。</summary>
        public CrossSectionBand WithCrownProfile(RoadCrownProfile crownProfile)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, crownProfile, SurfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变路面结构的新条带。</summary>
        public CrossSectionBand WithSurfaceLayer(RoadSurfaceLayer surfaceLayer)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, surfaceLayer, LaneCount, StructureScheme);

        /// <summary>返回相同字段、只改变车道数的新条带。</summary>
        public CrossSectionBand WithLaneCount(int laneCount)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, laneCount, StructureScheme);

        /// <summary>
        /// 返回相同字段、只改变 <see cref="StructureScheme"/> 的新条带。
        /// 传 <c>null</c> 清除当前方案（例如切到绿化带 / 中分带时）。
        /// </summary>
        public CrossSectionBand WithStructureScheme(StructureLayerScheme scheme)
            => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, Side, OuterKerb, InnerKerb, SlopeType, CrownProfile, SurfaceLayer, LaneCount, scheme);

        // ==================================== 简化工厂 ====================================
        //
        // 场景：VM.LoadPreset 与 ViewModel 测试需要快速构造常见条带。
        // 这些工厂仅封装默认值与 Kind 映射，不包含任何规范校验
        // （校验归 CrossSectionCodeChecker）。

        /// <summary>机动车道（默认 1.5% 横坡）。</summary>
        public static CrossSectionBand Lane(double width, double slopePct = 1.5, BandSide side = BandSide.Left, string name = "机动车道")
            => new CrossSectionBand(name, TemplateComponentKind.Pavement, width, slopePct, side);

        /// <summary>机动车道（带外侧立缘石）。常用于人行道相邻一侧的最外机动车道。</summary>
        public static CrossSectionBand LaneWithCurb(double width, double slopePct = 1.5, BandSide side = BandSide.Left, string name = "机动车道")
            => new CrossSectionBand(name, TemplateComponentKind.Pavement, width, slopePct, side,
                                    KerbSpec.DefaultCurb(), KerbSpec.None,
                                    RoadSlopeType.Single, RoadCrownProfile.Linear, RoadSurfaceLayer.PavementSurface, 1);

        /// <summary>非机动车道（默认 1.5% 横坡）。</summary>
        public static CrossSectionBand NonMotor(double width, double slopePct = 1.5, BandSide side = BandSide.Left, string name = "非机动车道")
            => new CrossSectionBand(name, TemplateComponentKind.NonMotorized, width, slopePct, side);

        /// <summary>人行道（默认 1.5% 横坡）。</summary>
        public static CrossSectionBand Sidewalk(double width, double slopePct = 1.5, BandSide side = BandSide.Left, string name = "人行道")
            => new CrossSectionBand(name, TemplateComponentKind.Sidewalk, width, slopePct, side);

        /// <summary>缘石（固定 0%）。</summary>
        public static CrossSectionBand Kerb(double width = 0.15, BandSide side = BandSide.Left, string name = "缘石")
            => new CrossSectionBand(name, TemplateComponentKind.Kerb, width, 0, side);

        /// <summary>绿化带（固定 0%）。</summary>
        public static CrossSectionBand GreenStrip(double width, BandSide side = BandSide.Left, string name = "绿化带")
            => new CrossSectionBand(name, TemplateComponentKind.GreenStrip, width, 0, side);

        /// <summary>中央分隔带（固定 0%，侧别 Center）。</summary>
        public static CrossSectionBand Median(double width, string name = "中央分隔带")
            => new CrossSectionBand(name, TemplateComponentKind.MedianStrip, width, 0, BandSide.Center);

        public bool Equals(CrossSectionBand other)
            => string.Equals(Name, other.Name, StringComparison.Ordinal)
               && Kind == other.Kind
               && Width.Equals(other.Width)
               && CrossSlopePct.Equals(other.CrossSlopePct)
               && Side == other.Side
               && OuterKerb.Equals(other.OuterKerb)
               && InnerKerb.Equals(other.InnerKerb)
               && SlopeType == other.SlopeType
               && CrownProfile == other.CrownProfile
               && SurfaceLayer == other.SurfaceLayer
               && LaneCount == other.LaneCount
               && ReferenceEquals(StructureScheme, other.StructureScheme);

        public override bool Equals(object obj) => obj is CrossSectionBand b && Equals(b);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Name?.GetHashCode() ?? 0;
                h = (h * 397) ^ (int)Kind;
                h = (h * 397) ^ Width.GetHashCode();
                h = (h * 397) ^ CrossSlopePct.GetHashCode();
                h = (h * 397) ^ (int)Side;
                h = (h * 397) ^ OuterKerb.GetHashCode();
                h = (h * 397) ^ InnerKerb.GetHashCode();
                h = (h * 397) ^ (int)SlopeType;
                h = (h * 397) ^ (int)CrownProfile;
                h = (h * 397) ^ (int)SurfaceLayer;
                h = (h * 397) ^ LaneCount;
                // StructureScheme 按引用参与 hash（方案是 Entity，Id 稳定但不比较内部 Layers）
                h = (h * 397) ^ (StructureScheme?.Id.GetHashCode() ?? 0);
                return h;
            }
        }

        public override string ToString()
            => $"{Side} {Name}({Kind}) W={Width:F3}m i={CrossSlopePct:F2}%"
               + (OuterKerb.IsPresent ? $" +Kerb({OuterKerb.Type} h={OuterKerb.Height:F2})" : string.Empty);
    }
}
