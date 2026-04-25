using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 交叉口的一个道路方向（Arm）。
    ///
    /// 这是对 <c>HyCADTool.Shared.AutoCAD.Services.CrosswalkService.RoadArm</c> 的 Domain 层迁移版本，
    /// 使用纯 C# 几何值对象（<see cref="Point3D"/> / <see cref="Vector3D"/> / <see cref="ArcData"/>），
    /// 不依赖 AutoCAD 类型，便于：
    /// - JSON 持久化（<c>.roaddesign.json</c>）；
    /// - v2 Blender 插件读取交叉口结构；
    /// - Domain 单元测试（无需 AutoCAD 进程）。
    ///
    /// Infrastructure 版本保留用于真实 AutoCAD 交互，通过 <c>RoadGeometryBridge</c> 双向转换（P0.8）。
    /// </summary>
    public sealed class RoadArm
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>对应 Domain 侧的平面线位 ID（可选，为 Empty 表示未关联）。</summary>
        public Guid AlignmentId { get; set; }

        public Point3D LeftInner { get; set; }
        public Point3D RightInner { get; set; }
        public Point3D LeftOuter { get; set; }
        public Point3D RightOuter { get; set; }
        public Point3D LeftCorner { get; set; }
        public Point3D RightCorner { get; set; }

        /// <summary>道路向外（远离交叉口中心）方向单位向量。</summary>
        public Vector3D OutwardDirection { get; set; }

        /// <summary>左侧缘石圆弧（与道路左边线相连）。</summary>
        public ArcData LeftArc { get; set; }

        /// <summary>右侧缘石圆弧（与道路右边线相连）。</summary>
        public ArcData RightArc { get; set; }

        public override string ToString()
            => $"RoadArm[Id={Id:N}, Left={LeftCorner}, Right={RightCorner}, Outward={OutwardDirection}]";
    }

    /// <summary>
    /// 圆弧的纯数据表示（对 AutoCAD <c>Arc</c> 的 Domain 替代）。
    /// </summary>
    public sealed class ArcData
    {
        /// <summary>圆心。</summary>
        public Point3D Center { get; set; }

        /// <summary>半径。</summary>
        public double Radius { get; set; }

        /// <summary>起始角（弧度，XY 平面）。</summary>
        public double StartAngle { get; set; }

        /// <summary>终止角（弧度，XY 平面）。</summary>
        public double EndAngle { get; set; }

        /// <summary>弧的起点（由 Center / Radius / StartAngle 推导）。</summary>
        public Point3D StartPoint => new Point3D(
            Center.X + Radius * Math.Cos(StartAngle),
            Center.Y + Radius * Math.Sin(StartAngle),
            Center.Z);

        /// <summary>弧的终点（由 Center / Radius / EndAngle 推导）。</summary>
        public Point3D EndPoint => new Point3D(
            Center.X + Radius * Math.Cos(EndAngle),
            Center.Y + Radius * Math.Sin(EndAngle),
            Center.Z);

        public override string ToString()
            => $"Arc[C={Center}, R={Radius:F2}, {StartAngle:F2}..{EndAngle:F2}]";
    }
}
