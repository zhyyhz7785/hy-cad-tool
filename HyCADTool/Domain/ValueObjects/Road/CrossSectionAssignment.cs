using System;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 一条路线在桩号区间上引用的标准横断面模板（Template）。
    /// 存储在 <see cref="Models.Road.Alignment.CrossSectionAssignments"/> 中，Template 仍位于 <see cref="Models.Road.RoadDesign.Templates"/> 共享池。
    /// </summary>
    public sealed class CrossSectionAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>引用的 <see cref="Models.Road.Template.Id"/>。</summary>
        public Guid TemplateId { get; set; }

        /// <summary>区段起点桩号（米，相对 Alignment 中心线从 BP 起算）。</summary>
        public double StartStation { get; set; }

        /// <summary>区段终点桩号（米，闭区间 [Start,End] 语义由 <see cref="IsContains"/> 定义）。</summary>
        public double EndStation { get; set; }

        /// <summary>备注，可选。</summary>
        public string Note { get; set; }

        /// <summary>桩号 <paramref name="stationRaw"/> 是否落在此区段内（容差 1e-6 m）。</summary>
        public bool IsContains(double stationRaw, double toleranceM = 1e-6)
        {
            if (double.IsNaN(stationRaw) || double.IsInfinity(stationRaw)) return false;
            double lo = Math.Min(StartStation, EndStation);
            double hi = Math.Max(StartStation, EndStation);
            return stationRaw + toleranceM >= lo && stationRaw - toleranceM <= hi;
        }

        /// <summary>校验区段在路线全长内且长度为正。</summary>
        public (bool Ok, string Error) Validate(double totalLengthM)
        {
            if (totalLengthM < 0) return (false, "路线全长非法。");
            if (StartStation < -1e-6 || EndStation < -1e-6) return (false, "桩号不能为负。");
            if (Math.Abs(StartStation - EndStation) < 1e-9) return (false, "区段长度过短。");
            double hi = Math.Max(StartStation, EndStation);
            if (hi > totalLengthM + 1e-3) return (false, $"区段超出路线全长（{hi:F3} > {totalLengthM:F3} m）。");
            return (true, null);
        }
    }
}
