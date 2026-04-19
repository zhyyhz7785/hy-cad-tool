using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 平面交叉口（Intersection）—— 2+ 条 <see cref="Alignment"/> 交汇处的聚合模型。
    ///
    /// <para><b>与 <see cref="RoadNode"/> 的区别</b></para>
    /// <see cref="RoadNode"/> 是 v0 / v1 的 "<see cref="RoadArm"/> 聚合容器"（从 Line + Arc 反推交叉口臂，配合
    /// <c>CrosswalkService</c> 画人行横道）；<see cref="Intersection"/> 是 P3 新引入的 <b>从 Alignment 驱动</b>的交叉口模型，
    /// 负责生成四臂转角圆弧 / 缘石坡道 / 盲道几何。两者在 v1.x 内并存：
    /// <list type="bullet">
    /// <item>旧 <c>hyRoad</c> 命令走 <see cref="RoadNode"/> + <c>CrosswalkService</c>（从 Line/Arc 选择反推）。</item>
    /// <item>新 <c>hyRoadIntersection</c> 命令走 <see cref="Intersection"/>（从 Alignment 驱动生成）。</item>
    /// </list>
    ///
    /// <para><b>聚合内容</b></para>
    /// <list type="bullet">
    /// <item><see cref="Legs"/>：按入向方位角 <b>CCW 排序</b>的臂列表（由 <c>IntersectionDesigner</c> 保证）；</item>
    /// <item><see cref="CornerArcs"/>：相邻两臂之间的转角圆弧，数量 = Legs.Count（顺序：Leg[i] → Leg[(i+1) mod N]）；</item>
    /// <item><see cref="Center"/>：交叉口几何中心（Legs 端点的加权平均，或直接取外部传入的"围绕点"）。</item>
    /// </list>
    ///
    /// <para><b>与 JSON 持久化</b></para>
    /// <see cref="RoadDesign"/> 的新增容器 <c>Intersections</c> 引用本类；
    /// <see cref="Id"/> 同步写入 DWG Xdata（<c>HY_ROAD</c> + KIND=Intersection），支持 SAVEAS 重绑定。
    /// </summary>
    public sealed class Intersection
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>显示名（用户可编辑），例："K1+200 @ 东湖路"。</summary>
        public string Name { get; set; }

        /// <summary>交叉口中心（米），由 Designer 在构造时计算，JSON 读回时原样恢复。</summary>
        public Point2D Center { get; set; }

        /// <summary>四 / 三 / 五臂的 <see cref="IntersectionLeg"/> 列表；按入向方位角 CCW 排序。</summary>
        public List<IntersectionLeg> Legs { get; set; } = new List<IntersectionLeg>();

        /// <summary>相邻两臂间的转角圆弧；数量 = <see cref="Legs"/>.Count。</summary>
        public List<CornerArc> CornerArcs { get; set; } = new List<CornerArc>();

        /// <summary>本交叉口所属的缘石坡道（CurbRamp）集合 —— GB 50763 §3.2 无障碍坡道。
        /// 一般每个 <see cref="CornerArcs"/> 对应 1 个单面坡 / 扇形坡；JSON 往返保留。</summary>
        public List<CurbRamp> CurbRamps { get; set; } = new List<CurbRamp>();

        /// <summary>本交叉口所属的盲道（TactilePaving）集合 —— GB 50763 §3.3。
        /// Stop 型（提示盲道）通常在 CurbRamp 前 / 斑马线前一段；Advance 型（行进盲道）沿人行道纵向延伸。</summary>
        public List<TactilePaving> TactilePavings { get; set; } = new List<TactilePaving>();

        /// <summary>缺省转角半径（米），用于用户"全部臂一次性改 R"时；单个 Arc 的实际半径在 <see cref="CornerArc.Radius"/>。
        /// 默认 <see cref="DefaultCornerRadius"/> = 20 m（CJJ 37 附录 B 支路级别）。</summary>
        public double DefaultCornerRadius { get; set; } = DefaultCornerRadiusValue;

        /// <summary>设计速度（km/h），供 <c>IntersectionCodeChecker</c> 查表。
        /// 与 <see cref="Alignment"/> 侧的设计速度可不同；交叉口本身通常按"较低速支路"取值。</summary>
        public double DesignSpeed { get; set; } = 30;

        public const double DefaultCornerRadiusValue = 20.0;

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>最近一次更新时间（UTC）。</summary>
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        public override string ToString()
            => $"Intersection[{Name ?? "-"}, Id={Id:N}, Legs={Legs.Count}, Arcs={CornerArcs.Count}, Ramps={CurbRamps.Count}, Tactile={TactilePavings.Count}, Center={Center}]";
    }
}
