using System;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 桩号标注参数（<see cref="RoadAlignmentService.DrawStationLabels"/> 使用）。
    ///
    /// v1 所有参数默认硬编码为 AutoCAD 1:1 绘图单位下的常用尺寸；
    /// v1.1 计划改为从 <c>hy-settings.json</c> 的 <c>Road.Station</c> 段读取，允许项目级覆盖。
    /// </summary>
    public sealed class RoadStationLabelOptions
    {
        /// <summary>主桩间隔（m）。默认 20 —— 对标鸿业 / Civil 3D 默认。</summary>
        public double MainInterval { get; set; } = 20.0;

        /// <summary>副桩间隔（m）。默认 5。副桩只画短刻度线，不标桩号文字，避免图面太满。</summary>
        public double SubInterval { get; set; } = 5.0;

        /// <summary>主桩刻度线总长（m，跨越中心线两侧，默认 4 → 每侧 2）。</summary>
        public double TickLengthMain { get; set; } = 4.0;

        /// <summary>副桩刻度线总长（m）。</summary>
        public double TickLengthSub { get; set; } = 1.5;

        /// <summary>桩号文字高度（m）。</summary>
        public double TextHeight { get; set; } = 3.0;

        /// <summary>文字距刻度线末端的外侧偏移（m），避免贴着刻度线。</summary>
        public double TextMargin { get; set; } = 0.5;

        /// <summary>是否把文字沿中心线切向旋转（false 则水平摆放）。默认 true，更专业。</summary>
        public bool RotateTextAlongTangent { get; set; } = true;

        /// <summary>
        /// 文字放在"行进方向的左侧"还是"右侧"。
        /// 国内路线设计图默认在左侧（起点在图左、终点在图右时，桩号标在上方）。
        /// </summary>
        public StationTextSide TextSide { get; set; } = StationTextSide.Left;

        public static RoadStationLabelOptions Default => new RoadStationLabelOptions();

        public void Validate()
        {
            if (MainInterval <= 0) throw new ArgumentOutOfRangeException(nameof(MainInterval));
            if (SubInterval < 0) throw new ArgumentOutOfRangeException(nameof(SubInterval));
            if (TickLengthMain < 0) throw new ArgumentOutOfRangeException(nameof(TickLengthMain));
            if (TickLengthSub < 0) throw new ArgumentOutOfRangeException(nameof(TickLengthSub));
            if (TextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(TextHeight));
        }
    }

    /// <summary>桩号文字挂在中心线哪一侧。</summary>
    public enum StationTextSide
    {
        Left,
        Right
    }
}
