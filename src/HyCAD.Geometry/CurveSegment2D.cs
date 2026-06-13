namespace HyCAD.Geometry
{
    /// <summary>
    /// 曲线段类型枚举
    /// （原 CurveSegment2D 封装类已随 DCEL 死代码清理删除，仅保留类型枚举供
    /// SimplifiedCurveMapping 标识原始曲线类型）
    /// </summary>
    public enum CurveSegmentType
    {
        Line,
        Arc,
        Ellipse,
        Spline
    }
}
