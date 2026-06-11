using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Entities
{
    /// <summary>
    /// Resolves effective display appearance for entities, including ByLayer fallbacks.
    /// This is the single entry point used by selection filters that compare visible values.
    /// </summary>
    public static class EntityAppearanceResolver
    {
        private const int OpaqueAlpha = 255;

        public static Color GetTrueColor(Entity entity)
        {
            if (entity == null) return Color.FromColorIndex(ColorMethod.ByAci, 7);

            using (var tr = entity.Database.TransactionManager.StartTransaction())
            {
                var color = GetTrueColor(entity, tr);
                tr.Commit();
                return color;
            }
        }

        public static int GetTrueLineWeight(Entity entity)
        {
            if (entity == null) return (int)LineWeight.ByLayer;

            using (var tr = entity.Database.TransactionManager.StartTransaction())
            {
                var lineWeight = GetTrueLineWeight(entity, tr);
                tr.Commit();
                return lineWeight;
            }
        }

        public static ObjectId GetTrueLinetype(Entity entity)
        {
            if (entity == null) return ObjectId.Null;

            using (var tr = entity.Database.TransactionManager.StartTransaction())
            {
                var linetypeId = GetTrueLinetype(entity, tr);
                tr.Commit();
                return linetypeId;
            }
        }

        public static int GetTrueTransparency(Entity entity)
        {
            if (entity == null) return OpaqueAlpha;

            using (var tr = entity.Database.TransactionManager.StartTransaction())
            {
                var transparency = GetTrueTransparency(entity, tr);
                tr.Commit();
                return transparency;
            }
        }

        public static ObjectId[] FilterByColor(Document document, Color targetColor, ObjectId[] ids)
        {
            return FilterEntities(document, ids, (entity, tr) => ColorsEqual(GetTrueColor(entity, tr), targetColor));
        }

        public static ObjectId[] FilterByLineWeight(Document document, int targetLineWeight, ObjectId[] ids)
        {
            return FilterEntities(document, ids, (entity, tr) => GetTrueLineWeight(entity, tr) == targetLineWeight);
        }

        public static ObjectId[] FilterByLinetype(Document document, ObjectId targetLinetypeId, ObjectId[] ids)
        {
            return FilterEntities(document, ids, (entity, tr) => GetTrueLinetype(entity, tr) == targetLinetypeId);
        }

        public static ObjectId[] FilterByTransparency(Document document, int targetAlpha, ObjectId[] ids)
        {
            return FilterEntities(document, ids, (entity, tr) => GetTrueTransparency(entity, tr) == targetAlpha);
        }

        private static ObjectId[] FilterEntities(Document document, ObjectId[] ids, Func<Entity, Transaction, bool> predicate)
        {
            if (document == null || predicate == null) return new ObjectId[0];

            var result = new List<ObjectId>();
            var db = document.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                if (ids == null || ids.Length == 0)
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    var allIds = new List<ObjectId>();
                    foreach (ObjectId id in ms)
                    {
                        allIds.Add(id);
                    }
                    ids = allIds.ToArray();
                }

                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity entity))
                        continue;

                    try
                    {
                        if (predicate(entity, tr))
                            result.Add(id);
                    }
                    catch
                    {
                        // Ignore entities whose appearance cannot be read in the current drawing.
                    }
                }

                tr.Commit();
            }

            return result.ToArray();
        }

        public static Color GetTrueColor(Entity entity, Transaction transaction)
        {
            var color = entity.Color;
            if (color.IsByLayer || color.IsByBlock)
            {
                return GetLayer(entity, transaction)?.Color ?? color;
            }

            return color;
        }

        public static int GetTrueLineWeight(Entity entity, Transaction transaction)
        {
            var lineWeight = entity.LineWeight;
            if (lineWeight == LineWeight.ByLayer || lineWeight == LineWeight.ByBlock)
            {
                var layer = GetLayer(entity, transaction);
                return layer != null ? (int)layer.LineWeight : (int)lineWeight;
            }

            return (int)lineWeight;
        }

        public static ObjectId GetTrueLinetype(Entity entity, Transaction transaction)
        {
            if (IsByLayerOrByBlock(entity.Linetype))
            {
                var layer = GetLayer(entity, transaction);
                return layer?.LinetypeObjectId ?? entity.LinetypeId;
            }

            return entity.LinetypeId;
        }

        public static int GetTrueTransparency(Entity entity, Transaction transaction)
        {
            var transparency = entity.Transparency;
            if (transparency.IsByAlpha)
                return transparency.Alpha;

            if (transparency.IsByLayer || transparency.IsByBlock)
            {
                var layerTransparency = GetLayer(entity, transaction)?.Transparency;
                if (layerTransparency.HasValue && layerTransparency.Value.IsByAlpha)
                    return layerTransparency.Value.Alpha;
            }

            return OpaqueAlpha;
        }

        private static LayerTableRecord GetLayer(Entity entity, Transaction transaction)
        {
            if (entity == null || entity.LayerId.IsNull)
                return null;

            var tr = transaction ?? entity.Database.TransactionManager.TopTransaction;
            if (tr == null)
                return null;

            return tr.GetObject(entity.LayerId, OpenMode.ForRead, false) as LayerTableRecord;
        }

        private static bool IsByLayerOrByBlock(string value)
        {
            return string.Equals(value, "ByLayer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "ByBlock", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ColorsEqual(Color actual, Color target)
        {
            if (actual == null || target == null) return false;

            if (actual.ColorMethod == ColorMethod.ByAci && target.ColorMethod == ColorMethod.ByAci)
                return actual.ColorIndex == target.ColorIndex;

            if (actual.ColorMethod != target.ColorMethod)
                return false;

            return actual.ColorValue.ToArgb() == target.ColorValue.ToArgb();
        }
    }
}
