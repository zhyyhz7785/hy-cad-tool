using HyCAD.Geometry;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>网格单元形状。</summary>
    public enum MeshCellKind
    {
        Rectangle,
        Triangle
    }

    /// <summary>矩形条带方向（三角形为 None）。</summary>
    public enum MeshCellOrientation
    {
        None,
        Vertical,
        Horizontal,
        Square
    }

    /// <summary>混凝土区域分解后的矩形或三角形单元。</summary>
    public sealed class MeshCell
    {
        public MeshCellKind Kind { get; set; }
        public MeshCellOrientation Orientation { get; set; }
        public Polygon2D Polygon { get; set; }
        public double AreaMm2 { get; set; }
    }
}
