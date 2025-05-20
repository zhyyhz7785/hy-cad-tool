using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        /// <summary>
        /// 将 Point2d 转换为 Point3d，Z 坐标默认为 0
        /// </summary>
        /// <param name="point2d">输入的 Point2d</param>
        /// <returns>转换后的 Point3d</returns>
        public static Point3d ConvertPoint2dTo3d(this Point2d point2d)
        {
            return new Point3d(point2d.X, point2d.Y, 0);
            var a = new Point3d();     
            
        }
        /// <summary>
        /// 将 Point3d 转换为 Point2d，忽略 Z 坐标
        /// </summary>
        /// <param name="point3d">输入的 Point3d</param>
        /// <returns>转换后的 Point2d</returns>
        public static Point2d ConvertPoint3dTo2d(this Point3d point3d)
        {
            return new Point2d(point3d.X, point3d.Y);
        }
        /// <summary>
        /// 将 Point2d 集合转换为 Point3d 集合，Z 坐标默认为 0
        /// </summary>
        /// <param name="points2d">输入的 Point2d 集合</param>
        /// <returns>转换后的 Point3d 集合</returns>
        public static List<Point3d> ConvertPoint2dCollectionTo3d(this IEnumerable<Point2d> points2d)
        {
            List<Point3d> points3d = new List<Point3d>();
            foreach (var point2d in points2d)
            {
                points3d.Add(ConvertPoint2dTo3d(point2d));
            }
            return points3d;
        }
        /// <summary>
        /// 将 Point3d 集合转换为 Point2d 集合，忽略 Z 坐标
        /// </summary>
        /// <param name="points3d">输入的 Point3d 集合</param>
        /// <returns>转换后的 Point2d 集合</returns>
        public static List<Point2d> ConvertPoint3dCollectionTo2d(this IEnumerable<Point3d> points3d)
        {
            List<Point2d> points2d = new List<Point2d>();
            foreach (var point3d in points3d)
            {
                points2d.Add(ConvertPoint3dTo2d(point3d));
            }
            return points2d;
        }
        public static Point2d Point3dTo2d(this Point3d point3D)
        {
            return point3D.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis));
        }
        public static Point3d Point2dTo3d(this Point2d point2D)
        {
            return new Point3d(point2D.X, point2D.Y, 0);
        }
    }
}
