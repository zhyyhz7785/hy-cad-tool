using System;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 路牙规格 VO。描述一段板块外缘（或内缘）所附路牙的尺寸与类型。
    ///
    /// 几何含义（生效仅当 <see cref="IsPresent"/> = true）：
    /// <list type="bullet">
    ///   <item>从板块外缘点向上抬升 <see cref="Height"/> 形成牙顶</item>
    ///   <item>沿横向继续延伸 <see cref="Width"/> 形成牙顶平台</item>
    ///   <item>下一板块以牙顶平台外端为新基准 y</item>
    /// </list>
    /// 这样一个板块原本只有 1 个外缘顶点，叠加路牙后变成 3 个顶点（外缘 / 牙顶左 / 牙顶右）。
    ///
    /// 不可变 readonly struct；任何修改通过 With* 工厂方法。
    /// </summary>
    public readonly struct KerbSpec : IEquatable<KerbSpec>
    {
        /// <summary>路牙类型；<see cref="RoadKerbType.None"/> 表示无路牙。</summary>
        public RoadKerbType Type { get; }

        /// <summary>规范型号字符串，例如 "15×10×50"（cm，宽×厚×长）。仅展示与持久化用，不参与几何计算。</summary>
        public string Model { get; }

        /// <summary>道牙高（m）。从板块外缘抬升的高度。</summary>
        public double Height { get; }

        /// <summary>道牙宽（m）。牙顶平台沿横向的延伸长度。0 表示牙顶就是一个点（仅竖向凸起，无水平平台）。</summary>
        public double Width { get; }

        /// <summary>是否存在有效路牙凸起：类型非 None 且高度 &gt; 0。</summary>
        public bool IsPresent => Type != RoadKerbType.None && Height > 1e-6;

        public KerbSpec(RoadKerbType type, string model, double height, double width)
        {
            if (double.IsNaN(height) || double.IsInfinity(height) || height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), $"道牙高必须 ≥ 0，当前 {height}。");
            if (double.IsNaN(width) || double.IsInfinity(width) || width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"道牙宽必须 ≥ 0，当前 {width}。");

            Type = type;
            Model = model ?? string.Empty;
            Height = height;
            Width = width;
        }

        // ==================================== 常用预设 ====================================

        /// <summary>无路牙。</summary>
        public static KerbSpec None { get; } = new KerbSpec(RoadKerbType.None, string.Empty, 0, 0);

        /// <summary>常用立缘石 15×10×50（cm）：道牙高 0.18 m / 道牙宽 0.15 m。CJJ 37 6.4 推荐值。</summary>
        public static KerbSpec DefaultCurb() => new KerbSpec(RoadKerbType.Curb, "15×10×50", 0.18, 0.15);

        /// <summary>常用平石 10×10×30（cm）：道牙高 0.02 m（微凸） / 道牙宽 0.10 m。</summary>
        public static KerbSpec DefaultPlain() => new KerbSpec(RoadKerbType.Plain, "10×10×30", 0.02, 0.10);

        /// <summary>立缘 + 平石组合：高度按立缘 0.18 m，宽 0.25 m（立缘 0.15 + 平石 0.10）。</summary>
        public static KerbSpec DefaultCombined() => new KerbSpec(RoadKerbType.Combined, "15×10×50 + 10×10×30", 0.18, 0.25);

        public bool Equals(KerbSpec other)
            => Type == other.Type
               && string.Equals(Model, other.Model, StringComparison.Ordinal)
               && Height.Equals(other.Height)
               && Width.Equals(other.Width);

        public override bool Equals(object obj) => obj is KerbSpec k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Type;
                h = (h * 397) ^ (Model?.GetHashCode() ?? 0);
                h = (h * 397) ^ Height.GetHashCode();
                h = (h * 397) ^ Width.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => IsPresent ? $"{Type} {Model} h={Height:F3}m w={Width:F3}m" : "None";
    }
}
