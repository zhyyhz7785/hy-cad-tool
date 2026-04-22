using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road.Civil;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 纵断面（沿桩号的高程变化）。
    ///
    /// 设计约束：
    /// - <see cref="Id"/> 稳定 GUID；v1 暂不写 DWG Xdata（Profile 没有对应 DWG 几何，仅在 JSON 中存在）；
    /// - 领域层不引用 AutoCAD 类型；
    /// - 与 <see cref="Alignment"/> 通过 父子关系（Alignment.Profiles）关联，无独立 ParentId 字段。
    /// </summary>
    public sealed class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>用户可编辑的名称，例如 "设计标高"、"地面线"。</summary>
        public string Name { get; set; }

        /// <summary>是否为设计线（true）或地面线（false）。</summary>
        public bool IsDesignProfile { get; set; } = true;

        /// <summary>
        /// 变坡点序列（纵断面的 PVI - Point of Vertical Intersection）。
        /// 按桩号升序排列。
        /// </summary>
        public List<ProfileVertex> Vertices { get; } = new List<ProfileVertex>();

        /// <summary>
        /// 设计速度（km/h）。用于规范校核（CJJ 37 表 6.2.2 最大纵坡 / CJJ 193 表 4.3.2 竖曲线最小半径）。
        /// 默认 60 km/h（主干路常见值）；可在 <see cref="Presentation.ViewModels.Road"/> 编辑窗口内切换。
        /// </summary>
        public int DesignSpeed { get; set; } = 60;

        /// <summary>
        /// 最近一次变更时间（UTC）。由 <c>RoadProfileService</c> 在 ReplaceVertices 等写入操作中刷新；
        /// 与聚合根的 <c>RoadDesign.LastModifiedUtc</c> 双层记录，便于 v2 增量同步时按对象级颗粒度刷新。
        /// </summary>
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 纵断设计图分幅集合（045 / M2 占位，Schema v2.0 新增）。
        /// <para>对应用户草案图中「纵断设计图组1 → 0~175 / 175~350 / …」的分幅节点；
        /// 支撑项目树 UI 展开，实际出图服务 v2.0 暂不接入。</para>
        /// </summary>
        public List<ProfileSheet> Sheets { get; } = new List<ProfileSheet>();

        public override string ToString() => $"Profile[{Name}, Id={Id:N}, PVI={Vertices.Count}]";
    }

    /// <summary>
    /// 纵断面变坡点（PVI）。
    /// 竖曲线由相邻两个 PVI 间的半径（<see cref="CurveRadius"/>）描述；
    /// v1 仅保留最基础字段，v2 扩展凸 / 凹 / 顶进方向。
    /// </summary>
    public sealed class ProfileVertex
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>桩号（m）。</summary>
        public double Station { get; set; }

        /// <summary>高程（m，绝对）。</summary>
        public double Elevation { get; set; }

        /// <summary>竖曲线半径（m）。0 表示无竖曲线（折线连接）。</summary>
        public double CurveRadius { get; set; } = 0;

        public override string ToString() => $"PVI[K{Station:F2}, H={Elevation:F2}, R={CurveRadius:F0}]";
    }
}
