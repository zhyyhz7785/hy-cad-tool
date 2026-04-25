using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using HyCADTool.Domain.Interfaces;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services
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

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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

        // === ZTools迁移方法 ===

        public string CreateLayerWithStyle(string layerName, short colorIndex, string lineType, int lineWeight)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
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

                        var layerRecord = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                            LineWeight = (LineWeight)lineWeight
                        };

                        // 设置线型（如果指定）
                        if (!string.IsNullOrEmpty(lineType) && lineType != "Continuous")
                        {
                            layerRecord.LinetypeObjectId = GetOrCreateLinetype(db, lineType, tr);
                        }

                        layerTable.Add(layerRecord);
                        tr.AddNewlyCreatedDBObject(layerRecord, true);
                        
                        doc.Editor.WriteMessage($"\n✓ 已创建带样式的图层: {layerName}");
                    }

                    tr.Commit();
                    return layerTable[layerName].ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建带样式图层失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos)
        {
            if (layerInfos == null || layerInfos.Length == 0)
                return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    bool modified = false;

                    foreach (var (layerName, colorIndex) in layerInfos)
                    {
                        if (layerTable.Has(layerName))
                            continue; // 静默跳过已存在的图层

                        if (!modified)
                        {
                            layerTable.UpgradeOpen();
                            modified = true;
                        }

                        var layerRecord = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                        };

                        layerTable.Add(layerRecord);
                        tr.AddNewlyCreatedDBObject(layerRecord, true);
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 批量创建图层失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public string GetLayerId(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Layer name cannot be null or empty", nameof(layerName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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
                        var layerId = layerTable[layerName];
                        tr.Commit();
                        return layerId.ToString();
                    }

                    throw new System.Exception($"图层 '{layerName}' 不存在");
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public List<string> GetAllLayerNames()
        {
            var layerNames = new List<string>();
            
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return layerNames;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    
                    foreach (ObjectId layerId in layerTable)
                    {
                        var layerRecord = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                        if (!layerRecord.IsErased)
                        {
                            layerNames.Add(layerRecord.Name);
                        }
                    }

                    tr.Commit();
                    return layerNames;
                }
                catch
                {
                    tr.Abort();
                    return layerNames;
                }
            }
        }

        public void SetEntityLayer(string entityId, string layerName)
        {
            if (string.IsNullOrWhiteSpace(entityId))
                throw new ArgumentException("Entity ID cannot be null or empty", nameof(entityId));
            
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Layer name cannot be null or empty", nameof(layerName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;
            
            // 解析ObjectId
            ObjectId objId;
            try
            {
                objId = new ObjectId(new IntPtr(long.Parse(entityId)));
                if (objId.IsNull)
                    throw new ArgumentException("Invalid entity ID", nameof(entityId));
            }
            catch
            {
                throw new ArgumentException("Invalid entity ID format", nameof(entityId));
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    ObjectId layerId;

                    // 确保图层存在
                    if (layerTable.Has(layerName))
                    {
                        layerId = layerTable[layerName];
                    }
                    else
                    {
                        // 创建图层
                        layerTable.UpgradeOpen();
                        var layerRecord = new LayerTableRecord { Name = layerName };
                        layerId = layerTable.Add(layerRecord);
                        tr.AddNewlyCreatedDBObject(layerRecord, true);
                    }

                    // 设置实体图层
                    var dbObject = tr.GetObject(objId, OpenMode.ForWrite);
                    if (dbObject is Entity entity)
                    {
                        entity.LayerId = layerId;
                    }
                    else
                    {
                        throw new ArgumentException("Selected object is not an entity", nameof(entityId));
                    }
                    
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        /// <summary>
        /// 获取或创建线型
        /// </summary>
        private ObjectId GetOrCreateLinetype(Database db, string linetypeName, Transaction tr)
        {
            var linetypeTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            
            if (linetypeTable.Has(linetypeName))
            {
                return linetypeTable[linetypeName];
            }

            // 如果线型不存在，返回连续线型
            return linetypeTable["Continuous"];
        }

        /// <inheritdoc />
        public void EnsureUserLayerItems(IReadOnlyList<LayerDefinitionItem> items)
        {
            if (items == null || items.Count == 0) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("No active document");
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                var continuousId = ltTable["Continuous"];

                foreach (var it in items)
                {
                    if (it == null || string.IsNullOrWhiteSpace(it.Name)) continue;
                    var layerName = it.Name.Trim();
                    LayerTableRecord rec;
                    if (layerTable.Has(layerName))
                    {
                        rec = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
                    }
                    else
                    {
                        layerTable.UpgradeOpen();
                        rec = new LayerTableRecord { Name = layerName };
                        layerTable.Add(rec);
                        tr.AddNewlyCreatedDBObject(rec, true);
                    }

                    rec.Color = Color.FromColorIndex(ColorMethod.ByAci, it.AciColor);
                    var lt = string.IsNullOrWhiteSpace(it.LinetypeName) ? "Continuous" : it.LinetypeName.Trim();
                    if (ltTable.Has(lt))
                        rec.LinetypeObjectId = ltTable[lt];
                    else
                        rec.LinetypeObjectId = continuousId;

                    if (it.LineWeightRaw < 0)
                        rec.LineWeight = LineWeight.ByLayer;
                    else
                    {
                        var w = it.LineWeightRaw;
                        if (w >= 0 && w < 32)
                        {
                            try { rec.LineWeight = (LineWeight)w; }
                            catch { rec.LineWeight = LineWeight.ByLayer; }
                        }
                        else
                            rec.LineWeight = LineWeight.ByLayer;
                    }

                    try
                    {
                        // AutoCAD 2010+ 常见属性
                        var pl = rec.GetType().GetProperty("Plottable");
                        pl?.SetValue(rec, it.IsPlottable);
                    }
                    catch { }
                }

                tr.Commit();
            }
        }
    }
}

