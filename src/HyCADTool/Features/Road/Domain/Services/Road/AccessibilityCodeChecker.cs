using System;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 无障碍设施规范校核器（<b>纯函数 / 无状态</b>）—— 针对 <see cref="CurbRamp"/> / <see cref="TactilePaving"/>
    /// 的 <b>GB 50763-2012</b>《无障碍设计规范》核心条款。
    ///
    /// <para><b>已覆盖条款</b></para>
    /// <list type="bullet">
    /// <item>§3.2.3：缘石坡道坡度 ≤ 1:12；考虑弱势群体 1:20 更优；</item>
    /// <item>§3.2.1：单面坡宽度 ≥ 1.50 m；三面坡正面宽 ≥ 1.20 m；扇形坡下口 ≥ 1.50 m；</item>
    /// <item>§3.3.1：行进盲道宽 0.25~0.50 m；提示盲道宽 0.30~0.60 m；</item>
    /// <item>§3.3.1：盲道距缘石内边 ≥ 0.25 m（由 Designer 布置时强制 offset；Checker 不重复校验）。</item>
    /// </list>
    ///
    /// <para><b>用法</b></para>
    /// <code>
    /// var r = AccessibilityCodeChecker.CheckCurbRampWidth(ramp);
    /// if (!r.Pass) Console.WriteLine(r.Message);
    /// </code>
    ///
    /// <para><b>决策</b></para>
    /// 与 <see cref="IntersectionCodeChecker"/> 一致：返回 <see cref="CheckResult"/>，不抛异常；命令层决策"硬停 / 警告 / 忽略"。
    /// </summary>
    public static class AccessibilityCodeChecker
    {
        /// <summary>单面坡最小宽度（m）—— GB 50763 §3.2.1。</summary>
        public const double MinSingleFaceWidth = 1.50;

        /// <summary>三面坡正面最小宽度（m）—— GB 50763 §3.2.1。</summary>
        public const double MinThreeFaceWidth = 1.20;

        /// <summary>扇形 / 全宽坡最小下口宽度（m）—— GB 50763 §3.2.1。</summary>
        public const double MinFanWidth = 1.50;

        /// <summary>坡道坡度上限 —— GB 50763 §3.2.3。</summary>
        public const double MaxSlope = 1.0 / 12.0;

        /// <summary>行进盲道宽度下限 —— GB 50763 §3.3.1。</summary>
        public const double MinAdvanceWidth = 0.25;

        /// <summary>行进盲道宽度上限 —— GB 50763 §3.3.1。</summary>
        public const double MaxAdvanceWidth = 0.50;

        /// <summary>提示盲道宽度下限 —— GB 50763 §3.3.1。</summary>
        public const double MinStopWidth = 0.30;

        /// <summary>提示盲道宽度上限 —— GB 50763 §3.3.1。</summary>
        public const double MaxStopWidth = 0.60;

        /// <summary>
        /// 校核结果（与 <see cref="IntersectionCodeChecker.CheckResult"/> 结构一致）。
        /// </summary>
        public readonly struct CheckResult
        {
            public bool Pass { get; }
            public double Value { get; }
            public double Expected { get; }
            public string RuleTag { get; }
            public string Message { get; }

            public CheckResult(bool pass, double value, double expected, string ruleTag, string message)
            {
                Pass = pass;
                Value = value;
                Expected = expected;
                RuleTag = ruleTag;
                Message = message;
            }

            public override string ToString()
                => $"[{(Pass ? "OK" : "FAIL")}] {RuleTag} value={Value:F3} expected={Expected:F3}";
        }

        /// <summary>校核缘石坡道宽度（按 <see cref="CurbRamp.Kind"/> 查对应最小值）。</summary>
        public static CheckResult CheckCurbRampWidth(CurbRamp ramp)
        {
            double min;
            string tag;
            switch (ramp.Kind)
            {
                case CurbRampKind.SingleFace:
                    min = MinSingleFaceWidth; tag = "GB50763-§3.2.1-SingleFace"; break;
                case CurbRampKind.ThreeFace:
                    min = MinThreeFaceWidth; tag = "GB50763-§3.2.1-ThreeFace"; break;
                case CurbRampKind.Fan:
                    min = MinFanWidth; tag = "GB50763-§3.2.1-Fan"; break;
                default:
                    min = MinSingleFaceWidth; tag = "GB50763-§3.2.1"; break;
            }

            bool pass = ramp.Width >= min - 1e-6;
            return new CheckResult(
                pass, ramp.Width, min, tag,
                pass
                    ? $"缘石坡道 {ramp.Kind} 宽度 {ramp.Width:F2} m ≥ {min:F2} m，OK。"
                    : $"缘石坡道 {ramp.Kind} 宽度 {ramp.Width:F2} m <b>不足</b> GB 50763 §3.2.1 要求 ≥ {min:F2} m。");
        }

        /// <summary>校核坡道坡度（≤ 1:12）。</summary>
        public static CheckResult CheckCurbRampSlope(CurbRamp ramp)
        {
            bool pass = ramp.Slope <= MaxSlope + 1e-9;
            return new CheckResult(
                pass, ramp.Slope, MaxSlope, "GB50763-§3.2.3",
                pass
                    ? $"缘石坡道坡度 1:{1.0 / ramp.Slope:F1} ≤ 1:{1.0 / MaxSlope:F1}，OK。"
                    : $"缘石坡道坡度 1:{1.0 / ramp.Slope:F1} <b>陡于</b> 1:{1.0 / MaxSlope:F1} 上限（GB 50763 §3.2.3）。");
        }

        /// <summary>校核盲道宽度（按 Kind 查对应范围）。</summary>
        public static CheckResult CheckTactilePavingWidth(TactilePaving paving)
        {
            if (paving == null) throw new ArgumentNullException(nameof(paving));

            double lo, hi;
            string tag;
            if (paving.Kind == TactilePavingKind.Advance)
            {
                lo = MinAdvanceWidth; hi = MaxAdvanceWidth; tag = "GB50763-§3.3.1-Advance";
            }
            else
            {
                lo = MinStopWidth; hi = MaxStopWidth; tag = "GB50763-§3.3.1-Stop";
            }

            bool pass = paving.Width >= lo - 1e-6 && paving.Width <= hi + 1e-6;
            return new CheckResult(
                pass, paving.Width, lo, tag,
                pass
                    ? $"{paving.Kind} 盲道宽 {paving.Width:F2} m 在 [{lo:F2}, {hi:F2}] 范围内，OK。"
                    : $"{paving.Kind} 盲道宽 {paving.Width:F2} m <b>不在</b> GB 50763 §3.3.1 要求的 [{lo:F2}, {hi:F2}] m 区间。");
        }
    }
}
