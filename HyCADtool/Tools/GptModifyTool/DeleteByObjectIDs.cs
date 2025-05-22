using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        public static void DeleteByObjectIDs(this List<ObjectId> toBeErased, Document doc, Database db)
        {
            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var polylineId in toBeErased)
                    {
                        var polyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                        if (polyline != null)
                        {
                            polyline.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
        }
    }
}
