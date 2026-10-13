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
                try
                {
                    editor?.WriteMessage("\n图层 " + entity.Layer + " 已锁定，跳过擦除。");
                }
                catch
                {
                }

                return false;
            }

            if (!entity.IsWriteEnabled)
                entity.UpgradeOpen();

            entity.Erase();
            return true;
        }
    }
}
