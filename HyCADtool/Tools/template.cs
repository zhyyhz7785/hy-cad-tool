using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;

namespace HyCADTool.Tools
{
    public static class TransactionBatchProcessor
    {
        /// <summary>
        /// 针对大量 ObjectId 列表，逐个在短事务中处理，避免eAtMaxReaders。
        /// </summary>
        public static void ProcessEach<T>(IEnumerable<ObjectId> objectIds, Func<T, bool> action) where T : DBObject
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc?.Database;
            if (db == null || objectIds == null) return;

            foreach (var id in objectIds)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead, false) as T;
                    if (obj != null)
                    {
                        bool success = action(obj);
                        if (success)
                        {
                            tr.Commit();
                        }
                        else
                        {
                            tr.Abort();
                        }
                    }
                    else
                    {
                        tr.Abort();
                    }
                }
            }
        }
    }
}
