using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Models.Road.ControlElements
{
    /// <summary>
    /// 参考点（ReferencePoint）—— 一种 <see cref="IHyControl"/>。
    ///
    /// <para><b>用途</b></para>
    /// 标记设计过程中的关键位置（锚点 / 基准点 / 视距校核点 / 临时量距点），
    /// 平面图上用一个十字符号或圆点显示；不参与 3D 导出。
    ///
    /// <para><b>Kind 常量</b></para>
    /// <c>"Control.ReferencePoint"</c>，与 <see cref="HyCADTool.Shared.AutoCAD.Xdata.HyRoadXdata"/> 对齐。
    /// </summary>
    public sealed class ReferencePoint : IHyControl
    {
        /// <summary>Kind 常量，用于 Xdata 和 JSON 多态解析。</summary>
        public const string KindConstant = "Control.ReferencePoint";

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>类型标签；Newtonsoft 序列化时写入、反序列化时用于多态派发。</summary>
        public string Kind { get; set; } = KindConstant;

        public string Name { get; set; }

        /// <summary>世界坐标位置。</summary>
        public Point3D Position { get; set; }

        /// <summary>是否瞬态（不入库只通过 TransientManager 显示）。</summary>
        public bool IsTransient { get; set; }

        /// <summary>可选的备注说明，出现在 Tooltip 里。</summary>
        public string Remark { get; set; }

        public override string ToString()
            => $"ReferencePoint[{Name}, Id={Id:N}, Pos=({Position.X:F2},{Position.Y:F2},{Position.Z:F2})]";
    }
}
