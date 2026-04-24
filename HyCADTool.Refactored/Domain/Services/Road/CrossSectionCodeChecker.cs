using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 标准横断面规范检查 v1（7 项）。
    ///
    /// 数据来源：CJJ 37-2012 城市道路工程设计规范。
    /// <list type="bullet">
    ///   <item>机动车道宽 3.0 ~ 3.75 m（CJJ 37 §6.2.2）。</item>
    ///   <item>人行道宽 ≥ 1.5 m（CJJ 37 §6.4.1）。</item>
    ///   <item>非机动车道宽 ≥ 2.5 m（CJJ 37 §6.5.1）。</item>
    ///   <item>机动车道横坡 1.0 ~ 2.0 %（CJJ 37 §6.2.3）。</item>
    ///   <item>人行道 / 非机动车道横坡 1.0 ~ 2.0 %。</item>
    ///   <item>中央分隔带宽 ≥ 1.5 m（若存在）（CJJ 37 §6.6.2）。</item>
    ///   <item>左右对称：|LeftHalfWidth − RightHalfWidth| ≤ 0.1 m。</item>
    /// </list>
    ///
    /// 复用 M1 的 <see cref="CodeCheckItem"/> 结构（Name / Passed / Message / Suggestion），
    /// 让 WPF 的 DataTrigger 布局可以直接照搬。返回 <see cref="CodeCheckReport"/> 以便
    /// "全部通过"可直接由 <see cref="CodeCheckReport.AllPassed"/> 读取。
    /// </summary>
    public static class CrossSectionCodeChecker
    {
        /// <summary>机动车道宽：一般 3.5 m，最大 3.75 m，最小 3.0 m。</summary>
        public const double MinLaneWidth = 3.0;
        public const double MaxLaneWidth = 3.75;

        /// <summary>人行道宽最小值。</summary>
        public const double MinSidewalkWidth = 1.5;

        /// <summary>非机动车道宽最小值。</summary>
        public const double MinNonMotorizedWidth = 2.5;

        /// <summary>车道 / 人行道 / 非机动车道横坡范围（%）。</summary>
        public const double MinCrossSlopePct = 1.0;
        public const double MaxCrossSlopePct = 2.0;

        /// <summary>中央分隔带存在时的最小宽度。</summary>
        public const double MinMedianWidth = 1.5;

        /// <summary>左右对称容差。</summary>
        public const double SymmetryTolerance = 0.1;

        public static CodeCheckReport Check(CrossSectionLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            var items = new List<CodeCheckItem>();

            // ========== ① 机动车道宽度 ==========
            CheckLaneWidths(layout, items);

            // ========== ② 人行道宽度 ==========
            CheckSidewalkWidths(layout, items);

            // ========== ③ 非机动车道宽度 ==========
            CheckNonMotorizedWidths(layout, items);

            // ========== ④ 机动车道横坡 ==========
            CheckLaneSlopes(layout, items);

            // ========== ⑤ 人行道 / 非机动车道横坡 ==========
            CheckPedestrianSlopes(layout, items);

            // ========== ⑥ 中央分隔带 ==========
            CheckMedian(layout, items);

            // ========== ⑦ 左右对称 ==========
            CheckSymmetry(layout, items);

            return new CodeCheckReport(
                designSpeed: layout.DesignSpeed,
                turnRad: 0,
                radius: 0,
                lsIn: 0,
                lsOut: 0,
                ly: 0,
                t1: 0,
                t2: 0,
                items: items);
        }

        // ---------------------------------------------------------------------
        //  实际的 7 条规范项
        // ---------------------------------------------------------------------

        private static void CheckLaneWidths(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            var offending = new List<(string Side, int Idx, double W)>();
            EnumerateBands(layout, (b, side, idx) =>
            {
                if (b.Kind != TemplateComponentKind.Pavement) return;
                if (b.Width + 1e-6 < MinLaneWidth || b.Width - 1e-6 > MaxLaneWidth)
                    offending.Add((side, idx, b.Width));
            });

            bool allOk;
            string msg;
            string suggestion = null;
            if (CountBandsByKind(layout, TemplateComponentKind.Pavement) == 0)
            {
                allOk = true;
                msg = "无机动车道条带。";
            }
            else if (offending.Count == 0)
            {
                allOk = true;
                msg = $"全部机动车道宽在 {MinLaneWidth:F2}~{MaxLaneWidth:F2} m 之间。";
            }
            else
            {
                allOk = false;
                var parts = new List<string>();
                foreach (var o in offending) parts.Add($"{o.Side}#{o.Idx}={o.W:F2}m");
                msg = $"存在 {offending.Count} 条机动车道宽度超限：{string.Join("，", parts)}。要求 {MinLaneWidth:F2}~{MaxLaneWidth:F2} m。";
                suggestion = $"建议把超限的条带宽度调整到 {MinLaneWidth:F2}~{MaxLaneWidth:F2} m（一般取 3.50 m）。";
            }

            items.Add(new CodeCheckItem("机动车道宽度", allOk, msg, suggestion));
        }

        private static void CheckSidewalkWidths(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            var offending = new List<(string Side, int Idx, double W)>();
            EnumerateBands(layout, (b, side, idx) =>
            {
                if (b.Kind != TemplateComponentKind.Sidewalk) return;
                if (b.Width + 1e-6 < MinSidewalkWidth)
                    offending.Add((side, idx, b.Width));
            });

            bool allOk;
            string msg;
            string suggestion = null;
            int total = CountBandsByKind(layout, TemplateComponentKind.Sidewalk);
            if (total == 0)
            {
                allOk = true;
                msg = "无人行道条带。";
            }
            else if (offending.Count == 0)
            {
                allOk = true;
                msg = $"全部人行道宽 ≥ {MinSidewalkWidth:F2} m。";
            }
            else
            {
                allOk = false;
                var parts = new List<string>();
                foreach (var o in offending) parts.Add($"{o.Side}#{o.Idx}={o.W:F2}m");
                msg = $"存在 {offending.Count} 条人行道宽度不足：{string.Join("，", parts)}。要求 ≥ {MinSidewalkWidth:F2} m。";
                suggestion = $"建议把超限人行道宽度提升到 ≥ {MinSidewalkWidth:F2} m（一般 2.0~3.0 m，商业区 3.5 m）。";
            }

            items.Add(new CodeCheckItem("人行道宽度", allOk, msg, suggestion));
        }

        private static void CheckNonMotorizedWidths(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            var offending = new List<(string Side, int Idx, double W)>();
            EnumerateBands(layout, (b, side, idx) =>
            {
                if (b.Kind != TemplateComponentKind.NonMotorized) return;
                if (b.Width + 1e-6 < MinNonMotorizedWidth)
                    offending.Add((side, idx, b.Width));
            });

            bool allOk;
            string msg;
            string suggestion = null;
            int total = CountBandsByKind(layout, TemplateComponentKind.NonMotorized);
            if (total == 0)
            {
                allOk = true;
                msg = "无非机动车道条带。";
            }
            else if (offending.Count == 0)
            {
                allOk = true;
                msg = $"全部非机动车道宽 ≥ {MinNonMotorizedWidth:F2} m。";
            }
            else
            {
                allOk = false;
                var parts = new List<string>();
                foreach (var o in offending) parts.Add($"{o.Side}#{o.Idx}={o.W:F2}m");
                msg = $"存在 {offending.Count} 条非机动车道宽度不足：{string.Join("，", parts)}。要求 ≥ {MinNonMotorizedWidth:F2} m。";
                suggestion = $"建议把超限非机动车道宽度提升到 ≥ {MinNonMotorizedWidth:F2} m（一般 2.5~3.5 m）。";
            }

            items.Add(new CodeCheckItem("非机动车道宽度", allOk, msg, suggestion));
        }

        private static void CheckLaneSlopes(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            var offending = new List<(string Side, int Idx, double Slope)>();
            EnumerateBands(layout, (b, side, idx) =>
            {
                if (b.Kind != TemplateComponentKind.Pavement) return;
                double pct = Math.Abs(b.CrossSlopePct);
                if (pct + 1e-6 < MinCrossSlopePct || pct - 1e-6 > MaxCrossSlopePct)
                    offending.Add((side, idx, b.CrossSlopePct));
            });

            bool allOk;
            string msg;
            string suggestion = null;
            int total = CountBandsByKind(layout, TemplateComponentKind.Pavement);
            if (total == 0)
            {
                allOk = true;
                msg = "无机动车道条带。";
            }
            else if (offending.Count == 0)
            {
                allOk = true;
                msg = $"全部机动车道横坡在 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}% 之间。";
            }
            else
            {
                allOk = false;
                var parts = new List<string>();
                foreach (var o in offending) parts.Add($"{o.Side}#{o.Idx}={o.Slope:F2}%");
                msg = $"存在 {offending.Count} 条机动车道横坡超限：{string.Join("，", parts)}。要求 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}%。";
                suggestion = $"建议把超限横坡调整到 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}%（一般取 1.5%）。";
            }

            items.Add(new CodeCheckItem("机动车道横坡", allOk, msg, suggestion));
        }

        private static void CheckPedestrianSlopes(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            var offending = new List<(string Side, int Idx, string Kind, double Slope)>();
            EnumerateBands(layout, (b, side, idx) =>
            {
                if (b.Kind != TemplateComponentKind.Sidewalk && b.Kind != TemplateComponentKind.NonMotorized) return;
                double pct = Math.Abs(b.CrossSlopePct);
                if (pct + 1e-6 < MinCrossSlopePct || pct - 1e-6 > MaxCrossSlopePct)
                    offending.Add((side, idx, b.Kind.ToString(), b.CrossSlopePct));
            });

            bool allOk;
            string msg;
            string suggestion = null;
            int total = CountBandsByKind(layout, TemplateComponentKind.Sidewalk)
                      + CountBandsByKind(layout, TemplateComponentKind.NonMotorized);
            if (total == 0)
            {
                allOk = true;
                msg = "无人行道/非机动车道条带。";
            }
            else if (offending.Count == 0)
            {
                allOk = true;
                msg = $"全部人行道/非机动车道横坡在 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}% 之间。";
            }
            else
            {
                allOk = false;
                var parts = new List<string>();
                foreach (var o in offending) parts.Add($"{o.Side}#{o.Idx}({o.Kind})={o.Slope:F2}%");
                msg = $"存在 {offending.Count} 条人行道/非机动车道横坡超限：{string.Join("，", parts)}。要求 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}%。";
                suggestion = $"建议把超限横坡调整到 {MinCrossSlopePct:F1}~{MaxCrossSlopePct:F1}%（一般取 1.5%）。";
            }

            items.Add(new CodeCheckItem("人行道/非机动车道横坡", allOk, msg, suggestion));
        }

        private static void CheckMedian(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            bool ok;
            string msg;
            string suggestion = null;
            if (layout.CenterMedianWidth <= 0)
            {
                ok = true;
                msg = "未设置中央分隔带（允许）。";
            }
            else if (layout.CenterMedianWidth + 1e-6 >= MinMedianWidth)
            {
                ok = true;
                msg = $"中央分隔带 {layout.CenterMedianWidth:F2} m ≥ {MinMedianWidth:F2} m。";
            }
            else
            {
                ok = false;
                msg = $"中央分隔带 {layout.CenterMedianWidth:F2} m < {MinMedianWidth:F2} m。";
                suggestion = $"建议把分隔带宽提高到 ≥ {MinMedianWidth:F2} m；若需更窄可取消分隔带（设为 0）。";
            }

            items.Add(new CodeCheckItem("中央分隔带宽", ok, msg, suggestion));
        }

        private static void CheckSymmetry(CrossSectionLayout layout, List<CodeCheckItem> items)
        {
            double diff = Math.Abs(layout.LeftHalfWidth - layout.RightHalfWidth);
            bool ok = diff <= SymmetryTolerance + 1e-9;
            string msg = ok
                ? $"左半 {layout.LeftHalfWidth:F2} m 与右半 {layout.RightHalfWidth:F2} m 对称（差 {diff:F3} m ≤ {SymmetryTolerance:F2} m）。"
                : $"左半 {layout.LeftHalfWidth:F2} m 与右半 {layout.RightHalfWidth:F2} m 差 {diff:F3} m，超过容差 {SymmetryTolerance:F2} m。";
            string suggestion = ok ? null
                : $"建议开启镜像开关，或手动把两侧调成 {(layout.LeftHalfWidth + layout.RightHalfWidth) / 2.0:F2} m。";
            items.Add(new CodeCheckItem("左右对称", ok, msg, suggestion));
        }

        // ---------------------------------------------------------------------
        //  辅助
        // ---------------------------------------------------------------------

        private static int CountBandsByKind(CrossSectionLayout layout, TemplateComponentKind kind)
        {
            int c = 0;
            foreach (var b in layout.LeftBands) if (b.Kind == kind) c++;
            foreach (var b in layout.RightBands) if (b.Kind == kind) c++;
            return c;
        }

        private static void EnumerateBands(
            CrossSectionLayout layout,
            Action<CrossSectionBand, string, int> inspector)
        {
            int idx = 0;
            foreach (var b in layout.LeftBands) { inspector(b, "左", idx); idx++; }
            idx = 0;
            foreach (var b in layout.RightBands) { inspector(b, "右", idx); idx++; }
        }
    }
}
