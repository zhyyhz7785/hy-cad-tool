using System;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 几何点标注参数（<c>RoadAlignmentService.DrawGeometryPointLabels</c> 使用）。
    ///
    /// 标注内容（每个几何点一个"钉子 + 引线 + 2 行文字"）：
    /// 1. 标记圆（Circle，半径 <see cref="MarkerRadius"/>）；
    /// 2. 从标记圆法向偏出 <see cref="LeaderLength"/> 画引出线（Line）；
    /// 3. 引线末端水平书写两行文字：第 1 行 = 点名（BC / EC / TS / ...），第 2 行 = 桩号。
    ///
    /// v1 所有参数默认硬编码为 AutoCAD 1:1 绘图单位下的常用尺寸；
    /// v1.1 可扩展为项目级 <c>hy-settings.json / Road.GeometryPoint</c>。
    /// </summary>
    public sealed class RoadGeometryPointLabelOptions
    {
        /// <summary>标记圆半径（m）。默认 0.8。</summary>
        public double MarkerRadius { get; set; } = 0.8;

        /// <summary>标记圆外再画的"引出线"长度（m），沿中心线法向。默认 3.0。</summary>
        public double LeaderLength { get; set; } = 3.0;

        /// <summary>第 1 行（点名）文字高度（m）。</summary>
        public double NameTextHeight { get; set; } = 2.5;

        /// <summary>第 2 行（桩号）文字高度（m）。默认略小于点名。</summary>
        public double StationTextHeight { get; set; } = 2.0;

        /// <summary>文字锚点距引线末端的间隙（m）。</summary>
        public double TextMargin { get; set; } = 0.4;

        /// <summary>文字与点名之间的行距（m）。</summary>
        public double TextLineSpacing { get; set; } = 0.6;

        /// <summary>
        /// 引出线与文字放在行进方向的"左侧"还是"右侧"。
        /// 默认左侧，与 <see cref="RoadStationLabelOptions"/> 保持一致。
        /// </summary>
        public StationTextSide TextSide { get; set; } = StationTextSide.Left;

        /// <summary>
        /// 是否同时为"纯 PI 点"（保留切线交点、未设半径）生成标注。默认 false，
        /// 因为这些点在用户视角里等同于普通折线顶点，加了反而让图面变乱。
        /// </summary>
        public bool LabelPlainPi { get; set; } = false;

        public static RoadGeometryPointLabelOptions Default => new RoadGeometryPointLabelOptions();

        public void Validate()
        {
            if (MarkerRadius <= 0) throw new ArgumentOutOfRangeException(nameof(MarkerRadius));
            if (LeaderLength < 0) throw new ArgumentOutOfRangeException(nameof(LeaderLength));
            if (NameTextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(NameTextHeight));
            if (StationTextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(StationTextHeight));
        }
    }
}
