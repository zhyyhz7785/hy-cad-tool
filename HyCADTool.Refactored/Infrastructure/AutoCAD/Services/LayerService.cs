using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using HyCADTool.Refactored.Domain.Interfaces;
using System;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 图层服务实现
    /// </summary>
    public class LayerService : ILayerService
    {
        public void CreateLayer(string layerName, short colorIndex)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Layer name cannot be null or empty", nameof(layerName));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (!layerTable.Has(layerName))
                    {
                        layerTable.UpgradeOpen();

                        var layerTableRecord = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                        };

                        layerTable.Add(layerTableRecord);
                        tr.AddNewlyCreatedDBObject(layerTableRecord, true);

                        doc.Editor.WriteMessage($"\n✓ 已创建图层: {layerName} (颜色: {colorIndex})");
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建图层失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void SetCurrentLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Layer name cannot be null or empty", nameof(layerName));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (layerTable.Has(layerName))
                    {
                        db.Clayer = layerTable[layerName];
                        tr.Commit();
                    }
                    else
                    {
                        throw new ArgumentException($"Layer '{layerName}' does not exist");
                    }
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public bool LayerExists(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return false;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    bool exists = layerTable.Has(layerName);
                    tr.Commit();
                    return exists;
                }
                catch
                {
                    tr.Abort();
                    return false;
                }
            }
        }

        public bool DeleteLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return false;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                    if (layerTable.Has(layerName))
                    {
                        var layerId = layerTable[layerName];
                        var layerTableRecord = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);

                        // 检查图层是否可以删除
                        if (!layerTableRecord.IsErased && layerTableRecord.Name != "0")
                        {
                            layerTableRecord.Erase();
                            tr.Commit();
                            return true;
                        }
                    }

                    tr.Abort();
                    return false;
                }
                catch
                {
                    tr.Abort();
                    return false;
                }
            }
        }
    }
}

