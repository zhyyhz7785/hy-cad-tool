using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 「沿中心线的 raw 累计距离」↔「显示桩号」之间的换算器。
    ///
    /// 无方程时：
    ///   display = startStation + rawFromBp
    /// 有 <see cref="StationEquation"/>（Ahead 方向，按 <see cref="StationEquation.BeforeRaw"/> 严格升序）时：
    ///   逐个方程推进。对给定 rawFromBp，找到最后一个满足 <c>eq.BeforeRaw ≤ rawFromBp</c> 的方程 eq*：
    ///     若不存在：display = startStation + rawFromBp
    ///     若存在 eq*：display = eq*.AheadStation + (rawFromBp − eq*.BeforeRaw)
    ///
    /// <b>设计约束</b>：方程顺序沿中心线从 BP 向 EP 严格升序；不支持 Back 方向 / 重叠方程。
    /// 校验由 <see cref="ValidateAscending"/> 负责；命令层在接受用户输入时必须先调用校验。
    /// </summary>
    public static class StationConverter
    {
        /// <summary>
        /// 把「沿中心线从 BP 的 raw 累计距离（米）」换算为「显示桩号（米）」。
        /// </summary>
        /// <param name="rawFromBp">从 BP 起的累计弧长，米；允许负值（延长线外推）。</param>
        /// <param name="startStation">Alignment 起点 BP 的显示桩号（米），通常等于 <see cref="Models.Road.Alignment.StartStation"/>。</param>
        /// <param name="equations">按 <see cref="StationEquation.BeforeRaw"/> 严格升序的方程列表；<c>null</c> 或空视为无方程。</param>
        /// <returns>显示桩号（米）。</returns>
        public static double ToDisplayStation(
            double rawFromBp,
            double startStation,
            IReadOnlyList<StationEquation> equations)
        {
            if (equations == null || equations.Count == 0)
                return startStation + rawFromBp;

            // 从后往前找第一个 BeforeRaw ≤ rawFromBp 的方程；
            // 调用方保证已升序，无需在线内排序。
            StationEquation applicable = null;
            for (int i = equations.Count - 1; i >= 0; i--)
            {
                var eq = equations[i];
                if (eq == null) continue;
                if (rawFromBp >= eq.BeforeRaw)
                {
                    applicable = eq;
                    break;
                }
            }

            if (applicable == null) return startStation + rawFromBp;
            return applicable.AheadStation + (rawFromBp - applicable.BeforeRaw);
        }

        /// <summary>
        /// <see cref="ToDisplayStation"/> 的逆：给定一个显示桩号，反推出"沿中心线从 BP 的 raw 累计距离"。
        ///
        /// 用于主副桩标注：想画出显示桩号恰好为整 <c>MainInterval</c> 倍数的 tick，需要先把
        /// display 转成 raw，再用 <see cref="Geometry.Polyline3D.SamplePlanarStations"/> 在 raw 坐标上定位。
        ///
        /// <b>语义</b>：方程是 Ahead 方向的分段线性映射，给定 display 值可能落入多段之一；
        /// 本方法从 <b>后往前</b> 匹配第一段 "eq.AheadStation ≤ display" 的方程作为活动段；
        /// 若 display 不属于任何方程（即最前一段），直接 <c>raw = display − startStation</c>。
        ///
        /// 不变量：<c>FromDisplayStation(ToDisplayStation(r, s, eqs), s, eqs) = r</c>（在方程严格升序时）。
        /// </summary>
        public static double FromDisplayStation(
            double displayStation,
            double startStation,
            IReadOnlyList<StationEquation> equations)
        {
            if (equations == null || equations.Count == 0)
                return displayStation - startStation;

            StationEquation applicable = null;
            for (int i = equations.Count - 1; i >= 0; i--)
            {
                var eq = equations[i];
                if (eq == null) continue;
                if (displayStation >= eq.AheadStation)
                {
                    applicable = eq;
                    break;
                }
            }

            if (applicable == null) return displayStation - startStation;
            return applicable.BeforeRaw + (displayStation - applicable.AheadStation);
        }

        /// <summary>
        /// 校验方程列表的合法性：
        /// 1) <see cref="StationEquation.BeforeRaw"/> 为有限值且 ≥ 0；
        /// 2) <see cref="StationEquation.AheadStation"/> 为有限值；
        /// 3) BeforeRaw 严格升序（不允许重合方程）；
        /// 4) 若提供 <paramref name="totalRawLength"/> &gt; 0，BeforeRaw &lt; totalRawLength。
        ///
        /// 返回 (ok, errors)：errors 为空表示通过；非空时每条对应一处问题。
        /// </summary>
        public static (bool Ok, List<string> Errors) ValidateAscending(
            IReadOnlyList<StationEquation> equations,
            double totalRawLength = 0,
            double tolerance = 1e-6)
        {
            var errors = new List<string>();
            if (equations == null || equations.Count == 0)
                return (true, errors);

            for (int i = 0; i < equations.Count; i++)
            {
                var eq = equations[i];
                if (eq == null)
                {
                    errors.Add($"方程[{i}] 为 null。");
                    continue;
                }

                if (double.IsNaN(eq.BeforeRaw) || double.IsInfinity(eq.BeforeRaw) || eq.BeforeRaw < -tolerance)
                    errors.Add($"方程[{i}] BeforeRaw = {eq.BeforeRaw} 非法（必须为有限非负值）。");

                if (double.IsNaN(eq.AheadStation) || double.IsInfinity(eq.AheadStation))
                    errors.Add($"方程[{i}] AheadStation = {eq.AheadStation} 非法（必须为有限值）。");

                if (totalRawLength > 0 && eq.BeforeRaw > totalRawLength + tolerance)
                    errors.Add($"方程[{i}] BeforeRaw = {eq.BeforeRaw:F3} m 超出中心线总长 {totalRawLength:F3} m。");
            }

            for (int i = 1; i < equations.Count; i++)
            {
                if (equations[i - 1] == null || equations[i] == null) continue;
                if (equations[i].BeforeRaw <= equations[i - 1].BeforeRaw + tolerance)
                {
                    errors.Add($"方程[{i}] BeforeRaw = {equations[i].BeforeRaw:F3} m 未严格大于上一条 {equations[i - 1].BeforeRaw:F3} m（要求升序不重合）。");
                }
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 深克隆一组方程到可修改的 List，按 <see cref="StationEquation.BeforeRaw"/> 升序输出。
        /// 命令层用它构造"工作副本"，避免直接写 <c>Alignment.StationEquations</c>。
        /// </summary>
        public static List<StationEquation> CloneSorted(IEnumerable<StationEquation> equations)
        {
            if (equations == null) return new List<StationEquation>();
            return equations
                .Where(e => e != null)
                .Select(e => e.Clone())
                .OrderBy(e => e.BeforeRaw)
                .ToList();
        }
    }
}
