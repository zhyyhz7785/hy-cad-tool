using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// 实体扩展方法 (Entity Helper Extension Methods)
    /// 提供实体对象的通用操作
    /// </summary>
    /// <remarks>
    /// 设计目的 (Design Purpose):
    /// 1. 简化实体属性设置 (Simplify Entity Property Setting)
    /// 2. 提供常用几何操作 (Provide Common Geometric Operations)
    /// 3. 增强代码可读性 (Enhance Code Readability)
    /// </remarks>
    public static class EntityHelper
    {
        #region 属性设置 (Property Setting)

        /// <summary>
        /// 设置实体图层 (Set Entity Layer)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="layerName">图层名称 (Layer Name)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T SetLayer<T>(this T entity, string layerName) where T : Entity
        {
            if (!string.IsNullOrEmpty(layerName))
            {
                entity.Layer = layerName;
            }
            return entity;
        }

        /// <summary>
        /// 设置实体颜色 (Set Entity Color)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="colorIndex">颜色索引 (Color Index)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T SetColor<T>(this T entity, short colorIndex) where T : Entity
        {
            entity.ColorIndex = colorIndex;
            return entity;
        }

        /// <summary>
        /// 设置实体线型 (Set Entity Line Type)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="linetypeName">线型名称 (Linetype Name)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T SetLinetype<T>(this T entity, string linetypeName) where T : Entity
        {
            if (!string.IsNullOrEmpty(linetypeName))
            {
                entity.Linetype = linetypeName;
            }
            return entity;
        }

        /// <summary>
        /// 设置实体线宽 (Set Entity Line Weight)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="lineWeight">线宽 (Line Weight)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T SetLineWeight<T>(this T entity, LineWeight lineWeight) where T : Entity
        {
            entity.LineWeight = lineWeight;
            return entity;
        }

        #endregion

        #region 几何变换 (Geometric Transformations)

        /// <summary>
        /// 移动实体 (Move Entity)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="fromPoint">起点 (From Point)</param>
        /// <param name="toPoint">终点 (To Point)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T Move<T>(this T entity, Point3d fromPoint, Point3d toPoint) where T : Entity
        {
            var displacement = toPoint - fromPoint;
            var matrix = Matrix3d.Displacement(displacement);
            entity.TransformBy(matrix);
            return entity;
        }

        /// <summary>
        /// 旋转实体 (Rotate Entity)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="basePoint">旋转基点 (Base Point)</param>
        /// <param name="angle">旋转角度（弧度）(Rotation Angle in Radians)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T Rotate<T>(this T entity, Point3d basePoint, double angle) where T : Entity
        {
            var matrix = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
            entity.TransformBy(matrix);
            return entity;
        }

        /// <summary>
        /// 缩放实体 (Scale Entity)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="basePoint">缩放基点 (Base Point)</param>
        /// <param name="scale">缩放比例 (Scale Factor)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T Scale<T>(this T entity, Point3d basePoint, double scale) where T : Entity
        {
            var matrix = Matrix3d.Scaling(scale, basePoint);
            entity.TransformBy(matrix);
            return entity;
        }

        /// <summary>
        /// 镜像实体 (Mirror Entity)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <param name="point1">镜像线起点 (Mirror Line Point 1)</param>
        /// <param name="point2">镜像线终点 (Mirror Line Point 2)</param>
        /// <param name="eraseSource">是否删除源对象 (Erase Source)</param>
        /// <returns>实体对象（链式调用）</returns>
        public static T Mirror<T>(this T entity, Point3d point1, Point3d point2, bool eraseSource = false) where T : Entity
        {
            var line = new Line3d(point1, point2);
            var plane = new Plane(point1, line.Direction);
            var matrix = Matrix3d.Mirroring(plane);
            entity.TransformBy(matrix);
            return entity;
        }

        #endregion

        #region 几何信息 (Geometric Information)

        /// <summary>
        /// 获取实体的包围盒 (Get Entity Bounding Box)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <returns>包围盒（最小点和最大点）</returns>
        public static (Point3d Min, Point3d Max) GetBounds(this Entity entity)
        {
            var extents = entity.GeometricExtents;
            return (extents.MinPoint, extents.MaxPoint);
        }

        /// <summary>
        /// 获取实体的中心点 (Get Entity Center Point)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <returns>中心点</returns>
        public static Point3d GetCenter(this Entity entity)
        {
            var (min, max) = entity.GetBounds();
            return new Point3d(
                (min.X + max.X) / 2,
                (min.Y + max.Y) / 2,
                (min.Z + max.Z) / 2
            );
        }

        #endregion

        #region 克隆 (Cloning)

        /// <summary>
        /// 深度克隆实体 (Deep Clone Entity)
        /// </summary>
        /// <param name="entity">实体对象 (Entity)</param>
        /// <returns>克隆的实体对象</returns>
        public static T DeepClone<T>(this T entity) where T : Entity
        {
            return (T)entity.Clone();
        }

        #endregion

        #region 特定实体类型扩展 (Specific Entity Type Extensions)

        /// <summary>
        /// 获取多段线的总长度 (Get Polyline Total Length)
        /// </summary>
        public static double GetTotalLength(this Polyline polyline)
        {
            double totalLength = 0;
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                totalLength += polyline.GetDistAtPoint(polyline.GetPoint3dAt(i));
            }
            return polyline.Length;
        }

        /// <summary>
        /// 获取多段线是否闭合 (Check if Polyline is Closed)
        /// </summary>
        public static bool IsClosed(this Polyline polyline)
        {
            return polyline.Closed;
        }

        /// <summary>
        /// 反转多段线方向 (Reverse Polyline Direction)
        /// </summary>
        public static Polyline Reverse(this Polyline polyline)
        {
            var newPoly = (Polyline)polyline.Clone();
            newPoly.ReverseCurve();
            return newPoly;
        }

        /// <summary>
        /// 获取线段长度 (Get Line Length)
        /// </summary>
        public static double GetLength(this Line line)
        {
            return line.Length;
        }

        /// <summary>
        /// 获取线段中点 (Get Line Midpoint)
        /// </summary>
        public static Point3d GetMidpoint(this Line line)
        {
            return new Point3d(
                (line.StartPoint.X + line.EndPoint.X) / 2,
                (line.StartPoint.Y + line.EndPoint.Y) / 2,
                (line.StartPoint.Z + line.EndPoint.Z) / 2
            );
        }

        /// <summary>
        /// 获取线段角度 (Get Line Angle)
        /// </summary>
        public static double GetAngle(this Line line)
        {
            var direction = line.EndPoint - line.StartPoint;
            return Math.Atan2(direction.Y, direction.X);
        }

        #endregion
    }
}

