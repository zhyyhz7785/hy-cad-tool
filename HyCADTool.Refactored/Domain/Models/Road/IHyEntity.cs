using System;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 道路领域「实体（Entity）」根标签接口（M6）。
    ///
    /// <para><b>语义</b></para>
    /// 实体 = 现实世界真实存在、需要真实模拟的对象：
    /// <list type="bullet">
    ///   <item>AutoCAD 平面图上可见（永久图形）</item>
    ///   <item>Blender 中 3D 显示（未来 v2/P7）</item>
    ///   <item>参与 <c>.roaddesign.json</c> 持久化</item>
    ///   <item>挂 <c>HY_ROAD</c> Xdata（KIND + ID）</item>
    /// </list>
    ///
    /// <para><b>已存在的实体类（M6 仅做标记，不改类体）</b></para>
    /// <see cref="Alignment"/> / <see cref="Profile"/> / <see cref="Template"/> /
    /// <see cref="Corridor"/> / <see cref="Intersection"/> / <see cref="RoadNode"/>
    /// 这些类并不在 M6 阶段直接 implement 本接口（保持 JSON 向后兼容）；
    /// 本接口主要用于新增的值对象（比如 M7 的 <c>StructureLayer</c>）与测试断言中的类型分组。
    ///
    /// <para><b>与 <see cref="IHyControl"/> 的二分关系</b></para>
    /// 实体是"真图元 + 真数据"；控制体是"辅助参考 + 临时可见"。
    /// 导出 Blender / Lumion 时仅导出 <see cref="IHyEntity"/>，不导 <see cref="IHyControl"/>。
    /// </summary>
    public interface IHyEntity
    {
        /// <summary>稳定 GUID，与 DWG Xdata 的 <c>HY_ROAD.ID</c> 对齐。</summary>
        Guid Id { get; }

        /// <summary>类型标签（"Alignment" / "Template" / "StructureLayer" / ...）。</summary>
        string Kind { get; }

        /// <summary>用户可见名称。</summary>
        string Name { get; }
    }
}
