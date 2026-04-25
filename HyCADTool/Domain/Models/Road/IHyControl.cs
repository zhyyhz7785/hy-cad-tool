using System;

namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 道路领域「控制体（Control）」根标签接口（M6）。
    ///
    /// <para><b>语义</b></para>
    /// 控制体 = 现实世界不存在但在设计过程中需要的辅助参考符号：
    /// <list type="bullet">
    ///   <item>参考点（ReferencePoint）：锚点、基准点、示意点</item>
    ///   <item>参考线（ReferenceLine）：参考轴、中心线投影线、桩号虚线</item>
    ///   <item>参考面（ReferencePlane）：参考面、标高面</item>
    ///   <item>选中集合（SelectionSet）：临时保存的一组实体 ID</item>
    /// </list>
    ///
    /// <para><b>与 <see cref="IHyEntity"/> 的区别</b></para>
    /// <list type="bullet">
    ///   <item>控制体仅在 AutoCAD 平面图上显示（临时图层，<see cref="IsTransient"/>=true 时不入库只走瞬态）</item>
    ///   <item>不参与 3D 导出（Blender / Lumion 忽略）</item>
    ///   <item>在 <c>.roaddesign.json</c> 中走 <c>Controls</c> 数组</item>
    ///   <item>DWG Xdata 前缀为 <c>Control.*</c>（<c>Control.ReferencePoint</c> / <c>Control.ReferenceLine</c> / ...）</item>
    /// </list>
    /// </summary>
    public interface IHyControl
    {
        /// <summary>稳定 GUID。</summary>
        Guid Id { get; }

        /// <summary>类型标签（"Control.ReferencePoint" / "Control.ReferenceLine" / ...）。</summary>
        string Kind { get; }

        /// <summary>用户可见名称。</summary>
        string Name { get; }

        /// <summary>
        /// 是否瞬态：true = 仅通过 TransientManager 显示、不写入 DWG、不参与 JSON 持久化；
        /// false = 永久可见控制体（临时图层的永久图元，可重绘）。
        /// </summary>
        bool IsTransient { get; }
    }
}
