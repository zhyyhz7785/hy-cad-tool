using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Shared.AutoCAD.Interaction
{
    /// <summary>
    /// Features 层统一的 CAD 交互：取世界坐标点、锁定图层跳过擦除、临时改当前图层。
    /// </summary>
    public interface ICadInteraction
    {
        PromptStatus GetWcsPoint(string prompt, out Point3d wcs);
        PromptStatus GetWcsPoint(string prompt, Point3d basePointWcs, out Point3d wcs);
        PromptStatus GetWcsEntity(PromptEntityOptions options, out PromptEntityResult result, out Point3d wcsPicked);
        bool SafeErase(Transaction transaction, Entity entity);
        IDisposable WithTemporaryLayer(Database db, string layerName);
    }
}
