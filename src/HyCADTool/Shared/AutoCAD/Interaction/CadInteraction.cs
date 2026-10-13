using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Utilities;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Interaction
{
    /// <summary>
    /// <see cref="ICadInteraction"/> 的 AutoCAD 实现：内部走 EditorWcs 与 LockedLayerErase。
    /// </summary>
    public sealed class CadInteraction : ICadInteraction
    {
        public PromptStatus GetWcsPoint(string prompt, out Point3d wcs)
        {
            wcs = default(Point3d);
            var ed = ActiveEditor();
            if (ed == null)
                return PromptStatus.Error;

            var result = ed.GetPoint(prompt);
            if (result.Status != PromptStatus.OK)
                return result.Status;

            wcs = EditorWcs.ToWcs(ed, result.Value);
            return PromptStatus.OK;
        }

        public PromptStatus GetWcsPoint(string prompt, Point3d basePointWcs, out Point3d wcs)
        {
            wcs = default(Point3d);
            var ed = ActiveEditor();
            if (ed == null)
                return PromptStatus.Error;

            var options = new PromptPointOptions(prompt)
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = EditorWcs.ToUcs(ed, basePointWcs)
            };

            var result = ed.GetPoint(options);
            if (result.Status != PromptStatus.OK)
                return result.Status;

            wcs = EditorWcs.ToWcs(ed, result.Value);
            return PromptStatus.OK;
        }

        public PromptStatus GetWcsEntity(PromptEntityOptions options, out PromptEntityResult result, out Point3d wcsPicked)
        {
            result = null;
            wcsPicked = default(Point3d);
            var ed = ActiveEditor();
            if (ed == null)
                return PromptStatus.Error;

            result = ed.GetEntity(options);
            if (result.Status != PromptStatus.OK)
                return result.Status;

            wcsPicked = EditorWcs.ToWcs(ed, result.PickedPoint);
            return PromptStatus.OK;
        }

        public bool SafeErase(Transaction transaction, Entity entity)
        {
            return LockedLayerErase.TryErase(transaction, entity, EditorFor(entity));
        }

        public bool TryOpenForWrite(Transaction transaction, ObjectId id, out Entity entity)
        {
            Editor editor = null;
            try
            {
                var db = id.Database;
                var doc = db == null ? null : CadTx.TryGetDocument(db);
                editor = doc?.Editor ?? ActiveEditor();
            }
            catch
            {
                editor = ActiveEditor();
            }

            return LockedLayerErase.TryOpenForWrite(transaction, id, out entity, editor);
        }

        private static Editor EditorFor(Entity entity)
        {
            try
            {
                var db = entity?.Database;
                var doc = db == null ? null : CadTx.TryGetDocument(db);
                return doc?.Editor ?? ActiveEditor();
            }
            catch
            {
                return ActiveEditor();
            }
        }

        public IDisposable WithTemporaryLayer(Database db, string layerName)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Layer name cannot be null or empty", nameof(layerName));

            var previous = db.Clayer;
            WithLayerWriteLock(db, tr =>
            {
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!layerTable.Has(layerName))
                    throw new ArgumentException("Layer '" + layerName + "' does not exist", nameof(layerName));
                var layerId = layerTable[layerName];
                var record = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                if (record.IsFrozen)
                    throw new InvalidOperationException("Layer '" + layerName + "' is frozen.");
                db.Clayer = layerId;
            });

            return new RestoreCurrentLayer(db, previous);
        }

        private static void WithLayerWriteLock(Database db, Action<Transaction> body)
        {
            var doc = CadTx.TryGetDocument(db);
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

        private static Editor ActiveEditor()
        {
            return AcApp.DocumentManager.MdiActiveDocument?.Editor;
        }

        private sealed class RestoreCurrentLayer : IDisposable
        {
            private Database _db;
            private readonly ObjectId _previous;

            public RestoreCurrentLayer(Database db, ObjectId previous)
            {
                _db = db;
                _previous = previous;
            }

            public void Dispose()
            {
                var db = _db;
                _db = null;
                if (db == null)
                    return;
                try
                {
                    WithLayerWriteLock(db, tr =>
                    {
                        if (_previous.IsNull || _previous.IsErased)
                            throw new InvalidOperationException("previous CLAYER is no longer valid.");
                        db.Clayer = _previous;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("[HyCAD] 恢复 CLAYER 失败: " + ex.Message);
                }
            }
        }
    }
}
