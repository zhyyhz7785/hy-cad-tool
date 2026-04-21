using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 图层管理器实现
    /// 负责 AutoCAD 图层的创建和配置
    /// </summary>
    public class LayerManager : ILayerManager
    {
        private readonly Database _database;

        public LayerManager()
        {
            _database = Application.DocumentManager.MdiActiveDocument?.Database;
        }

        public LayerManager(Database database)
        {
            _database = database;
        }

        /// <summary>
        /// 确保图层存在，如果不存在则创建
        /// </summary>
        public void EnsureLayer(Transaction tr, string layerName, short colorIndex)
        {
            EnsureLayerCore(tr, layerName, colorIndex, applyLock: false, locked: false);
        }

        /// <summary>
        /// 确保图层存在（可指定锁定状态）。
        /// </summary>
        public void EnsureLayer(Transaction tr, string layerName, short colorIndex, bool locked)
        {
            EnsureLayerCore(tr, layerName, colorIndex, applyLock: true, locked: locked);
        }

        /// <summary>
        /// 批量创建图层
        /// </summary>
        public void EnsureLayers(Transaction tr, params (string name, short colorIndex)[] layers)
        {
            foreach (var (name, colorIndex) in layers)
            {
                EnsureLayer(tr, name, colorIndex);
            }
        }

        /// <summary>
        /// 临时解锁图层执行 body，finally 恢复原锁态；若图层不存在则按 locked=true 创建。
        /// </summary>
        public void WithUnlocked(Transaction tr, string layerName, short colorIndex, Action body)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (string.IsNullOrWhiteSpace(layerName)) throw new ArgumentException("layerName", nameof(layerName));
            if (body == null) throw new ArgumentNullException(nameof(body));

            var database = ResolveDatabase();
            var layerTable = (LayerTable)tr.GetObject(database.LayerTableId, OpenMode.ForRead);

            LayerTableRecord layer;
            bool originalLocked;
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                layer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                    IsLocked = true,
                };
                layerTable.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
                layerTable.DowngradeOpen();
                originalLocked = true;
            }
            else
            {
                layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForRead);
                originalLocked = layer.IsLocked;
            }

            if (originalLocked)
            {
                if (!layer.IsWriteEnabled) layer.UpgradeOpen();
                layer.IsLocked = false;
            }

            try
            {
                body();
            }
            finally
            {
                try
                {
                    if (originalLocked)
                    {
                        if (!layer.IsWriteEnabled) layer.UpgradeOpen();
                        layer.IsLocked = true;
                    }
                }
                catch
                {
                    // 恢复锁定失败不升级为致命异常：AutoCAD shutdown 路径上 finally 抛会原生崩。
                }
            }
        }

        private void EnsureLayerCore(Transaction tr, string layerName, short colorIndex, bool applyLock, bool locked)
        {
            var database = ResolveDatabase();
            var layerTable = (LayerTable)tr.GetObject(database.LayerTableId, OpenMode.ForRead);

            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                var layer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                };
                if (applyLock) layer.IsLocked = locked;
                layerTable.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
                layerTable.DowngradeOpen();
            }
            else
            {
                var layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                if (applyLock && layer.IsLocked != locked)
                {
                    layer.IsLocked = locked;
                }
            }
        }

        private Database ResolveDatabase()
        {
            var currentDatabase = Application.DocumentManager.MdiActiveDocument?.Database;
            return currentDatabase ?? _database ?? throw new System.InvalidOperationException("无法获取当前文档数据库。");
        }
    }
}
