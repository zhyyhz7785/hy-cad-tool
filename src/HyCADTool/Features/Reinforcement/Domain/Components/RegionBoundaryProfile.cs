using HyCAD.Geometry;

using System.Collections.Generic;



namespace HyCADTool.Features.Reinforcement.Domain.Components

{

    /// <summary>外轮廓边的环境角色（外侧接触介质，仅土/气两类）。</summary>

    public enum BoundaryEdgeRole

    {

        /// <summary>土壤接触段（割线以下）。</summary>

        Soil,

        /// <summary>空气接触段（割线以上，或非基础图形全轮廓）。</summary>

        Air

    }



    /// <summary>单条已分类外轮廓边。</summary>

    public sealed class ClassifiedBoundaryEdge

    {

        public Line2D Edge { get; set; }

        public BoundaryEdgeRole Role { get; set; }

        public double MidYElevation { get; set; }

        public int SegmentIndex { get; set; }

    }



    /// <summary>计算组标高上下文（图形坐标 mm，埋深输入 m）。</summary>

    public sealed class RegionElevationContext

    {

        /// <summary>1m 对应图形单位 mm。</summary>

        public const double DrawingUnitsPerMeter = 1000.0;

        public int GroupIndex { get; }

        /// <summary>组内全部独立图形外框最低 Y（mm，自动）。</summary>

        public double GroupMinY { get; }

        /// <summary>面板输入的埋置深度（m）。</summary>

        public double EmbedmentDepth { get; }

        /// <summary>面板输入的基础底面标高（仅命令行上报，不参与几何）。</summary>

        public double FoundationElevation { get; }



        /// <summary>土气割线 Y(mm) = GroupMinY + 埋置深度(m) × 1000。</summary>

        public double CutY => GroupMinY + (EmbedmentDepth >= 0 ? EmbedmentDepth : 0) * DrawingUnitsPerMeter;



        public RegionElevationContext(

            int groupIndex,

            double groupMinY,

            double embedmentDepth,

            double foundationElevation)

        {

            GroupIndex = groupIndex;

            GroupMinY = groupMinY;

            EmbedmentDepth = embedmentDepth >= 0 ? embedmentDepth : 0;

            FoundationElevation = foundationElevation;

        }



        public static bool IsFoundationElevationValid(double elevation)

        {

            return !double.IsNaN(elevation) && !double.IsInfinity(elevation);

        }

    }



    /// <summary>单个独立图形外轮廓边分析结果。</summary>

    public sealed class RegionBoundaryProfile

    {

        public int GroupIndex { get; set; }

        public int RegionIndexInGroup { get; set; }

        public RegionElevationContext Context { get; set; }

        public IReadOnlyList<ClassifiedBoundaryEdge> Edges { get; set; }



        /// <summary>是否为组内 ymin 最低的基础图形。</summary>

        public bool IsFoundationGraphic { get; set; }

        public double GraphicMinY { get; set; }

        public double CutY { get; set; } = double.NaN;



        public int SoilCount { get; set; }

        public int AirCount { get; set; }

    }



    /// <summary>整组分析结果。</summary>

    public sealed class GroupBoundaryProfile

    {

        public RegionElevationContext Context { get; set; }

        public IReadOnlyList<RegionBoundaryProfile> Regions { get; set; }

        public double GroupMinX { get; set; }

        public double GroupMaxX { get; set; }

        public double GroupMinY { get; set; }

        public int FoundationGraphicIndex { get; set; } = -1;

        public double CutY { get; set; } = double.NaN;

        public double CutIntersectionMinX { get; set; } = double.NaN;

        public double CutIntersectionMaxX { get; set; } = double.NaN;

        /// <summary>是否为全局 ymin 最低的落地组（仅命令行标注，不影响各组土/气切分）。</summary>
        public bool IsPrimarySoilGroup { get; set; }
    }
}
