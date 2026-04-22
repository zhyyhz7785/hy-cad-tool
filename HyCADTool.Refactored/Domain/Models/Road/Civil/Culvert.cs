using System;

namespace HyCADTool.Refactored.Domain.Models.Road.Civil
{
    /// <summary>
    /// 涵洞（045 / M2 占位）。v2.0 仅最小字段。
    /// </summary>
    public sealed class Culvert
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }

        /// <summary>所属 Alignment；null 表示未归属到任何路线。</summary>
        public Guid? OwnerAlignmentId { get; set; }

        /// <summary>中心桩号（m）。</summary>
        public double Station { get; set; }

        /// <summary>与中线的斜交角（度）；90 = 正交。</summary>
        public double SkewDeg { get; set; } = 90;

        /// <summary>涵洞类型（盖板/箱涵/圆管等）。</summary>
        public string CulvertType { get; set; }

        public override string ToString()
            => $"Culvert[{Name}, Id={Id:N}, K{Station:F0}]";
    }
}
