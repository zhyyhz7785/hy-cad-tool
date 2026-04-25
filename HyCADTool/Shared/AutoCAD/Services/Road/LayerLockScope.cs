using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 在同一事务中临时解锁图层，<see cref="IDisposable.Dispose"/> 时恢复原锁定状态。
    ///
    /// <para>典型用法：</para>
    /// <code>
    /// using (doc.LockDocument())
    /// using (var tr = db.TransactionManager.StartTransaction())
    /// {
    ///     using (LayerLockScope.Unlock(tr, db, "05_hy_道路_原线"))
    ///     {
    ///         // 向锁定层写入 / 擦除实体
    ///     }
    ///     tr.Commit();
    /// }
    /// </code>
    ///
    /// <para>设计要点：</para>
    /// <list type="bullet">
    ///   <item>构造阶段：读 <see cref="LayerTableRecord.IsLocked"/>，若已锁则 <c>UpgradeOpen</c> 解锁；</item>
    ///   <item>Dispose 阶段：若原本是锁定的，恢复为锁定；<b>异常全部吞掉</b>（避免 finally 抛出导致 AutoCAD 原生崩）；</item>
    ///   <item>若图层不存在，<see cref="Unlock"/> 返回一个 no-op scope，调用方无需额外判空；</item>
    ///   <item>同一图层嵌套调用安全（内层读到的"原锁态"是外层改出来的 false，Dispose 时不会再锁）。</item>
    /// </list>
    /// </summary>
    public sealed class LayerLockScope : IDisposable
    {
        private readonly Transaction _tr;
        private readonly ObjectId _layerId;
        private readonly bool _originalLocked;
        private bool _disposed;

        private LayerLockScope(Transaction tr, ObjectId layerId, bool originalLocked)
        {
            _tr = tr;
            _layerId = layerId;
            _originalLocked = originalLocked;
        }

        /// <summary>
        /// 临时解锁 <paramref name="layerName"/>；图层不存在时返回 no-op scope。
        /// </summary>
        public static LayerLockScope Unlock(Transaction tr, Database db, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return new LayerLockScope(null, ObjectId.Null, false);
            }

            try
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    return new LayerLockScope(null, ObjectId.Null, false);
                }

                var layerId = lt[layerName];
                var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                bool original = layer.IsLocked;
                if (original)
                {
                    layer.UpgradeOpen();
                    layer.IsLocked = false;
                }
                return new LayerLockScope(tr, layerId, original);
            }
            catch
            {
                return new LayerLockScope(null, ObjectId.Null, false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_tr == null || _layerId.IsNull || !_originalLocked) return;

            try
            {
                var layer = (LayerTableRecord)_tr.GetObject(_layerId, OpenMode.ForRead);
                if (!layer.IsLocked)
                {
                    layer.UpgradeOpen();
                    layer.IsLocked = true;
                }
            }
            catch
            {
                // 恢复锁定失败不升级为致命异常：AutoCAD shutdown 路径上 finally 抛会原生崩。
            }
        }
    }
}
