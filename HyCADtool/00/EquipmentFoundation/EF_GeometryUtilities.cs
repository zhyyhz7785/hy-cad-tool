using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace HyCADTool.Utilities
{
    public static class GeometryUtils
    {
        public static bool IsPointInside(Polyline pline, Point3d point)
        {
            int intersections = 0;
            int nvert = pline.NumberOfVertices;
            for (int i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                Point3d pi = pline.GetPoint3dAt(i);
                Point3d pj = pline.GetPoint3dAt(j);
                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    intersections++;
                }
            }
            return (intersections % 2) == 1;
        }
    }
}