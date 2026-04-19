using System;
using System.Globalization;

namespace HyCADTool.Refactored.Domain.Models.Drawing
{
    /// <summary>
    /// 绘图单位（影响标注样式 paper→model 换算与 AutoCAD INSUNITS 同步）
    /// 整个 HyCADTool.Refactored 通用；HYOV 命令里的旧 DrawingUnit 与此统一。
    /// </summary>
    public enum DrawingUnit
    {
        /// <summary>毫米（施工图常用），UnitFactor = 1.0，INSUNITS = 4</summary>
        Millimeter,
        /// <summary>厘米（道路横断面常用），UnitFactor = 0.1，INSUNITS = 5</summary>
        Centimeter,
        /// <summary>米（总图常用），UnitFactor = 0.001，INSUNITS = 6</summary>
        Meter
    }

    /// <summary>
    /// 比例上下文（不可变值对象）——封装主比例 / 副比例(checkbox) / 单位 / 小数位，及派生计算。
    /// 
    /// 不变量（详见 plan §一-1）：
    /// <list type="bullet">
    /// <item>纸面标记大小：恒等于 paper_mm（默认 2.5 mm）</item>
    /// <item>标注读数：恒等于真实世界尺寸</item>
    /// <item>几何 1:1 语义：1 个模型单位 = 1 个绘图单位</item>
    /// </list>
    /// 
    /// 派生公式：
    /// <list type="bullet">
    /// <item>DIMSCALE = MainScale</item>
    /// <item>DIMLFAC = UseSub ? SubScale / MainScale : 1.0</item>
    /// <item>GeomMul = UseSub ? MainScale / SubScale : 1.0</item>
    /// <item>stored_paper = paper_mm × UnitFactor（再乘 DIMSCALE 得 model-unit 尺寸）</item>
    /// </list>
    /// </summary>
    public sealed class ScaleContext : IEquatable<ScaleContext>
    {
        public double MainScale { get; }
        public double SubScale { get; }
        public bool UseSubScale { get; }
        public DrawingUnit Unit { get; }
        public int Precision { get; }

        public ScaleContext(double mainScale, double subScale, bool useSubScale, DrawingUnit unit, int precision)
        {
            if (mainScale <= 0) throw new ArgumentException("MainScale 必须大于 0", nameof(mainScale));
            if (subScale <= 0) throw new ArgumentException("SubScale 必须大于 0", nameof(subScale));
            MainScale = mainScale;
            SubScale = subScale;
            UseSubScale = useSubScale;
            Unit = unit;
            Precision = ClampPrecision(unit, precision);
        }

        /// <summary>单位因子（paper-mm → model-unit 的换算倍数）</summary>
        public double UnitFactor
        {
            get
            {
                switch (Unit)
                {
                    case DrawingUnit.Millimeter: return 1.0;
                    case DrawingUnit.Centimeter: return 0.1;
                    case DrawingUnit.Meter: return 0.001;
                    default: return 1.0;
                }
            }
        }

        /// <summary>副比例几何放大倍数：UseSub 时用户需把几何按此倍数放大，未开启时 = 1</summary>
        public double GeomMul => UseSubScale ? MainScale / SubScale : 1.0;

        /// <summary>标注线性比例因子（DIMLFAC）：抵消 GeomMul，使标注数字恒等于真实尺寸</summary>
        public double DimLfac => UseSubScale ? SubScale / MainScale : 1.0;

        /// <summary>副比例归一后的实际使用 Sub：UseSub=false 时 = MainScale（样式名中使用）</summary>
        public double EffectiveSubScale => UseSubScale ? SubScale : MainScale;

        /// <summary>单位短名（用于样式名后缀：mm / cm / m）</summary>
        public string UnitShortName
        {
            get
            {
                switch (Unit)
                {
                    case DrawingUnit.Millimeter: return "mm";
                    case DrawingUnit.Centimeter: return "cm";
                    case DrawingUnit.Meter: return "m";
                    default: return "mm";
                }
            }
        }

        /// <summary>AutoCAD INSUNITS 系统变量代码（mm=4 / cm=5 / m=6）</summary>
        public short InsUnitsCode
        {
            get
            {
                switch (Unit)
                {
                    case DrawingUnit.Millimeter: return 4;
                    case DrawingUnit.Centimeter: return 5;
                    case DrawingUnit.Meter: return 6;
                    default: return 4;
                }
            }
        }

        // =========================================================================
        //  样式名工厂（命名格式与 plan §二 对齐）
        // =========================================================================

        /// <summary>标注样式名：0-Hy-{M}-{S}-Dim-{u}-{p}</summary>
        public string BuildDimStyleName() =>
            $"0-Hy-{FormatScale(MainScale)}-{FormatScale(EffectiveSubScale)}-Dim-{UnitShortName}-{Precision}";

        /// <summary>引线样式名：0-Hy-{M}-{S}-Mleader-{u}-{p}</summary>
        public string BuildMLeaderStyleName() =>
            $"0-Hy-{FormatScale(MainScale)}-{FormatScale(EffectiveSubScale)}-Mleader-{UnitShortName}-{Precision}";

        /// <summary>表格样式名：0-Hy-{M}-{S}-Table-{u}（无小数位）</summary>
        public string BuildTableStyleName() =>
            $"0-Hy-{FormatScale(MainScale)}-{FormatScale(EffectiveSubScale)}-Table-{UnitShortName}";

        // =========================================================================
        //  静态工具：小数位允许集合与夹紧
        // =========================================================================

        /// <summary>各单位允许的标注小数位（统一 0..3，三单位下拉一致）。</summary>
        public static int[] GetAllowedPrecisions(DrawingUnit unit)
        {
            // 历史上 mm 固定 0 / cm=1-2 / m=1-3，随后业务反馈要求三档统一到 0..3，
            // 交由使用者在切换单位时用 GetDefaultPrecision 自动切到合理默认值。
            return new[] { 0, 1, 2, 3 };
        }

        /// <summary>
        /// 切换到指定单位时的推荐默认小数位：mm=0 / cm=2 / m=3（对应纸面 0.25 mm 量级读数粒度）。
        /// </summary>
        public static int GetDefaultPrecision(DrawingUnit unit)
        {
            switch (unit)
            {
                case DrawingUnit.Millimeter: return 0;
                case DrawingUnit.Centimeter: return 2;
                case DrawingUnit.Meter: return 3;
                default: return 0;
            }
        }

        /// <summary>
        /// 把小数位夹紧到允许集合（当前统一 0..3）。保留方法名以兼容旧调用点。
        /// </summary>
        public static int ClampPrecision(DrawingUnit unit, int precision)
        {
            if (precision < 0) return 0;
            if (precision > 3) return 3;
            return precision;
        }

        /// <summary>
        /// 比例数字格式化：整数不带小数点（"50"），小数最多 2 位（"50.25"），避免浮点尾巴。
        /// </summary>
        private static string FormatScale(double scale)
        {
            double rounded = Math.Round(scale, 4);
            if (Math.Abs(rounded - Math.Round(rounded)) < 1e-6)
                return ((long)Math.Round(rounded)).ToString(CultureInfo.InvariantCulture);
            return rounded.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // =========================================================================
        //  Equality
        // =========================================================================

        public bool Equals(ScaleContext other)
        {
            if (other == null) return false;
            return MainScale == other.MainScale
                && SubScale == other.SubScale
                && UseSubScale == other.UseSubScale
                && Unit == other.Unit
                && Precision == other.Precision;
        }

        public override bool Equals(object obj) => Equals(obj as ScaleContext);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = MainScale.GetHashCode();
                h = h * 31 + SubScale.GetHashCode();
                h = h * 31 + UseSubScale.GetHashCode();
                h = h * 31 + (int)Unit;
                h = h * 31 + Precision;
                return h;
            }
        }

        public override string ToString() =>
            $"ScaleContext(M={MainScale}, S={SubScale}, UseSub={UseSubScale}, Unit={Unit}, Prec={Precision})";
    }
}
