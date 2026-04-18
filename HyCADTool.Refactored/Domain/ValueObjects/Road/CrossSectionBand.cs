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

        public CrossSectionBand(string name, TemplateComponentKind kind, double width, double crossSlopePct, BandSide side)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("条带名称不能为空。", nameof(name));
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"条带宽度必须 > 0，当前 {width}。");
            if (double.IsNaN(crossSlopePct) || double.IsInfinity(crossSlopePct))
                throw new ArgumentOutOfRangeException(nameof(crossSlopePct), $"横坡必须为有限值，当前 {crossSlopePct}。");
            if (Math.Abs(crossSlopePct) > 20)
                throw new ArgumentOutOfRangeException(nameof(crossSlopePct), $"横坡绝对值不得超过 20%，当前 {crossSlopePct}。");

            Name = name.Trim();
            Kind = kind;
            Width = width;
            CrossSlopePct = crossSlopePct;
            Side = side;
        }

        /// <summary>返回相同字段、只改变宽度的新条带。</summary>
        public CrossSectionBand WithWidth(double width) => new CrossSectionBand(Name, Kind, width, CrossSlopePct, Side);

        /// <summary>返回相同字段、只改变横坡的新条带。</summary>
        public CrossSectionBand WithSlope(double crossSlopePct) => new CrossSectionBand(Name, Kind, Width, crossSlopePct, Side);

        /// <summary>返回相同字段、只改变类型的新条带（横坡不变）。</summary>
        public CrossSectionBand WithKind(TemplateComponentKind kind) => new CrossSectionBand(Name, kind, Width, CrossSlopePct, Side);

        /// <summary>返回相同字段、只改变侧别的新条带（镜像时用）。</summary>
        public CrossSectionBand WithSide(BandSide side) => new CrossSectionBand(Name, Kind, Width, CrossSlopePct, side);

        /// <summary>返回相同字段、只改变名称的新条带。</summary>
        public CrossSectionBand WithName(string name) => new CrossSectionBand(name, Kind, Width, CrossSlopePct, Side);

        // ==================================== 简化工厂 ====================================
        //
        // 场景：VM.LoadPreset 与 ViewModel 测试需要快速构造常见条带。
        // 这些工厂仅封装默认值与 Kind 映射，不包含任何规范校验
        // （校验归 CrossSectionCodeChecker）。

        /// <summary>机动车道（默认 1.5% 横坡）。</summary>
        public static CrossSectionBand Lane(double width, double slopePct = 1.5, BandSide side = BandSide.Left, string name = "机动车道")
            => new CrossSectionBand(name, TemplateComponentKind.Pavement, width, slopePct, side);

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
               && Side == other.Side;

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
                return h;
            }
        }

        public override string ToString()
            => $"{Side} {Name}({Kind}) W={Width:F3}m i={CrossSlopePct:F2}%";
    }
}
