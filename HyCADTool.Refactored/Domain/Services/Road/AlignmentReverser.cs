using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// <see cref="Domain.Models.Road.Alignment"/> 方向反转的纯 Domain 工具。
    ///
    /// 反转后：
    /// · PI 序列整体倒排；
    /// · 每个 PI 的 <see cref="PiElement.SpiralIn"/> 与 <see cref="PiElement.SpiralOut"/> 互换
    ///   （沿反方向前进时原"入侧缓和"会变成新"出侧缓和"）；
    /// · <see cref="PiElement.Radius"/> / <see cref="PiElement.Tag"/> 保持不变（半径是标量几何量，
    ///   切向反向时半径不变，曲率符号由两边 PI 顺序隐式决定）。
    ///
    /// 不处理 <see cref="Domain.Models.Road.Alignment.StartStation"/>——保留由命令层决策；
    /// 方程通过 <see cref="ReverseStationEquations"/> 做"几何镜像"变换，保持可逆性。
    /// </summary>
    public static class AlignmentReverser
    {
        /// <summary>
        /// 反转 PI 序列，不修改 <paramref name="source"/>，返回新列表。
        /// </summary>
        public static List<PiElement> ReversePiElements(IReadOnlyList<PiElement> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new List<PiElement>(source.Count);
            for (int i = source.Count - 1; i >= 0; i--)
            {
                var e = source[i];
                result.Add(new PiElement(e.P, e.Radius, e.SpiralOut, e.SpiralIn, e.Tag));
            }
            return result;
        }

        /// <summary>
        /// 把桩号方程做"几何镜像"：<c>BeforeRaw' = totalRaw − BeforeRaw</c>，<c>AheadStation</c> 保留，
        /// 列表按 BeforeRaw 升序返回。
        ///
        /// <b>语义</b>：反方向的方程点位保留在同一<b>物理弧长</b>上（镜像位置），跳到的显示值保留不变。
        /// 这样在没有用户干预时：
        /// <list type="bullet">
        ///   <item>连续两次反转等价于恒等变换（可逆性：<c>Reverse(Reverse(eqs, L), L) = eqs</c>）；</item>
        ///   <item>StartStation 保持不变——反 alignment 的桩号坐标系仍从原 StartStation 开始沿新方向累加；</item>
        ///   <item>若希望沿新方向从原 EP 的 display 起算，请命令层手工调整 StartStation / 方程。</item>
        /// </list>
        ///
        /// 若 <c>totalRaw</c> 为非正值，抛 <see cref="ArgumentOutOfRangeException"/>；
        /// 若镜像后 BeforeRaw 越界 (≤0 或 ≥totalRaw − tol)，该方程被剔除（方程点恰好落在端点时无意义）。
        /// </summary>
        public static List<StationEquation> ReverseStationEquations(
            IReadOnlyList<StationEquation> source,
            double totalRawLength,
            double tolerance = 1e-6)
        {
            if (totalRawLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalRawLength), "totalRawLength 必须为正数。");

            var result = new List<StationEquation>();
            if (source == null || source.Count == 0) return result;

            for (int i = source.Count - 1; i >= 0; i--)
            {
                var eq = source[i];
                if (eq == null) continue;
                double newBefore = totalRawLength - eq.BeforeRaw;
                if (newBefore <= tolerance) continue;
                if (newBefore >= totalRawLength - tolerance) continue;
                result.Add(new StationEquation(newBefore, eq.AheadStation));
            }
            // 原 equations 已按 BeforeRaw 升序，倒序镜像后仍是升序；显式 Sort 以防输入无序。
            result.Sort((x, y) => x.BeforeRaw.CompareTo(y.BeforeRaw));
            return result;
        }
    }
}
