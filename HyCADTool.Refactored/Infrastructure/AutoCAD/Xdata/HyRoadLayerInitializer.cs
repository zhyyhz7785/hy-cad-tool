using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// HyRoad 模块图层的<b>锁定 / 特殊属性</b>初始化工具。
    /// 普通「存在即可」的图层在插件初始化时由 <c>ILayerService.EnsureUserLayerItems</c>（来自 hy-settings 层表）批量创建；
    /// 需要额外强制属性（如锁定）的图层走本类。
    ///
    /// <para>当前受管图层：</para>
    /// <list type="bullet">
    ///   <item><c>05_hy_道路_原线</c>（<see cref="HyRoadLayers.RawPolylineLayer"/>）— 启动即锁定，
    ///     防止用户在工作台操作过程中误改 / 误删 <c>KindAlignmentRawPick</c> 档案线。
    ///     工作台内部写入实体需临时解锁（<see cref="Services.Road.LayerLockScope.Unlock"/>）。</item>
    /// </list>
    /// </summary>
    public static class HyRoadLayerInitializer
    {
        /// <summary>
        /// 幂等确保 <c>05_hy_道路_原线</c> 图层存在且 <see cref="LayerTableRecord.IsLocked"/> = true。
        /// 不存在则以 ACI <see cref="HyRoadLayers.RawPolylineColor"/> 创建 + 锁定；
        /// 已存在仅在当前未锁定时置锁（不改颜色，避免覆盖用户自定义）。
        /// </summary>
        /// <returns>true 表示本次执行到写库；false 表示无 doc / 异常。</returns>
        public static bool EnsureRawPolylineLayerLocked(Document doc)
        {
            if (doc == null) return false;
            try
            {
                var db = doc.Database;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(HyRoadLayers.RawPolylineLayer))
                    {
                        lt.UpgradeOpen();
                        var layer = new LayerTableRecord
                        {
                            Name = HyRoadLayers.RawPolylineLayer,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, HyRoadLayers.RawPolylineColor),
                            IsLocked = true,
                        };
                        lt.Add(layer);
                        tr.AddNewlyCreatedDBObject(layer, true);
                    }
                    else
                    {
                        var layer = (LayerTableRecord)tr.GetObject(lt[HyRoadLayers.RawPolylineLayer], OpenMode.ForRead);
                        if (!layer.IsLocked)
                        {
                            layer.UpgradeOpen();
                            layer.IsLocked = true;
                        }
                    }
                    tr.Commit();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
