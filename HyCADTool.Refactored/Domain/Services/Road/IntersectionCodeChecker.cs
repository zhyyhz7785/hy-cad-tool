using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 平面交叉口规范校核器（<b>纯函数 / 无状态</b>）。
    ///
    /// <para><b>规范依据</b></para>
    /// <list type="bullet">
    /// <item><b>CJJ 37-2012</b>《城市道路工程设计规范》<b>附录 B</b>（平面交叉口缘石转弯半径推荐表）；</item>
    /// <item><b>CJJ 152-2010</b>《城市道路交叉口设计规程》<b>表 6.4.2</b>（交叉口转角最小半径）；</item>
    /// <item>以下表值 <b>保守取两份规范的较大值</b>，覆盖设计车辆 "小客车 + 中型车" 混合场景。</item>
    /// </list>
    ///
    /// <para><b>用法</b></para>
    /// <code>
    /// var r = IntersectionCodeChecker.CheckCornerRadius(18.0, designSpeed: 30);
    /// if (!r.Pass) Console.WriteLine($"转角半径不足：当前 {r.Radius} &lt; {r.MinRequired}（{r.RuleTag}）");
    /// </code>
    ///
    /// <para><b>决策</b></para>
    /// <list type="bullet">
    /// <item>所有校核方法返回 <see cref="CheckResult"/> 结构，而非抛异常 —— 命令层负责决定"硬停 / 警告 / 忽略"。</item>
    /// <item>不读写全局状态；不依赖 Domain.Models.Road.* 聚合，只吃标量参数。</item>
    /// </list>
    /// </summary>
    public static class IntersectionCodeChecker
    {
        /// <summary>
        /// 校核结果。
        /// </summary>
        public readonly struct CheckResult
        {
            public bool Pass { get; }
            public double Radius { get; }
            public double MinRequired { get; }
            public string RuleTag { get; }
            public string Message { get; }

            public CheckResult(bool pass, double radius, double minRequired, string ruleTag, string message)
            {
                Pass = pass;
                Radius = radius;
                MinRequired = minRequired;
                RuleTag = ruleTag;
                Message = message;
            }

            public override string ToString() => $"[{(Pass ? "OK" : "FAIL")}] {RuleTag} R={Radius:F2} vs ≥ {MinRequired:F2}";
        }

        /// <summary>
        /// 设计速度（km/h）→ 交叉口 <b>最小转角半径</b>（米）推荐表。
        ///
        /// <para>取 CJJ 37 附录 B 与 CJJ 152 表 6.4.2 的 <b>保守并集</b>：</para>
        /// <list type="bullet">
        /// <item>V ≤ 20：Rmin = 15 m（支路 / 居住区）</item>
        /// <item>V ≤ 30：Rmin = 20 m（支路 / 次干路低速）</item>
        /// <item>V ≤ 40：Rmin = 25 m（次干路）</item>
        /// <item>V ≤ 50：Rmin = 30 m（主干路低速）</item>
        /// <item>V ≤ 60：Rmin = 40 m（主干路）</item>
        /// <item>V &gt; 60：Rmin = 50 m（快速路辅道）</item>
        /// </list>
        ///
        /// <para>返回的列表按速度升序，便于 UI 枚举。</para>
        /// </summary>
        public static IReadOnlyList<(double Speed, double MinRadius)> MinCornerRadiusTable { get; } =
            new List<(double, double)>
            {
                (20, 15),
                (30, 20),
                (40, 25),
                (50, 30),
                (60, 40),
                (80, 50),
            };

        /// <summary>
        /// 给定设计速度，返回表中对应的最小转角半径。
        /// 查表策略：向上取第一个 <c>TableSpeed ≥ designSpeed</c> 的档位；若 designSpeed 超出最大档，取最大档。
        /// </summary>
        public static double LookupMinCornerRadius(double designSpeed)
        {
            foreach (var row in MinCornerRadiusTable)
            {
                if (designSpeed <= row.Speed + 1e-6) return row.MinRadius;
            }
            return MinCornerRadiusTable[MinCornerRadiusTable.Count - 1].MinRadius;
        }

        /// <summary>
        /// 校核单个 CornerArc 的半径。
        /// </summary>
        /// <param name="radius">当前 CornerArc 的半径（米）。</param>
        /// <param name="designSpeed">设计速度（km/h）。</param>
        public static CheckResult CheckCornerRadius(double radius, double designSpeed)
        {
            if (radius <= 0)
                return new CheckResult(false, radius, 0,
                    "CJJ37-Appendix-B", $"半径必须 > 0（当前 {radius}）。");

            double min = LookupMinCornerRadius(designSpeed);
            bool pass = radius >= min - 1e-6;
            string msg = pass
                ? $"转角半径 R={radius:F2} m 满足 V={designSpeed} km/h 下 ≥ {min:F2} m。"
                : $"转角半径 R={radius:F2} m <b>不足</b> V={designSpeed} km/h 要求 ≥ {min:F2} m（CJJ 37 附录 B / CJJ 152 表 6.4.2）。";
            return new CheckResult(pass, radius, min, "CJJ37-Appendix-B", msg);
        }

        /// <summary>
        /// 校核最小臂数（交叉口至少需要 2 条 Leg）。
        /// </summary>
        public static CheckResult CheckMinLegCount(int legCount)
        {
            const int minRequired = 2;
            bool pass = legCount >= minRequired;
            return new CheckResult(
                pass,
                legCount,
                minRequired,
                "Intersection-MinLegs",
                pass
                    ? $"臂数 {legCount} ≥ {minRequired}，OK。"
                    : $"交叉口至少需要 {minRequired} 条臂（当前 {legCount}）。");
        }

        /// <summary>
        /// 校核相邻两臂之间的夹角（avoid "几乎重合臂"）。
        /// 约束：相邻两臂夹角 ≥ 15°（CJJ 152 §6.2.2 建议不小于 45°，但考虑立交匝道与支路 Y 形接入，15° 放宽作为下限）。
        /// </summary>
        /// <param name="angleDeg">相邻两臂"入向"之间的夹角（度），已按 [0, 360) 归一化。</param>
        public static CheckResult CheckAdjacentLegAngle(double angleDeg)
        {
            const double minDeg = 15.0;
            bool pass = angleDeg >= minDeg - 1e-6 && angleDeg <= 360 - minDeg + 1e-6;
            return new CheckResult(
                pass,
                angleDeg,
                minDeg,
                "CJJ152-6.2.2",
                pass
                    ? $"相邻臂夹角 {angleDeg:F1}° 合法。"
                    : $"相邻臂夹角 {angleDeg:F1}° 过小（< {minDeg}° 或 > {360 - minDeg}°），存在几乎重合或回折。");
        }
    }
}
