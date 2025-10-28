using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 几何扩展方法
    /// 提供便捷的几何操作方法
    /// </summary>
    public static class GeometryExtensions
    {
        /// <summary>
        /// 获取向量的法向量（逆时针旋转90度）
        /// </summary>
        public static Vector2D GetNormal(this Vector2D vector)
        {
            return new Vector2D(-vector.Y, vector.X);
        }
        
        /// <summary>
        /// 缩放向量
        /// </summary>
        public static Vector2D Scale(this Vector2D vector, double factor)
        {
            return vector * factor;
        }
        
        /// <summary>
        /// 取反向量
        /// </summary>
        public static Vector2D Negate(this Vector2D vector)
        {
            return -vector;
        }
        
        /// <summary>
        /// 向量加法
        /// </summary>
        public static Vector2D Add(this Vector2D v1, Vector2D v2)
        {
            return v1 + v2;
        }
        
        /// <summary>
        /// 按角度旋转向量
        /// </summary>
        public static Vector2D RotateBy(this Vector2D vector, double angleRadians)
        {
            double cos = Math.Cos(angleRadians);
            double sin = Math.Sin(angleRadians);
            return new Vector2D(
                vector.X * cos - vector.Y * sin,
                vector.X * sin + vector.Y * cos);
        }
    }
}














