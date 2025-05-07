// HyCADTool/Interfaces/ICadService.cs
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace HyCADTool.Interfaces
{
    public interface ICadService
    {
        void WriteMessage(string message);
        Polyline SelectPolyline();
        void DrawEntities(IEnumerable<Entity> entities, string layerName);
        void CreateTable(Point3d insertionPoint, string csvFilePath, double scale);
    }
}