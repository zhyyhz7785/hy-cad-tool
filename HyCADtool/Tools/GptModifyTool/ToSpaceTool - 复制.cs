using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        public static ObjectId ToSpace(this Entity ent, Database db = null, string space = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            ObjectId id = ObjectId.Null;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blkTbl = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var spaceRecord = (BlockTableRecord)trans.GetObject(blkTbl[space ?? BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    if (ent.Id.IsNull)
                    {
                        id = spaceRecord.AppendEntity(ent);
                        trans.AddNewlyCreatedDBObject(ent, true);
                    }
                    else
                    {
                        id = ent.Id;
                    }
                    trans.Commit();
                }
            }
            return id;
        }
        public static ObjectIdCollection ToSpace(this IEnumerable<Entity> ents, Database db = null, string space = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var ids = new ObjectIdCollection();
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blkTbl = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var spaceRecord = (BlockTableRecord)trans.GetObject(blkTbl[space ?? BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    foreach (var ent in ents)
                    {
                        if (ent.Id.IsNull)
                        {
                            ids.Add(spaceRecord.AppendEntity(ent));
                            trans.AddNewlyCreatedDBObject(ent, true);
                        }
                        else
                        {
                            ids.Add(ent.Id);
                        }
                    }
                    trans.Commit();
                }
            }
            return ids;
        }
        public static ObjectIdCollection ToSpace(this IEnumerable<Point3d> points, int pdMode = 3, double pdSize = -5, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var ids = new ObjectIdCollection();
            Application.SetSystemVariable("PDMODE", pdMode);
            Application.SetSystemVariable("PDSIZE", pdSize);
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blkTbl = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    foreach (var point in points)
                    {
                        DBPoint dbPoint = new DBPoint(point);
                        dbPoint.SetDatabaseDefaults();
                        ids.Add(btr.AppendEntity(dbPoint));
                        trans.AddNewlyCreatedDBObject(dbPoint, true);
                    }
                    trans.Commit();
                }
            }
            return ids;
        }
        public static ObjectIdCollection ToSpace(this IEnumerable<Point3d> points, string layerName, short colorIndex = 4, int pdMode = 3, double pdSize = -5, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var ids = new ObjectIdCollection();
            Application.SetSystemVariable("PDMODE", pdMode);
            Application.SetSystemVariable("PDSIZE", pdSize);
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blkTbl = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    LayerTable lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                        };
                        lt.Add(ltr);
                        trans.AddNewlyCreatedDBObject(ltr, true);
                    }
                    foreach (var point in points)
                    {
                        DBPoint dbPoint = new DBPoint(point)
                        {
                            Layer = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                        };
                        dbPoint.SetDatabaseDefaults();
                        ids.Add(btr.AppendEntity(dbPoint));
                        trans.AddNewlyCreatedDBObject(dbPoint, true);
                    }
                    trans.Commit();
                }
            }
            return ids;
        }
        public static ObjectId ToSpace(this Point3d point, string layerName = "Hy_Points", short colorIndex = 4, int pdMode = 3, double pdSize = -5, Database db = null)
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            ObjectId pointId = ObjectId.Null;
            Application.SetSystemVariable("PDMODE", pdMode);
            Application.SetSystemVariable("PDSIZE", pdSize);
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blkTbl = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    LayerTable lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                        };
                        lt.Add(ltr);
                        trans.AddNewlyCreatedDBObject(ltr, true);
                    }
                    DBPoint dbPoint = new DBPoint(point)
                    {
                        Layer = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                    };
                    dbPoint.SetDatabaseDefaults();
                    pointId = btr.AppendEntity(dbPoint);
                    trans.AddNewlyCreatedDBObject(dbPoint, true);
                    trans.Commit();
                }
            }
            return pointId;
        }
    }
}
