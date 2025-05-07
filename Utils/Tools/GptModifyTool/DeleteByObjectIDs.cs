using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
namespace CadUtils
{
    public static partial class EtGpt
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
