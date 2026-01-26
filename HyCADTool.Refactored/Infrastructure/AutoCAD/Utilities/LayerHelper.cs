using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities
{
    /// <summary>
    /// 图层辅助工具：获取/创建图层，设置线型与线宽
    /// 注意：不自动提交事务，由调用方控制事务边界
    /// </summary>
    public static class LayerHelper
    {
        public class LayerStyle
        {
            public string LayerName { get; set; }
            public string LinetypeName { get; set; }
            public LineWeight? LineWeight { get; set; }
        }

        public static List<string> GetAllLayerNames(Database db)
        {
            var names = new List<string>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    names.Add(rec.Name);
                }
                tr.Commit();
            }
            return names;
        }

        public static ObjectId EnsureLayer(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    var rec = new LayerTableRecord { Name = layerName };
                    var id = lt.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                    tr.Commit();
                    return id;
                }
                tr.Commit();
            }
            using (var tr2 = db.TransactionManager.StartTransaction())
            {
                var lt2 = (LayerTable)tr2.GetObject(db.LayerTableId, OpenMode.ForRead);
                tr2.Commit();
                return lt2[layerName];
            }
        }

        public static void SetLayerLinetype(Database db, string layerName, string linetypeName)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    tr.Abort();
                    return;
                }

                var rec = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (!ltt.Has(linetypeName))
                {
                    tr.Abort();
                    return;
                }
                rec.LinetypeObjectId = ltt[linetypeName];
                tr.Commit();
            }
        }

        public static void SetLayerLineWeight(Database db, string layerName, LineWeight lineWeight)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    tr.Abort();
                    return;
                }
                var rec = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                rec.LineWeight = lineWeight;
                tr.Commit();
            }
        }

        /// <summary>
        /// 预览批量样式：返回（层名 -> 可用与否、线型是否存在）的信息，不写库
        /// </summary>
        public static Dictionary<string, (bool layerExists, bool linetypeExists)> PreviewLayerStyles(Database db, IEnumerable<LayerStyle> styles)
        {
            var result = new Dictionary<string, (bool, bool)>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                foreach (var s in styles)
                {
                    if (s == null || string.IsNullOrEmpty(s.LayerName)) continue;
                    bool layerExists = lt.Has(s.LayerName);
                    bool ltypeExists = string.IsNullOrEmpty(s.LinetypeName) || ltt.Has(s.LinetypeName);
                    result[s.LayerName] = (layerExists, ltypeExists);
                }
                tr.Commit();
            }
            return result;
        }

        /// <summary>
        /// 应用批量样式：逐条设置线型、线宽；不存在的图层将被创建
        /// </summary>
        public static void ApplyLayerStyles(Database db, IEnumerable<LayerStyle> styles)
        {
            if (styles == null) return;
            foreach (var s in styles)
            {
                if (s == null || string.IsNullOrEmpty(s.LayerName)) continue;
                var id = EnsureLayer(db, s.LayerName);
                if (!string.IsNullOrEmpty(s.LinetypeName))
                {
                    SetLayerLinetype(db, s.LayerName, s.LinetypeName);
                }
                if (s.LineWeight.HasValue)
                {
                    SetLayerLineWeight(db, s.LayerName, s.LineWeight.Value);
                }
            }
        }
    }
}


