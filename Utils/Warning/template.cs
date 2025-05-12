using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace CadUtils
{
    public static partial class EtGpt
    {
        public static void AddWarningEntity(BlockTableRecord btr, Transaction tr, Polyline polyline, string layerName, string message)
        {
            Point3d centroid = GeometryUtils.GetPolylineCentroid(polyline); // 计算质心
            var dbText = new DBText
            {
                Position = centroid,
                TextString = message,
                Layer = layerName,
                Height = 2.0 * BaseConfig.Scale
            };
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
        }
    }
}
