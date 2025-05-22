using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Colors;
using System;

namespace HyCADTool.Tools
{
    public static partial class Et
    {       
        public static ObjectId CreateLayer(string layerName, short colorIndex = 7, Database db = null, Editor ed = null)
        {
            if (db == null)
                db = Application.DocumentManager.MdiActiveDocument.Database;

            if (ed == null)
                ed = Application.DocumentManager.MdiActiveDocument.Editor;

            using (DocumentLock docLock = Application.DocumentManager.MdiActiveDocument.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                if (lt.Has(layerName))
                    return lt[layerName];

                lt.UpgradeOpen();

                var newLayer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };

                ObjectId id = lt.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
                tr.Commit();

                ed.WriteMessage($"\n图层 '{layerName}' 已创建。");
                return id;
            }
        }

        public static ObjectId CreateLayer(string layerName, short colorIndex, string lineType, LineWeight lineWeight)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                        LinetypeObjectId = Et.GetOrCreateLinetypeId(db, lineType, tr),
                        LineWeight = lineWeight
                    };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                tr.Commit();
                return lt[layerName];
            }
        }


        /// <summary>
        /// 创建多个图层。每项包含名称与颜色索引。
        /// </summary>

        public static void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                foreach (var item in layerInfos)
                {
                    string layerName = item.layerName;
                    short colorIndex = item.colorIndex;

                    if (lt.Has(layerName))
                    {
                        doc.Editor.WriteMessage($"\n图层 '{layerName}' 已存在。");
                        continue;
                    }

                    lt.UpgradeOpen();

                    var newLayer = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                    };

                    lt.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                    doc.Editor.WriteMessage($"\n图层 '{layerName}' 已创建。");
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 将实体设置到指定图层，若图层不存在则创建。
        /// </summary>
        public static void SetLayer(this Entity entity, string newLayerName)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrWhiteSpace(newLayerName))
                throw new ArgumentException("图层名不能为空", nameof(newLayerName));

            Database db = entity.Database;
            if (db == null)
                db = Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                ObjectId layerId;

                if (lt.Has(newLayerName))
                {
                    layerId = lt[newLayerName];
                }
                else
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord { Name = newLayerName };
                    layerId = lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }

                if (!entity.IsWriteEnabled)
                    entity.UpgradeOpen();

                entity.LayerId = layerId;
                tr.Commit();
            }
        }

        /// <summary>
        /// 获取指定图层的 ObjectId，如果不存在则抛出异常。
        /// </summary>
        public static ObjectId GetLayerId(this string layerName, Database db = null)
        {
            if (db == null)
                db = Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                if (lt.Has(layerName))
                    return lt[layerName];

                throw new System.Exception($"图层 '{layerName}' 不存在。");
            }
        }
    }
}
