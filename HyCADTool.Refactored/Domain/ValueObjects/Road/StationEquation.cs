using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 桩号方程（Station Equation）。
    ///
    /// 工程动机：当旧线复测、中途接入新线、或图上需要让某一物理位置沿用既有桩号时，
    /// 道路的"沿中心线累计距离"与"标牌显示的桩号"会在某一切点 BP' 处发生跳变。
    /// 例如：K5+300 的物理位置被重新编号为 K6+000——这就是一次桩号方程。
    ///
    /// 本实现只支持 <b>Ahead 方向</b>（前进方向）的方程，即：
    /// 在 <see cref="BeforeRaw"/>（沿中心线从 BP 起累计距离，米）处，
    /// 显示桩号从 <c>K = StartStation + BeforeRaw</c> 跳变为 <see cref="AheadStation"/>，
    /// 之后继续沿中心线以正向 +1 m 累加。
    ///
    /// Back 方向 / 重叠方程不在 v1 支持范围（鸿业纬地与 Civil 3D 都允许 Back，留 v2 再补）。
    ///
    /// 与 <see cref="AlignmentStationBreakdown"/> / <see cref="StationConverter"/> 一起使用，
    /// 后者负责把"raw 累计距离"映射为"显示桩号"。
    /// </summary>
    public sealed class StationEquation
    {
        /// <summary>
        /// 方程点（BP' 之前侧）在中心线上沿 XY 平面的累计距离，米；
        /// 即距 <c>Alignment</c> 起点 BP 的累计弧长，永远 ≥ 0 且 &lt; 全线总长。
        /// </summary>
        public double BeforeRaw { get; set; }

        /// <summary>
        /// 方程点（BP' 之后侧）的显示桩号，米。
        /// 从本方程点开始，沿中心线继续前进，显示桩号 = <see cref="AheadStation"/> + (rawFromBp - <see cref="BeforeRaw"/>)。
        /// </summary>
        public double AheadStation { get; set; }

        public StationEquation() { }

        public StationEquation(double beforeRaw, double aheadStation)
        {
            BeforeRaw = beforeRaw;
            AheadStation = aheadStation;
        }

        /// <summary>克隆出独立副本，避免共享引用。</summary>
        public StationEquation Clone() => new StationEquation(BeforeRaw, AheadStation);

        public override string ToString() =>
            $"StationEquation[Raw={BeforeRaw:F3}m → Ahead={AheadStation:F3}m]";
    }
}
