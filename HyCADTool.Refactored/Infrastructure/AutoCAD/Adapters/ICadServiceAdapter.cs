using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Interfaces
{
    /// <summary>
    /// CAD 服务接口（适配原项目）
    /// 用于 PilePanel 和 ClusterPanel 的兼容性
    /// </summary>
    public interface ICadService
    {
        void WriteMessage(string message);
        Polyline SelectPolyline();
        void DrawEntities(IEnumerable<Entity> entities, string layerName);
        void CreateTable(Point3d insertionPoint, string csvFilePath, double scale);
    }
}

