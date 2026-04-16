using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 纵断面（沿桩号的高程变化）。
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
