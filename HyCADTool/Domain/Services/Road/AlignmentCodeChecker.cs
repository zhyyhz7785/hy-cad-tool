using System;
using System.Collections.Generic;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// "三单元平曲线"的实时规范检查 v1（6 项）。
    ///
    /// 数据来源：CJJ 37-2012 城市道路工程设计规范 + CJJ 152-2010 城市道路交叉口设计规程
    /// 表值选取（v1 简化，内嵌常量；迁移到 hy-settings.json 的扩展留给 v2）：
    /// <list type="bullet">
    ///   <item>最小圆曲线半径（一般值）：40:100, 50:150, 60:200, 80:400（CJJ 37 表 6.3.1）。</item>
    ///   <item>最小缓和曲线长度 Ls_min：40:35, 50:40, 60:50, 80:70（CJJ 37 表 6.3.4）。</item>
    ///   <item>最小圆曲线长度 Ly_min：40:35, 50:40, 60:50, 80:70（CJJ 37 §6.3.2，行车 3 秒最低）。</item>
    ///   <item>设缓和曲线转角阈值：|θ| ≥ 7° 必设缓和（CJJ 37 §6.3.4；小角可免除）。</item>
    ///   <item>对称性容差：|Ls1 − Ls2| ≤ 1 m 判通过。</item>
    /// </list>
    ///
    /// 使用场景：
    /// - UI 在 TextBox 改值时调 <see cref="Check"/> 得到 <see cref="CodeCheckReport"/>，
    ///   逐项绑定到 WPF 的 DataTrigger 显示红字/正常字；
    /// - 命令行可直接打印 Report.Messages。
    /// </summary>
    public static class AlignmentCodeChecker
    {
        /// <summary>
        /// 最小圆曲线半径（设计速度 km/h → m）。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MinRadiusTable =
            new Dictionary<int, double> { { 40, 100 }, { 50, 150 }, { 60, 200 }, { 80, 400 } };

        /// <summary>
        /// 最小缓和曲线长度（设计速度 km/h → m）。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MinLsTable =
            new Dictionary<int, double> { { 40, 35 }, { 50, 40 }, { 60, 50 }, { 80, 70 } };

        /// <summary>
        /// 最小圆曲线长度（设计速度 km/h → m）。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MinLyTable =
            new Dictionary<int, double> { { 40, 35 }, { 50, 40 }, { 60, 50 }, { 80, 70 } };

        /// <summary>
        /// 支持的设计速度列表，直接给 UI ComboBox 用。
        /// </summary>
        public static readonly IReadOnlyList<int> SupportedSpeeds = new[] { 40, 50, 60, 80 };

        /// <summary>
        /// 设缓和曲线的转角阈值（度）。|θ| &lt; 该值时允许省略缓和曲线。
        /// </summary>
        public const double SpiralRequiredTurnDeg = 7.0;

        /// <summary>
        /// Ls 对称性容差（m）。
        /// </summary>
        public const double SymmetryTolerance = 1.0;

        /// <summary>
        /// 对单个 PI 做规范检查。
        /// </summary>
        /// <param name="designSpeed">设计速度 km/h，取 <see cref="SupportedSpeeds"/> 之一；超出范围按就近 clamp。</param>
        /// <param name="turnRad">转角，rad（带符号）。</param>
        /// <param name="radius">圆曲线半径，m。</param>
        /// <param name="lsIn">入侧缓和曲线长，m。</param>
        /// <param name="lsOut">出侧缓和曲线长，m。</param>
        /// <param name="prevTangentLen">前直线段长，m（前一个 PI 到当前 PI 的距离）。</param>
        /// <param name="nextTangentLen">后直线段长，m（当前 PI 到下一个 PI 的距离）。</param>
        public static CodeCheckReport Check(
            int designSpeed,
            double turnRad,
            double radius,
            double lsIn,
            double lsOut,
            double prevTangentLen,
            double nextTangentLen)
        {
            int speed = ClampSpeed(designSpeed);
            double minR = Lookup(MinRadiusTable, speed);
            double minLs = Lookup(MinLsTable, speed);
            double minLy = Lookup(MinLyTable, speed);

            double absTurnDeg = Math.Abs(turnRad) * 180.0 / Math.PI;

            var items = new List<CodeCheckItem>();

            // ① 转角类别 + 缓和曲线必要性（CJJ 37 §6.3.4）
            //   a) |θ| < 7° 视为小偏角，不强制缓和曲线；
            //   b) |θ| ≥ 7° 必须设缓和曲线（Ls1, Ls2 均 > 0）。
            bool needSpiral = absTurnDeg >= SpiralRequiredTurnDeg;
            bool hasSpiral = lsIn > 0 && lsOut > 0;
            string turnCat = absTurnDeg < 1e-6
                ? "直行（无圆角）"
                : (needSpiral ? $"一般曲线（|θ|={absTurnDeg:F3}° ≥ {SpiralRequiredTurnDeg}°）"
                              : $"小偏角（|θ|={absTurnDeg:F3}° < {SpiralRequiredTurnDeg}°）");
            bool turnOk = !needSpiral || hasSpiral;
            items.Add(new CodeCheckItem(
                "转角分类与缓和曲线必要性",
                turnOk,
                turnOk
                    ? $"{turnCat}"
                    : $"{turnCat}，缺缓和曲线（Ls_in={lsIn:F2}, Ls_out={lsOut:F2}）。",
                turnOk ? null : $"至少给 Ls_in、Ls_out 各设 ≥ {minLs:F0} m 的缓和曲线。"));

            // ② R ≥ 最小半径
            bool rOk = radius <= 0 ? !needSpiral : radius + 1e-6 >= minR;
            string rSuggestion = null;
            if (!rOk)
            {
                // 查一下降速后能放过的最大速度档
                int allowedSpeed = FindMaxSpeedFitting(MinRadiusTable, radius);
                rSuggestion = allowedSpeed > 0 && allowedSpeed < speed
                    ? $"建议把 R 提高到 ≥ {minR:F0} m，或把设计速度降到 {allowedSpeed} km/h（对应 R_min = {MinRadiusTable[allowedSpeed]:F0} m）。"
                    : $"建议把 R 提高到 ≥ {minR:F0} m。";
            }
            items.Add(new CodeCheckItem(
                "圆曲线半径 R ≥ 最小半径",
                rOk,
                radius <= 0
                    ? (needSpiral ? $"未设置半径，{speed} km/h 下 R ≥ {minR:F0} m。" : "未设置半径（小偏角允许）。")
                    : $"R={radius:F2} m，{speed} km/h 下要求 ≥ {minR:F0} m。",
                rSuggestion));

            // ③ Ls ≥ 最小缓和曲线长
            double minLsCurrent = Math.Min(lsIn > 0 ? lsIn : double.MaxValue, lsOut > 0 ? lsOut : double.MaxValue);
            bool lsOk;
            string lsMsg;
            if (!needSpiral && !hasSpiral)
            {
                lsOk = true;
                lsMsg = "小偏角，允许省略缓和曲线。";
            }
            else if (lsIn <= 0 || lsOut <= 0)
            {
                lsOk = false;
                lsMsg = $"Ls_in={lsIn:F2}, Ls_out={lsOut:F2}，至少一侧为 0。{speed} km/h 下要求 ≥ {minLs:F0} m。";
            }
            else
            {
                lsOk = minLsCurrent + 1e-6 >= minLs;
                lsMsg = $"Ls_in={lsIn:F2}, Ls_out={lsOut:F2} m，{speed} km/h 下要求 ≥ {minLs:F0} m。";
            }
            string lsSuggestion = null;
            if (!lsOk)
            {
                int allowedSpeed = FindMaxSpeedFitting(MinLsTable, Math.Min(lsIn > 0 ? lsIn : 0, lsOut > 0 ? lsOut : 0));
                lsSuggestion = allowedSpeed > 0 && allowedSpeed < speed
                    ? $"建议把 Ls_in、Ls_out 都提高到 ≥ {minLs:F0} m，或把设计速度降到 {allowedSpeed} km/h（对应 Ls_min = {MinLsTable[allowedSpeed]:F0} m）。"
                    : $"建议把 Ls_in、Ls_out 都提高到 ≥ {minLs:F0} m。";
            }
            items.Add(new CodeCheckItem("缓和曲线长 Ls ≥ 最小值", lsOk, lsMsg, lsSuggestion));

            // ④ Ly ≥ 最小圆曲线长
            double ly = RoadGeometryFormulas.CircularArcLength(radius, lsIn, lsOut, turnRad);
            bool lyOk;
            string lyMsg;
            if (radius <= 0 || absTurnDeg < 1e-6)
            {
                lyOk = true;
                lyMsg = "无圆曲线段。";
            }
            else
            {
                lyOk = ly + 1e-6 >= minLy;
                lyMsg = $"Ly={ly:F2} m，{speed} km/h 下要求 ≥ {minLy:F0} m。";
            }
            string lySuggestion = null;
            if (!lyOk)
            {
                // Ly = R·(|θ| − (Ls_in + Ls_out)/(2R))。Ly 不足 = 缓和曲线"吃掉"了圆弧。
                // 思路：① 减小 Ls 让圆弧回来；② 加大 R 让圆弧长度按比例变大。
                //   最大允许 Ls_sum 使 Ly = minLy: Ls_sum_max ≈ 2R·(|θ| − minLy/R) = 2R·|θ| − 2·minLy
                double lsSumMax = 2 * radius * Math.Abs(turnRad) - 2 * minLy;
                string byLs = lsSumMax > 0
                    ? $"把 Ls_in+Ls_out 降到 ≤ {lsSumMax:F1} m"
                    : "（无法仅靠减 Ls 达成）";
                // R_min for Ly pass: Ly = R·|θ| − (Ls_in+Ls_out)/2 ≥ minLy
                //   ⇒ R ≥ (minLy + (Ls_in+Ls_out)/2) / |θ|
                double rNeeded = Math.Abs(turnRad) > 1e-9
                    ? (minLy + (lsIn + lsOut) / 2.0) / Math.Abs(turnRad)
                    : 0;
                string byR = rNeeded > 0 ? $"把 R 提高到 ≥ {rNeeded:F1} m" : null;
                lySuggestion = byR != null ? $"建议 {byLs}，或 {byR}。" : $"建议 {byLs}。";
            }
            items.Add(new CodeCheckItem("圆曲线长 Ly ≥ 最小值", lyOk, lyMsg, lySuggestion));

            // ⑤ 切线长 T ≤ 相邻直线段长
            double t1 = RoadGeometryFormulas.TangentInLength(radius, lsIn, turnRad);
            double t2 = RoadGeometryFormulas.TangentOutLength(radius, lsOut, turnRad);
            bool tOk;
            string tMsg;
            string tSuggestion = null;
            if (radius <= 0 || absTurnDeg < 1e-6)
            {
                tOk = true;
                tMsg = "无圆曲线段。";
            }
            else
            {
                bool t1Ok = t1 <= prevTangentLen + 1e-6;
                bool t2Ok = t2 <= nextTangentLen + 1e-6;
                tOk = t1Ok && t2Ok;
                tMsg = $"T1={t1:F2} (前直线={prevTangentLen:F2}), T2={t2:F2} (后直线={nextTangentLen:F2})。";

                if (!tOk)
                {
                    // 取最紧的一侧作为可容纳的切线长上限。
                    double tMax = Math.Min(prevTangentLen, nextTangentLen);
                    double tanHalf = Math.Tan(Math.Abs(turnRad) / 2.0);

                    // 近似公式 T ≈ R·tan(θ/2) + Ls/2（忽略 Ls³/24R² 的高阶项）
                    //   ⇒ 反推 R_max = (T_max − Ls/2) / tan(θ/2)
                    //   ⇒ 反推 Ls_max = 2 (T_max − R·tan(θ/2))
                    double lsForR = Math.Max(lsIn, lsOut);
                    double rMax = tanHalf > 1e-9 ? (tMax - lsForR / 2.0) / tanHalf : 0;
                    double lsMax = 2 * (tMax - radius * tanHalf);

                    var parts = new List<string>();
                    if (rMax > 0 && rMax < radius)
                    {
                        // 降 R 必须还 ≥ 最小半径才算合规建议
                        if (rMax + 1e-6 >= minR) parts.Add($"把 R 降到 ≤ {rMax:F1} m");
                        else parts.Add($"把 R 降到 ≤ {rMax:F1} m（注意 < 规范 R_min = {minR:F0} m）");
                    }
                    if (lsMax > 0 && lsMax < Math.Max(lsIn, lsOut))
                    {
                        if (lsMax + 1e-6 >= minLs) parts.Add($"把 Ls 降到 ≤ {lsMax:F1} m");
                        else parts.Add($"把 Ls 降到 ≤ {lsMax:F1} m（注意 < 规范 Ls_min = {minLs:F0} m）");
                    }

                    parts.Add($"或把 PI 往两端挪，让前/后直线段都 ≥ {Math.Max(t1, t2):F1} m");
                    tSuggestion = "建议" + string.Join("，或", parts) + "。";
                }
            }
            items.Add(new CodeCheckItem("切线长 T ≤ 邻段直线长", tOk, tMsg, tSuggestion));

            // ⑥ 对称性
            bool symOk;
            string symMsg;
            string symSuggestion = null;
            if (!hasSpiral)
            {
                symOk = true;
                symMsg = "未设缓和曲线，无对称性要求。";
            }
            else
            {
                double diff = Math.Abs(lsIn - lsOut);
                symOk = diff <= SymmetryTolerance + 1e-9;
                symMsg = symOk
                    ? $"Ls1={lsIn:F2}, Ls2={lsOut:F2} (对称，容差 {SymmetryTolerance} m)。"
                    : $"Ls1={lsIn:F2}, Ls2={lsOut:F2}，差值 {diff:F2} m > {SymmetryTolerance} m（非对称）。";
                if (!symOk)
                {
                    double avg = (lsIn + lsOut) / 2.0;
                    symSuggestion = $"建议勾选\"Ls1 = Ls2 (对称)\"开关，或手动把两侧调成 {avg:F1} m。";
                }
            }
            items.Add(new CodeCheckItem("Ls1 ≈ Ls2 (对称)", symOk, symMsg, symSuggestion));

            return new CodeCheckReport(speed, turnRad, radius, lsIn, lsOut, ly, t1, t2, items);
        }

        /// <summary>
        /// 按设计速度就近查表（40 / 50 / 60 / 80）。
        /// </summary>
        public static int ClampSpeed(int designSpeed)
        {
            int best = 40;
            int bestDist = Math.Abs(designSpeed - 40);
            foreach (var s in SupportedSpeeds)
            {
                int d = Math.Abs(designSpeed - s);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        private static double Lookup(IReadOnlyDictionary<int, double> table, int speed)
            => table.TryGetValue(speed, out var v) ? v : table[40];

        /// <summary>
        /// 在"最小值表"中找到当前 value 仍能满足的最大速度档。
        /// 用来给"要么降速也能过关"的建议。找不到（value 连 40 档都过不了）返回 0。
        /// </summary>
        private static int FindMaxSpeedFitting(IReadOnlyDictionary<int, double> table, double value)
        {
            int best = 0;
            foreach (var s in SupportedSpeeds)
            {
                if (value + 1e-6 >= table[s] && s > best) best = s;
            }
            return best;
        }
    }

    /// <summary>
    /// 单个规范检查项的结果。
    ///
    /// - <see cref="Message"/>：解释"当前是什么状态"（一定有，含 OK 情况）。
    /// - <see cref="Suggestion"/>：只在 <see cref="Passed"/> = false 时才提供"怎么改能过"的具体建议，
    ///   形如"建议把 R 降到 ≤ 57 m，或把 Ls 降到 ≤ 30 m"。为空表示这类 NG 没有机械化的建议（例如设计速度反而要降）。
    /// </summary>
    public readonly struct CodeCheckItem
    {
        public string Name { get; }
        public bool Passed { get; }
        public string Message { get; }
        public string Suggestion { get; }

        public CodeCheckItem(string name, bool passed, string message, string suggestion = null)
        {
            Name = name ?? string.Empty;
            Passed = passed;
            Message = message ?? string.Empty;
            Suggestion = suggestion ?? string.Empty;
        }

        public bool HasSuggestion => !string.IsNullOrEmpty(Suggestion);

        public override string ToString()
        {
            var head = $"[{(Passed ? "OK" : "NG")}] {Name}：{Message}";
            return HasSuggestion ? head + " → " + Suggestion : head;
        }
    }

    /// <summary>
    /// 单 PI 的完整检查报告（6 项 + 关键派生量）。
    /// </summary>
    public sealed class CodeCheckReport
    {
        public int DesignSpeed { get; }
        public double TurnRad { get; }
        public double Radius { get; }
        public double LsIn { get; }
        public double LsOut { get; }
        public double Ly { get; }
        public double T1 { get; }
        public double T2 { get; }
        public IReadOnlyList<CodeCheckItem> Items { get; }

        public CodeCheckReport(
            int designSpeed,
            double turnRad,
            double radius,
            double lsIn,
            double lsOut,
            double ly,
            double t1,
            double t2,
            IReadOnlyList<CodeCheckItem> items)
        {
            DesignSpeed = designSpeed;
            TurnRad = turnRad;
            Radius = radius;
            LsIn = lsIn;
            LsOut = lsOut;
            Ly = ly;
            T1 = t1;
            T2 = t2;
            Items = items ?? System.Array.Empty<CodeCheckItem>();
        }

        /// <summary>所有检查项是否全部通过。</summary>
        public bool AllPassed
        {
            get
            {
                foreach (var it in Items)
                    if (!it.Passed) return false;
                return true;
            }
        }
    }
}
