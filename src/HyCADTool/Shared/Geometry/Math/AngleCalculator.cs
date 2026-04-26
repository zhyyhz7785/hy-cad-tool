using HyCADTool.Shared.Geometry;
using System;

namespace HyCADTool.Shared.Geometry.Math
{
    /// <summary>
    /// 角度计算器 - 纯数学计算，平台无关
    /// Angle Calculator - Pure mathematical calculations, platform-independent
    /// </summary>
    public static class AngleCalculator
    {
        /// <summary>
        /// 弧度转角度
        /// Convert radians to degrees
        /// </summary>
        /// <param name="radians">弧度 Radians</param>
        /// <returns>角度 Degrees</returns>
        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / System.Math.PI;
        }

        /// <summary>
        /// 角度转弧度
        /// Convert degrees to radians
        /// </summary>
        /// <param name="degrees">角度 Degrees</param>
        /// <returns>弧度 Radians</returns>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * System.Math.PI / 180.0;
        }

        /// <summary>
        /// 归一化角度到 [0, 2π)
        /// Normalize angle to [0, 2π)
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>归一化角度 Normalized angle</returns>
        public static double NormalizeAngle(double angle)
        {
            while (angle < 0)
                angle += 2 * System.Math.PI;
            while (angle >= 2 * System.Math.PI)
                angle -= 2 * System.Math.PI;
            return angle;
        }

        /// <summary>
        /// 归一化角度到 [-π, π)
        /// Normalize angle to [-π, π)
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>归一化角度 Normalized angle</returns>
        public static double NormalizeAngleSymmetric(double angle)
        {
            angle = NormalizeAngle(angle);
            if (angle > System.Math.PI)
                angle -= 2 * System.Math.PI;
            return angle;
        }

        /// <summary>
        /// 计算两向量夹角
        /// Calculate angle between two vectors
        /// </summary>
        /// <param name="v1">第一个向量 First vector</param>
        /// <param name="v2">第二个向量 Second vector</param>
        /// <returns>夹角（弧度）[0, π] Angle in radians [0, π]</returns>
        public static double AngleBetweenVectors(Vector2D v1, Vector2D v2)
        {
            double dot = Vector2D.Dot(v1, v2);
            double mag1 = v1.Length;
            double mag2 = v2.Length;
            
            if (mag1 < 1e-10 || mag2 < 1e-10)
                return 0;
            
            double cos = dot / (mag1 * mag2);
            cos = System.Math.Max(-1, System.Math.Min(1, cos)); // Clamp to [-1, 1]
            
            return System.Math.Acos(cos);
        }

        /// <summary>
        /// 计算有向角度（从 v1 到 v2 的逆时针角度）
        /// Calculate directed angle (counterclockwise from v1 to v2)
        /// </summary>
        /// <param name="v1">第一个向量 First vector</param>
        /// <param name="v2">第二个向量 Second vector</param>
        /// <returns>有向角度（弧度）[-π, π] Directed angle in radians [-π, π]</returns>
        public static double DirectedAngle(Vector2D v1, Vector2D v2)
        {
            double angle1 = VectorAngle(v1);
            double angle2 = VectorAngle(v2);
            
            double diff = angle2 - angle1;
            return NormalizeAngleSymmetric(diff);
        }

        /// <summary>
        /// 计算向量角度（相对于 X 轴）
        /// Calculate vector angle relative to X axis
        /// </summary>
        /// <param name="vector">向量 Vector</param>
        /// <returns>角度（弧度）[-π, π] Angle in radians [-π, π]</returns>
        public static double VectorAngle(Vector2D vector)
        {
            return System.Math.Atan2(vector.Y, vector.X);
        }

        /// <summary>
        /// 判断角度是否相等（在指定容差内）
        /// Check if angles are equal within tolerance
        /// </summary>
        /// <param name="angle1">第一个角度（弧度）First angle in radians</param>
        /// <param name="angle2">第二个角度（弧度）Second angle in radians</param>
        /// <param name="tolerance">容差（弧度）Tolerance in radians</param>
        /// <returns>是否相等 True if equal</returns>
        public static bool AreAnglesEqual(double angle1, double angle2, double tolerance = 1e-10)
        {
            // 归一化到 [-π, π)
            angle1 = NormalizeAngleSymmetric(angle1);
            angle2 = NormalizeAngleSymmetric(angle2);
            
            double diff = System.Math.Abs(angle1 - angle2);
            
            // 考虑 π 和 -π 的情况
            if (diff > System.Math.PI)
                diff = 2 * System.Math.PI - diff;
            
            return diff < tolerance;
        }

        /// <summary>
        /// 计算角度差（angle2 - angle1）
        /// Calculate angle difference (angle2 - angle1)
        /// </summary>
        /// <param name="angle1">第一个角度（弧度）First angle in radians</param>
        /// <param name="angle2">第二个角度（弧度）Second angle in radians</param>
        /// <returns>角度差（弧度）[-π, π] Angle difference in radians [-π, π]</returns>
        public static double AngleDifference(double angle1, double angle2)
        {
            return NormalizeAngleSymmetric(angle2 - angle1);
        }

        /// <summary>
        /// 线性插值角度
        /// Linear interpolation of angles
        /// </summary>
        /// <param name="angle1">起始角度（弧度）Start angle in radians</param>
        /// <param name="angle2">结束角度（弧度）End angle in radians</param>
        /// <param name="t">插值参数 [0,1] Interpolation parameter [0,1]</param>
        /// <returns>插值角度（弧度）Interpolated angle in radians</returns>
        public static double LerpAngle(double angle1, double angle2, double t)
        {
            double diff = AngleDifference(angle1, angle2);
            return angle1 + diff * t;
        }

        /// <summary>
        /// 判断角度是否在指定范围内（考虑角度的周期性）
        /// Check if angle is within range (considering angle periodicity)
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <param name="minAngle">最小角度（弧度）Minimum angle in radians</param>
        /// <param name="maxAngle">最大角度（弧度）Maximum angle in radians</param>
        /// <returns>是否在范围内 True if within range</returns>
        public static bool IsAngleInRange(double angle, double minAngle, double maxAngle)
        {
            angle = NormalizeAngle(angle);
            minAngle = NormalizeAngle(minAngle);
            maxAngle = NormalizeAngle(maxAngle);
            
            if (minAngle <= maxAngle)
            {
                return angle >= minAngle && angle <= maxAngle;
            }
            else
            {
                // 跨越 0 度的情况
                return angle >= minAngle || angle <= maxAngle;
            }
        }

        /// <summary>
        /// 计算三点的夹角（以 p2 为顶点）
        /// Calculate angle at p2 formed by p1-p2-p3
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">顶点 Vertex point</param>
        /// <param name="p3">第三个点 Third point</param>
        /// <returns>夹角（弧度）[0, π] Angle in radians [0, π]</returns>
        public static double AngleAtVertex(Point2D p1, Point2D p2, Point2D p3)
        {
            var v1 = new Vector2D(p1.X - p2.X, p1.Y - p2.Y);
            var v2 = new Vector2D(p3.X - p2.X, p3.Y - p2.Y);
            
            return AngleBetweenVectors(v1, v2);
        }

        /// <summary>
        /// 计算三点的有向角（从 p1-p2 到 p2-p3 的逆时针角度）
        /// Calculate directed angle from p1-p2 to p2-p3 (counterclockwise)
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">顶点 Vertex point</param>
        /// <param name="p3">第三个点 Third point</param>
        /// <returns>有向角度（弧度）[-π, π] Directed angle in radians [-π, π]</returns>
        public static double DirectedAngleAtVertex(Point2D p1, Point2D p2, Point2D p3)
        {
            var v1 = new Vector2D(p1.X - p2.X, p1.Y - p2.Y);
            var v2 = new Vector2D(p3.X - p2.X, p3.Y - p2.Y);
            
            return DirectedAngle(v1, v2);
        }

        /// <summary>
        /// 判断角度是否为直角（在容差内）
        /// Check if angle is right angle within tolerance
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <param name="tolerance">容差（弧度）Tolerance in radians</param>
        /// <returns>是否为直角 True if right angle</returns>
        public static bool IsRightAngle(double angle, double tolerance = 1e-10)
        {
            angle = System.Math.Abs(NormalizeAngle(angle));
            return System.Math.Abs(angle - System.Math.PI / 2) < tolerance || 
                   System.Math.Abs(angle - 3 * System.Math.PI / 2) < tolerance;
        }

        /// <summary>
        /// 判断角度是否为平角（在容差内）
        /// Check if angle is straight angle within tolerance
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <param name="tolerance">容差（弧度）Tolerance in radians</param>
        /// <returns>是否为平角 True if straight angle</returns>
        public static bool IsStraightAngle(double angle, double tolerance = 1e-10)
        {
            angle = System.Math.Abs(NormalizeAngle(angle));
            return System.Math.Abs(angle - System.Math.PI) < tolerance;
        }

        /// <summary>
        /// 判断角度是否为锐角
        /// Check if angle is acute
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>是否为锐角 True if acute</returns>
        public static bool IsAcuteAngle(double angle)
        {
            angle = System.Math.Abs(NormalizeAngle(angle));
            return angle > 0 && angle < System.Math.PI / 2;
        }

        /// <summary>
        /// 判断角度是否为钝角
        /// Check if angle is obtuse
        /// </summary>
        /// <param name="angle">角度（弧度）Angle in radians</param>
        /// <returns>是否为钝角 True if obtuse</returns>
        public static bool IsObtuseAngle(double angle)
        {
            angle = System.Math.Abs(NormalizeAngle(angle));
            return angle > System.Math.PI / 2 && angle < System.Math.PI;
        }
    }
}

