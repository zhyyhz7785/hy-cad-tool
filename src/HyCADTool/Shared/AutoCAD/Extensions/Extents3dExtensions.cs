using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Shared.AutoCAD.Extensions
{
    /// <summary>
    /// Extents3d 扩展方法
    /// 提供 AutoCAD Extents3d 与 Domain 类型之间的转换
    /// </summary>
    public static class Extents3dExtensions
    {
        /// <summary>
        /// 将 AutoCAD Extents3d 转换为 Domain BoundingBox (2D, XY平面)
        /// </summary>
        public static BoundingBox ToBoundingBox(this Extents3d extents)
        {
            var minPoint = new Point2D(extents.MinPoint.X, extents.MinPoint.Y);
            var maxPoint = new Point2D(extents.MaxPoint.X, extents.MaxPoint.Y);
            return new BoundingBox(minPoint, maxPoint);
        }

        /// <summary>
        /// 将 Domain BoundingBox 转换为 AutoCAD Extents3d (Z=0)
        /// </summary>
        public static Extents3d ToExtents3d(this BoundingBox box, double z = 0)
        {
            return new Extents3d(
                new Point3d(box.MinPoint.X, box.MinPoint.Y, z),
                new Point3d(box.MaxPoint.X, box.MaxPoint.Y, z));
        }
    }
}

