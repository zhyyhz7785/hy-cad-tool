using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static bool IsPointInsidePolyline(Polyline polyline, Point3d point)
        {
            int crossings = 0;
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point3d p1 = polyline.GetPoint3dAt(i);
                Point3d p2 = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);
                if (((p1.Y <= point.Y && point.Y < p2.Y) || (p2.Y <= point.Y && point.Y < p1.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    crossings++;
                }
            }
            return (crossings % 2 != 0);
        }
    }
}
