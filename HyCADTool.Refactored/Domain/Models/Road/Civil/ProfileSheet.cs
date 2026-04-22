using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 纵断设计图分幅（045 / M2 占位）。
    ///
    /// 对应用户草案图中 `纵断设计图组1 → 0~175 / 175~350 / …` 的分幅节点；
    /// 承载单幅图纸的桩号区间 + 比例尺 + 图号；
    /// 挂在 <see cref="Profile"/> 的派生集合下（v2.0 仅作为占位，未接入出图服务）。
    /// </summary>
    public sealed class ProfileSheet
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>图幅名称，默认 "{StartStation:F0}~{EndStation:F0}"。</summary>
        public string Name { get; set; }

        /// <summary>起始桩号（m，显示桩号，随 Alignment.StationEquations 换算）。</summary>
        public double StartStation { get; set; }

        /// <summary>结束桩号（m）。</summary>
        public double EndStation { get; set; }

        /// <summary>水平比例（默认 1:1000）。仅存分母。</summary>
        public double ScaleHorizontal { get; set; } = 1000;

        /// <summary>垂直比例（默认 1:100）。仅存分母。</summary>
        public double ScaleVertical { get; set; } = 100;

        /// <summary>图号。</summary>
        public string SheetNumber { get; set; }

        public override string ToString()
            => $"ProfileSheet[{Name}, K{StartStation:F0}~K{EndStation:F0}]";
    }
}
