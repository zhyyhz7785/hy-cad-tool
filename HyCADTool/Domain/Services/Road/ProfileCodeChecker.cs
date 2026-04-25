using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 纵断面设计 v1 规范校核（4 项）。
    ///
    /// 规范来源（与 plan / 鸿业 / 纬地一致）：
    /// <list type="bullet">
    ///   <item>① 最大纵坡 ≤ 限值（CJJ 37-2012 表 6.2.2，按设计速度查表，v1 以"主干路"档为准）</item>
    ///   <item>② 最小纵坡 ≥ 0.3%（CJJ 37-2012 §6.2.3，"防排水"约束；纵坡过缓 ⇒ 排水不畅）</item>
    ///   <item>③ 凸竖曲线最小半径 ≥ 一般值（CJJ 193-2012 表 4.3.2）</item>
    ///   <item>④ 凹竖曲线最小半径 ≥ 一般值（CJJ 193-2012 表 4.3.2）</item>
    /// </list>
    ///
    /// 设计原则（与 <see cref="AlignmentCodeChecker"/> 一致）：
    /// - 复用 <see cref="CodeCheckItem"/> 结构，方便 WPF 用同一 ItemTemplate 渲染；
    /// - 不抛异常；fg 为 null / Pvis 不足时全部项给"无可校核数据"并 Passed = true（不阻塞 UI）；
    /// - 凸/凹判断按 ω = g_out − g_in（凸 ω &lt; 0、凹 ω &gt; 0），与 <see cref="ProfileFgDesigner"/> 一致；
    /// - 设计速度按 <see cref="SupportedSpeeds"/> 就近 clamp。
    /// </summary>
    public static class ProfileCodeChecker
    {
        /// <summary>
        /// 支持的设计速度（km/h）。与 <see cref="AlignmentCodeChecker.SupportedSpeeds"/> 对齐，
        /// 便于同一 Profile / Alignment 切换时 ComboBox 共享。
        /// </summary>
        public static readonly IReadOnlyList<int> SupportedSpeeds = new[] { 40, 50, 60, 80 };

        /// <summary>
        /// 最大纵坡（CJJ 37-2012 表 6.2.2，主干路档；下坡为负，校核取 |g| ≤ MaxGrade）。
        /// 单位：小数（0.06 = 6%）。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MaxGradeTable =
            new Dictionary<int, double> { { 40, 0.07 }, { 50, 0.06 }, { 60, 0.06 }, { 80, 0.05 } };

        /// <summary>
        /// 凸竖曲线最小半径一般值（CJJ 193-2012 表 4.3.2）。单位：m。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MinCrestRadiusTable =
            new Dictionary<int, double> { { 40, 450 }, { 50, 700 }, { 60, 1400 }, { 80, 3000 } };

        /// <summary>
        /// 凹竖曲线最小半径一般值（CJJ 193-2012 表 4.3.2）。单位：m。
        /// </summary>
        public static readonly IReadOnlyDictionary<int, double> MinSagRadiusTable =
            new Dictionary<int, double> { { 40, 450 }, { 50, 700 }, { 60, 1050 }, { 80, 2000 } };

        /// <summary>
        /// 最小纵坡（CJJ 37-2012 §6.2.3）。0.003 = 0.3%。
        /// </summary>
        public const double MinGrade = 0.003;

        /// <summary>
        /// 直坡段坡度数值容差（避免浮点误差让"恰好 6.0%"判 NG）。
        /// </summary>
        private const double GradeEps = 1e-9;

        /// <summary>
        /// 半径容差。
        /// </summary>
        private const double RadiusEps = 1e-6;

        /// <summary>
        /// 重载：直接按 Profile 的 DesignSpeed 校核。
        /// </summary>
        public static ProfileCheckReport Check(Profile profile, ProfileFgResult fg)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return Check(profile.DesignSpeed, fg);
        }

        /// <summary>
        /// 主入口：按"已经构造完毕的 FG 结果"做校核。
        /// </summary>
        /// <param name="designSpeed">设计速度（km/h），不在 <see cref="SupportedSpeeds"/> 时就近 clamp。</param>
        /// <param name="fg">PVI 派生几何（来自 <see cref="ProfileFgDesigner.Build"/>）；可空。</param>
        public static ProfileCheckReport Check(int designSpeed, ProfileFgResult fg)
        {
            int speed = ClampSpeed(designSpeed);
            double maxGrade = Lookup(MaxGradeTable, speed);
            double minCrest = Lookup(MinCrestRadiusTable, speed);
            double minSag = Lookup(MinSagRadiusTable, speed);

            // 数据不足兜底
            if (fg == null || fg.Pvis.Count < 2)
            {
                return new ProfileCheckReport(speed, new List<CodeCheckItem>
                {
                    new CodeCheckItem("最大纵坡", true, "PVI 数 < 2，无可校核坡度。"),
                    new CodeCheckItem("最小纵坡", true, "PVI 数 < 2，无可校核坡度。"),
                    new CodeCheckItem("凸曲线最小半径", true, "无内部 PVI，无凸曲线。"),
                    new CodeCheckItem("凹曲线最小半径", true, "无内部 PVI，无凹曲线。"),
                });
            }

            var items = new List<CodeCheckItem>(4);

            // === ① 最大纵坡 ===
            //   遍历相邻 PVI 段，取 fg.Pvis[i].GradeOut（== fg.Pvis[i+1].GradeIn）
            int totalSlopes = fg.Pvis.Count - 1;
            var slopeOver = new List<string>();
            double maxAbs = 0;
            for (int i = 0; i < totalSlopes; i++)
            {
                double grade = fg.Pvis[i].GradeOut;
                double absG = Math.Abs(grade);
                if (absG > maxAbs) maxAbs = absG;
                if (absG > maxGrade + GradeEps)
                {
                    var p0 = fg.Pvis[i].Vertex;
                    var p1 = fg.Pvis[i + 1].Vertex;
                    slopeOver.Add($"K{p0.Station:F0}~K{p1.Station:F0} 坡度 {grade * 100:+0.00;-0.00}% ");
                }
            }
            bool maxOk = slopeOver.Count == 0;
            string maxMsg = maxOk
                ? $"{totalSlopes} 段坡度全部 ≤ {maxGrade * 100:F1}%（最大 |g| = {maxAbs * 100:F2}%）。"
                : $"{slopeOver.Count}/{totalSlopes} 段超过 {maxGrade * 100:F1}%（{speed} km/h 限值）：{string.Join("，", slopeOver)}";
            string maxSug = maxOk ? null : SuggestForMaxGrade(maxAbs, speed);
            items.Add(new CodeCheckItem(
                $"最大纵坡 |g| ≤ {maxGrade * 100:F1}% (CJJ 37 §6.2.2)",
                maxOk,
                maxMsg,
                maxSug));

            // === ② 最小纵坡 ===
            var slopeUnder = new List<string>();
            double minAbsNonZero = double.MaxValue;
            for (int i = 0; i < totalSlopes; i++)
            {
                double grade = fg.Pvis[i].GradeOut;
                double absG = Math.Abs(grade);
                if (absG > 0 && absG < minAbsNonZero) minAbsNonZero = absG;
                if (absG < MinGrade - GradeEps)
                {
                    var p0 = fg.Pvis[i].Vertex;
                    var p1 = fg.Pvis[i + 1].Vertex;
                    slopeUnder.Add($"K{p0.Station:F0}~K{p1.Station:F0} |g| = {absG * 100:F3}%");
                }
            }
            bool minOk = slopeUnder.Count == 0;
            string minMsg;
            if (minOk)
            {
                if (minAbsNonZero == double.MaxValue) minAbsNonZero = 0;
                minMsg = $"{totalSlopes} 段坡度全部 ≥ {MinGrade * 100:F1}%（最小 |g| = {minAbsNonZero * 100:F2}%）。";
            }
            else
            {
                minMsg = $"{slopeUnder.Count}/{totalSlopes} 段低于 {MinGrade * 100:F1}%（防排水下限）：{string.Join("，", slopeUnder)}";
            }
            items.Add(new CodeCheckItem(
                $"最小纵坡 |g| ≥ {MinGrade * 100:F1}% (CJJ 37 §6.2.3)",
                minOk,
                minMsg,
                minOk ? null : "建议加大相邻 PVI 高差，或在低洼段设锯齿形偏沟，保证排水。"));

            // === ③ 凸曲线最小半径 ===
            var crestUnder = new List<string>();
            int crestCount = 0;
            for (int i = 1; i < fg.Pvis.Count - 1; i++)
            {
                var info = fg.Pvis[i];
                if (!info.IsCrest) continue;
                crestCount++;
                double r = info.Vertex.CurveRadius;
                if (r > 0 && r < minCrest - RadiusEps)
                {
                    crestUnder.Add($"PVI[{i}] (K{info.Vertex.Station:F0}) R={r:F0} m");
                }
                else if (r <= 0)
                {
                    crestUnder.Add($"PVI[{i}] (K{info.Vertex.Station:F0}) 未设半径");
                }
            }
            bool crestOk = crestUnder.Count == 0;
            string crestMsg;
            if (crestCount == 0)
            {
                crestMsg = "无凸曲线变坡点。";
                crestOk = true;
            }
            else if (crestOk)
            {
                crestMsg = $"{crestCount} 个凸曲线全部 R ≥ {minCrest:F0} m。";
            }
            else
            {
                crestMsg = $"{crestUnder.Count}/{crestCount} 个凸曲线半径不足 {minCrest:F0} m（{speed} km/h 一般值）：{string.Join("，", crestUnder)}";
            }
            items.Add(new CodeCheckItem(
                $"凸曲线 R ≥ {minCrest:F0} m (CJJ 193 §4.3.2)",
                crestOk,
                crestMsg,
                crestOk ? null : SuggestForRadius("凸曲线", minCrest, speed, MinCrestRadiusTable)));

            // === ④ 凹曲线最小半径 ===
            var sagUnder = new List<string>();
            int sagCount = 0;
            for (int i = 1; i < fg.Pvis.Count - 1; i++)
            {
                var info = fg.Pvis[i];
                if (!info.IsSag) continue;
                sagCount++;
                double r = info.Vertex.CurveRadius;
                if (r > 0 && r < minSag - RadiusEps)
                {
                    sagUnder.Add($"PVI[{i}] (K{info.Vertex.Station:F0}) R={r:F0} m");
                }
                else if (r <= 0)
                {
                    sagUnder.Add($"PVI[{i}] (K{info.Vertex.Station:F0}) 未设半径");
                }
            }
            bool sagOk = sagUnder.Count == 0;
            string sagMsg;
            if (sagCount == 0)
            {
                sagMsg = "无凹曲线变坡点。";
                sagOk = true;
            }
            else if (sagOk)
            {
                sagMsg = $"{sagCount} 个凹曲线全部 R ≥ {minSag:F0} m。";
            }
            else
            {
                sagMsg = $"{sagUnder.Count}/{sagCount} 个凹曲线半径不足 {minSag:F0} m（{speed} km/h 一般值）：{string.Join("，", sagUnder)}";
            }
            items.Add(new CodeCheckItem(
                $"凹曲线 R ≥ {minSag:F0} m (CJJ 193 §4.3.2)",
                sagOk,
                sagMsg,
                sagOk ? null : SuggestForRadius("凹曲线", minSag, speed, MinSagRadiusTable)));

            return new ProfileCheckReport(speed, items);
        }

        /// <summary>
        /// 设计速度就近 clamp（与 <see cref="SupportedSpeeds"/> 比对，距离最近者胜出；并列取靠前者）。
        /// </summary>
        public static int ClampSpeed(int designSpeed)
        {
            int best = SupportedSpeeds[0];
            int bestDist = Math.Abs(designSpeed - best);
            foreach (var s in SupportedSpeeds)
            {
                int d = Math.Abs(designSpeed - s);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        private static double Lookup(IReadOnlyDictionary<int, double> table, int speed)
            => table.TryGetValue(speed, out var v) ? v : table[SupportedSpeeds[0]];

        /// <summary>
        /// 给"最大纵坡超限"的修改建议：要么改高差，要么降速。
        /// </summary>
        private static string SuggestForMaxGrade(double currentMaxAbs, int currentSpeed)
        {
            // 找一个能容纳 currentMaxAbs 的更低速度档
            int allowed = 0;
            foreach (var s in SupportedSpeeds)
            {
                if (currentMaxAbs <= MaxGradeTable[s] + GradeEps && (allowed == 0 || s > allowed))
                    allowed = s;
            }
            if (allowed > 0 && allowed < currentSpeed)
                return $"建议减小相邻 PVI 高差让 |g| ≤ {MaxGradeTable[currentSpeed] * 100:F1}%，或把设计速度降到 {allowed} km/h（对应限值 {MaxGradeTable[allowed] * 100:F1}%）。";
            return $"建议减小相邻 PVI 高差，让最大坡度 ≤ {MaxGradeTable[currentSpeed] * 100:F1}%。";
        }

        /// <summary>
        /// 给"竖曲线半径不足"的修改建议：提半径或降速度。
        /// </summary>
        private static string SuggestForRadius(string kind, double minR, int currentSpeed, IReadOnlyDictionary<int, double> table)
        {
            var sb = new StringBuilder();
            sb.Append($"建议把 {kind} R 提高到 ≥ {minR:F0} m");
            int allowed = 0;
            // 找一个 minR 更小、当前 R 仍能过的速度档
            foreach (var s in SupportedSpeeds)
            {
                if (s < currentSpeed && (allowed == 0 || s > allowed)) allowed = s;
            }
            if (allowed > 0)
                sb.Append($"，或把设计速度降到 {allowed} km/h（对应一般值 {table[allowed]:F0} m）");
            sb.Append('。');
            return sb.ToString();
        }
    }

    /// <summary>
    /// 纵断面 v1 校核报告（4 项）。
    /// </summary>
    public sealed class ProfileCheckReport
    {
        public int DesignSpeed { get; }
        public IReadOnlyList<CodeCheckItem> Items { get; }

        public bool AllPassed
        {
            get
            {
                for (int i = 0; i < Items.Count; i++)
                    if (!Items[i].Passed) return false;
                return true;
            }
        }

        public int FailedCount => Items.Count(i => !i.Passed);

        public ProfileCheckReport(int designSpeed, IReadOnlyList<CodeCheckItem> items)
        {
            DesignSpeed = designSpeed;
            Items = items ?? Array.Empty<CodeCheckItem>();
        }
    }
}
