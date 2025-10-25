using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 曲线分割服务 - Infrastructure 层（AutoCAD 特定）
    /// 
    /// 职责：
    /// 1. 计算曲线交点（使用 AutoCAD API）
    /// 2. 分割曲线段（使用 GetSplitCurves）
    /// 3. 分解多段线为基本曲线
    /// </summary>
    public class CurveSegmentService
    {
        private readonly double _tolerance;

        public CurveSegmentService(double tolerance = 1e-9)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// 计算两条曲线的交点
        /// </summary>
        public Point3dCollection CalculateIntersections(Curve curve1, Curve curve2)
        {
            var intersectionPoints = new Point3dCollection();
            curve1.IntersectWith(curve2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
            return intersectionPoints;
        }

        /// <summary>
        /// 检查点是否在曲线上
        /// </summary>
        public bool IsPointOnCurve(Curve curve, Point3d point)
        {
            double param;
            try
            {
                param = curve.GetParameterAtPoint(point);
            }
            catch
            {
                return false;
            }

            return param >= curve.StartParam - _tolerance &&
                   param <= curve.EndParam + _tolerance;
        }

        /// <summary>
        /// 获取点在曲线上的参数
        /// </summary>
        public double GetParameterAtPoint(Curve curve, Point3d point)
        {
            try
            {
                return curve.GetParameterAtPoint(point);
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// 分割曲线段
        /// </summary>
        public Curve SplitCurveSegment(Curve curve, double paramStart, double paramEnd)
        {
            if (curve == null)
                return null;

            // 确保参数顺序正确
            if (paramStart > paramEnd)
            {
                double temp = paramStart;
                paramStart = paramEnd;
                paramEnd = temp;
            }

            try
            {
                // 特殊处理：Circle（圆）
                if (curve is Circle circle)
                {
                    // 圆的参数范围是 0 到 2π
                    // 使用 GetSplitCurves 但只取一个分割点的方式不可靠
                    // 改用直接创建 Arc 的方式
                    
                    Point3d startPt = circle.GetPointAtParameter(paramStart);
                    Point3d endPt = circle.GetPointAtParameter(paramEnd);

                    // 计算圆心到起点和终点的向量
                    Vector3d startVec = startPt - circle.Center;
                    Vector3d endVec = endPt - circle.Center;
                    
                    // 计算角度（相对于 X 轴）
                    double startAngle = Math.Atan2(startVec.Y, startVec.X);
                    double endAngle = Math.Atan2(endVec.Y, endVec.X);

                    // 标准化角度到 [0, 2π) 范围
                    if (startAngle < 0) startAngle += 2 * Math.PI;
                    if (endAngle < 0) endAngle += 2 * Math.PI;

                    // 确保 endAngle > startAngle（沿逆时针方向）
                    // 如果 endAngle <= startAngle，说明弧跨越了 0 度
                    if (endAngle <= startAngle)
                    {
                        endAngle += 2 * Math.PI;
                    }

                    // 创建圆弧（Arc 构造函数：中心点、法向量、半径、起始角、终止角）
                    Arc arc = new Arc(circle.Center, circle.Normal, circle.Radius, startAngle, endAngle);
                    return arc;
                }

                // 其他曲线：使用 GetSplitCurves 方法
                Point3d startPt2 = curve.GetPointAtParameter(paramStart);
                Point3d endPt2 = curve.GetPointAtParameter(paramEnd);

                // 创建分割点集合
                Point3dCollection splitPoints = new Point3dCollection { startPt2, endPt2 };

                // 使用GetSplitCurves方法
                DBObjectCollection segments = curve.GetSplitCurves(splitPoints);

                // 找到起点和终点匹配的曲线段
                Curve result = null;
                int matchedIndex = -1;

                for (int i = 0; i < segments.Count; i++)
                {
                    Curve segment = segments[i] as Curve;
                    if (segment != null)
                    {
                        Point3d segStart = segment.StartPoint;
                        Point3d segEnd = segment.EndPoint;

                        // 检查曲线段的起点和终点是否匹配
                        bool matchForward = segStart.IsEqualTo(startPt2, Tolerance.Global) && 
                                          segEnd.IsEqualTo(endPt2, Tolerance.Global);
                        bool matchReverse = segStart.IsEqualTo(endPt2, Tolerance.Global) && 
                                          segEnd.IsEqualTo(startPt2, Tolerance.Global);

                        if (matchForward || matchReverse)
                        {
                            matchedIndex = i;
                            result = segment.Clone() as Curve;
                            break;
                        }
                    }
                }

                // 释放所有未使用的段
                for (int i = 0; i < segments.Count; i++)
                {
                    if (i != matchedIndex && segments[i] != null)
                    {
                        segments[i].Dispose();
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将 Polyline 分解为基本曲线
        /// </summary>
        public List<Curve> ExplodePolyline(Polyline pline)
        {
            var curves = new List<Curve>();

            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                if (pline.GetSegmentType(i) == SegmentType.Line)
                {
                    // 直线段
                    Point3d start = pline.GetPoint3dAt(i);
                    Point3d end = pline.GetPoint3dAt(i + 1);
                    curves.Add(new Line(start, end));
                }
                else if (pline.GetSegmentType(i) == SegmentType.Arc)
                {
                    // 弧线段
                    CircularArc2d arc2d = pline.GetArcSegment2dAt(i);
                    if (arc2d != null)
                    {
                        Point3d center3d = new Point3d(arc2d.Center.X, arc2d.Center.Y, pline.Elevation);
                        Vector3d normal = pline.Normal;
                        Arc arc3d = new Arc(center3d, normal, arc2d.Radius, arc2d.StartAngle, arc2d.EndAngle);
                        curves.Add(arc3d);
                    }
                }
            }

            // 处理闭合多段线
            if (pline.Closed && pline.NumberOfVertices > 2)
            {
                int lastIndex = pline.NumberOfVertices - 1;
                if (pline.GetSegmentType(lastIndex) == SegmentType.Line)
                {
                    Point3d start = pline.GetPoint3dAt(lastIndex);
                    Point3d end = pline.GetPoint3dAt(0);
                    curves.Add(new Line(start, end));
                }
                else if (pline.GetSegmentType(lastIndex) == SegmentType.Arc)
                {
                    CircularArc2d arc2d = pline.GetArcSegment2dAt(lastIndex);
                    if (arc2d != null)
                    {
                        Point3d center3d = new Point3d(arc2d.Center.X, arc2d.Center.Y, pline.Elevation);
                        Vector3d normal = pline.Normal;
                        Arc arc3d = new Arc(center3d, normal, arc2d.Radius, arc2d.StartAngle, arc2d.EndAngle);
                        curves.Add(arc3d);
                    }
                }
            }

            return curves;
        }

        /// <summary>
        /// 将 Polyline2d 分解为 Line 段
        /// </summary>
        public List<Curve> ExplodePolyline2d(Polyline2d pline2d, Transaction trans)
        {
            var lines = new List<Curve>();
            var vertices = new List<Point3d>();

            // 获取所有顶点
            foreach (ObjectId vId in pline2d)
            {
                Vertex2d vertex = trans.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                if (vertex != null)
                {
                    vertices.Add(vertex.Position);
                }
            }

            // 创建线段
            for (int i = 0; i < vertices.Count - 1; i++)
            {
                lines.Add(new Line(vertices[i], vertices[i + 1]));
            }

            // 处理闭合
            if (pline2d.Closed && vertices.Count > 2)
            {
                lines.Add(new Line(vertices[vertices.Count - 1], vertices[0]));
            }

            return lines;
        }
    }
}

