using System;

namespace HyCAD.Geometry
{
    /// <summary>
    /// 曲线段类型枚举
    /// </summary>
    public enum CurveSegmentType
    {
        Line,
        Arc,
        Ellipse,
        Spline
    }

    /// <summary>
    /// 曲线段统一封装类
    /// 包含原始几何信息 + 简化直线（用于 DCEL 构建）
    /// </summary>
    public class CurveSegment2D
    {
        /// <summary>
        /// 曲线段类型
        /// </summary>
        public CurveSegmentType Type { get; }

        /// <summary>
        /// 简化为直线（用于 DCEL 构建）
        /// 所有曲线类型都会简化为起点到终点的直线
        /// </summary>
        public Line2D SimplifiedLine { get; }

        /// <summary>
        /// 原始直线（仅当 Type == Line 时有值）
        /// </summary>
        public Line2D? OriginalLine { get; }

        /// <summary>
        /// 原始圆弧（仅当 Type == Arc 时有值）
        /// </summary>
        public Arc2D? OriginalArc { get; }

        /// <summary>
        /// 是否来自完整圆（用于部分匹配时判断方向）
        /// </summary>
        public bool IsFromFullCircle { get; }

        /// <summary>
        /// 原始 bulge 值（如果来自 Polyline）
        /// 保留原始的顺逆时针方向信息
        /// </summary>
        public double? OriginalBulge { get; }

        /// <summary>
        /// 圆心在SimplifiedLine的哪一侧（仅当 Type == Arc 时有效）
        /// > 0: 圆心在SimplifiedLine左侧（法向方向）
        /// < 0: 圆心在SimplifiedLine右侧
        /// = 0: 圆心在SimplifiedLine上（直径）
        /// 用于判断应该绘制短弧还是长弧
        /// </summary>
        public double CenterSide { get; }

        /// <summary>
        /// 原始椭圆（仅当 Type == Ellipse 时有值）
        /// </summary>
        public Ellipse2D? OriginalEllipse { get; }

        /// <summary>
        /// 原始样条（仅当 Type == Spline 时有值）
        /// </summary>
        public Spline2D? OriginalSpline { get; }

        /// <summary>
        /// 构造函数：直线
        /// </summary>
        public CurveSegment2D(Line2D line)
        {
            Type = CurveSegmentType.Line;
            SimplifiedLine = line;
            OriginalLine = line;
        }

        /// <summary>
        /// 构造函数：圆弧
        /// </summary>
        public CurveSegment2D(Arc2D arc, bool isFromFullCircle = false, double? originalBulge = null, double centerSide = 0)
        {
            Type = CurveSegmentType.Arc;
            SimplifiedLine = new Line2D(arc.StartPoint, arc.EndPoint);
            OriginalArc = arc;
            IsFromFullCircle = isFromFullCircle;
            OriginalBulge = originalBulge;
            CenterSide = centerSide;
        }

        /// <summary>
        /// 构造函数：椭圆
        /// </summary>
        public CurveSegment2D(Ellipse2D ellipse)
        {
            Type = CurveSegmentType.Ellipse;
            SimplifiedLine = new Line2D(ellipse.StartPoint, ellipse.EndPoint);
            OriginalEllipse = ellipse;
        }

        /// <summary>
        /// 构造函数：样条
        /// </summary>
        public CurveSegment2D(Spline2D spline)
        {
            Type = CurveSegmentType.Spline;
            SimplifiedLine = new Line2D(spline.StartPoint, spline.EndPoint);
            OriginalSpline = spline;
        }

        /// <summary>
        /// 获取起点
        /// </summary>
        public Point2D StartPoint => SimplifiedLine.StartPoint;

        /// <summary>
        /// 获取终点
        /// </summary>
        public Point2D EndPoint => SimplifiedLine.EndPoint;

        /// <summary>
        /// 获取简化直线长度
        /// </summary>
        public double SimplifiedLength => SimplifiedLine.Length;

        public override string ToString()
        {
            return $"CurveSegment2D[Type={Type}, Start={StartPoint}, End={EndPoint}]";
        }
    }
}

