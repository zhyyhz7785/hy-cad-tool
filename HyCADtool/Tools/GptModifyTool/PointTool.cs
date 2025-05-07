using Autodesk.AutoCAD.Geometry;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
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
