using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    /// <summary>
    /// 擦除或升级写打开之前检查图层锁定。锁定时跳过并在命令行提示，避免 eOnLockedLayer 中断事务。
    /// </summary>
    public static class LockedLayerErase
    {
        public static bool IsLocked(Transaction transaction, Entity entity)
        {
            if (transaction == null || entity == null)
                return false;

            var layer = transaction.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
            return layer != null && layer.IsLocked;
        }

        public static bool TryErase(Transaction transaction, Entity entity, Editor editor)
        {
            if (entity == null || entity.IsErased)
                return false;

            if (IsLocked(transaction, entity))
            {
                Warn(editor, entity.Layer, "擦除");
                return false;
            }

            if (!entity.IsWriteEnabled)
                entity.UpgradeOpen();

            entity.Erase();
            return true;
        }

        /// <summary>
        /// 只读打开后检查锁定，再升级为写。锁定、已删或类型不对时返回 false，entity 为 null。
        /// </summary>
        public static bool TryOpenForWrite(Transaction transaction, ObjectId id, out Entity entity, Editor editor)
        {
            entity = null;
            if (transaction == null || id.IsNull)
                return false;

            var obj = transaction.GetObject(id, OpenMode.ForRead, false);
            var opened = obj as Entity;
            if (opened == null || opened.IsErased)
                return false;

            if (IsLocked(transaction, opened))
            {
                Warn(editor, opened.Layer, "写入");
                return false;
            }

            if (!opened.IsWriteEnabled)
                opened.UpgradeOpen();

            entity = opened;
            return true;
        }

        public static bool TryOpenForWrite<T>(Transaction transaction, ObjectId id, out T entity, Editor editor)
            where T : Entity
        {
            entity = null;
            Entity raw;
            if (!TryOpenForWrite(transaction, id, out raw, editor))
                return false;
            entity = raw as T;
            return entity != null;
        }

        private static void Warn(Editor editor, string layer, string action)
        {
            try
            {
                editor?.WriteMessage("\n图层 " + layer + " 已锁定，跳过" + action + "。");
            }
            catch
            {
            }
        }
    }
}
