using System;
using System.Linq;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维样条值对象（平台无关）
    /// 存储样条的关键信息以便重建
    /// </summary>
    public readonly struct Spline2D : IEquatable<Spline2D>
    {
        /// <summary>
        /// 控制点数组
        /// </summary>
        public Point2D[] ControlPoints { get; }

        /// <summary>
        /// 样条阶数
        /// </summary>
        public int Degree { get; }

        /// <summary>
        /// 起点
        /// </summary>
        public Point2D StartPoint { get; }

        /// <summary>
        /// 终点
        /// </summary>
        public Point2D EndPoint { get; }

        public Spline2D(Point2D[] controlPoints, int degree, Point2D startPoint, Point2D endPoint)
        {
            if (controlPoints == null || controlPoints.Length < 2)
                throw new ArgumentException("At least 2 control points required", nameof(controlPoints));
            if (degree < 1)
                throw new ArgumentException("Degree must be at least 1", nameof(degree));

            ControlPoints = controlPoints;
            Degree = degree;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        /// <summary>
        /// 控制点数量
        /// </summary>
        public int ControlPointCount => ControlPoints?.Length ?? 0;

        #region IEquatable Implementation

        public bool Equals(Spline2D other)
        {
            if (Degree != other.Degree)
                return false;

            if (!StartPoint.Equals(other.StartPoint) || !EndPoint.Equals(other.EndPoint))
                return false;

            if (ControlPoints == null && other.ControlPoints == null)
                return true;

            if (ControlPoints == null || other.ControlPoints == null)
                return false;

            if (ControlPoints.Length != other.ControlPoints.Length)
                return false;

            for (int i = 0; i < ControlPoints.Length; i++)
            {
                if (!ControlPoints[i].Equals(other.ControlPoints[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is Spline2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Degree.GetHashCode();
                hash = (hash * 397) ^ StartPoint.GetHashCode();
                hash = (hash * 397) ^ EndPoint.GetHashCode();
                if (ControlPoints != null)
                {
                    foreach (var point in ControlPoints)
                    {
                        hash = (hash * 397) ^ point.GetHashCode();
                    }
                }
                return hash;
            }
        }

        public static bool operator ==(Spline2D left, Spline2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Spline2D left, Spline2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Spline2D[Degree={Degree}, ControlPoints={ControlPointCount}, Start={StartPoint}, End={EndPoint}]";
        }
    }
}

























