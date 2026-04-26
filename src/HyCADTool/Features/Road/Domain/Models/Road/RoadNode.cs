using System;
using System.Collections.Generic;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 道路节点（交叉口 / 端头 / 转盘）。
    ///
    /// v1 为 <see cref="RoadArm"/> 聚合容器（对应 <c>CrosswalkService.AnalyzeIntersection</c> 的输出）。
    /// v2 引入 T 形 / Y 形 / 十字 / 环岛等细分节点类型。
    /// </summary>
    public sealed class RoadNode
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>交叉口中心点。</summary>
        public Point3D Center { get; set; }

        /// <summary>交叉口包含的道路方向（Arm）。</summary>
        public List<RoadArm> Arms { get; } = new List<RoadArm>();

        public override string ToString() => $"RoadNode[{Name}, Id={Id:N}, Arms={Arms.Count}]";
    }
}
