using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Interaction
{
    /// <summary>
    /// 按 Database 找到所属文档再加锁，避免锁着活动图却写另一张图。
    /// </summary>
    public static class CadTx
    {
        public static void Write(Database db, Action<Transaction> body)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var doc = TryGetDocument(db);
            if (doc != null)
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    body(tr);
                    tr.Commit();
                }
                return;
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                body(tr);
                tr.Commit();
            }
        }

        public static T Read<T>(Database db, Func<Transaction, T> body)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var result = body(tr);
                tr.Commit();
                return result;
            }
        }

        public static Document TryGetDocument(Database db)
        {
            if (db == null)
                return null;
            try
            {
                var manager = AcApp.DocumentManager;
                if (manager == null)
                    return null;

                var fromApi = manager.GetDocument(db);
                if (fromApi != null)
                    return fromApi;

                var active = manager.MdiActiveDocument;
                if (active != null && ReferenceEquals(active.Database, db))
                    return active;
            }
            catch
            {
            }

            return null;
        }
    }
}
