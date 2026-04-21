using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 「拾取原始记录」服务：在 <see cref="HyRoadLayers.RawPolylineLayer"/>（<c>05_hy_道路_原线</c>，
    /// ByLayer 252 本色，<b>启动即锁定</b>）上绘制 <see cref="Alignment.RawPickedPolyline"/> ——
    /// 用户拾取瞬间的几何快照。
    ///
    /// <para>工作台 v2 工作流下的定位（两层预览 + 一层存档）：</para>
    /// <list type="bullet">
    ///   <item>拾取 → 本服务画一条 <see cref="HyRoadXdata.KindAlignmentRawPick"/>（252 本色，跨会话留存）；</item>
    ///   <item>PI 调整 → 只有瞬态黄（<c>RoadAlignmentPreviewService</c>），原线层不再自动刷彩色；</item>
    ///   <item>点「预览」 → 追加分段彩色到 <c>05_hy_道路_预览</c>（<c>RoadAlignmentLivePreviewService</c>）；</item>
    ///   <item>Apply 定稿 → 在 <c>05_hy_道路_平面线位</c> 生成唯一 <see cref="HyRoadXdata.KindAlignment"/>，
    ///     并<b>临时解锁原线层</b>清掉本 Id 的 RawPick + 历史 DesignPreview 残留。</item>
    /// </list>
    ///
    /// <para><b>锁定层写入约定</b>：所有向 <c>05_hy_道路_原线</c> 写 / 擦实体的事务必须包裹在
    /// <see cref="LayerLockScope.Unlock"/> 内，finally 自动恢复锁定状态。</para>
    /// </summary>
    public static class RoadAlignmentRawPolylineService
    {
        /// <summary>
        /// 为当前图纸中所有线位绘制原线；先擦除旧的原线实体（幂等）。
        /// 若某线位 <see cref="Alignment.RawPickedPolyline"/> 为空但 <see cref="Alignment.Centerline"/> 有效，
        /// 则用当前中心线克隆补齐并计入 <paramref name="rawFieldsUpgraded"/>，供调用方决定是否落盘 JSON。
        /// </summary>
        public static (int Drawn, int RawFieldsUpgraded) DrawAll(Document doc, RoadDesign design)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (design == null) throw new ArgumentNullException(nameof(design));

            int drawn = 0;
            int upgraded = 0;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
                {
                    EraseAllInternal(tr, db);

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var a in design.Alignments)
                    {
                        if (a == null) continue;
                        var poly = a.RawPickedPolyline;
                        if (poly == null || poly.VertexCount < 2)
                        {
                            if (a.Centerline != null && a.Centerline.VertexCount >= 2)
                            {
                                a.RawPickedPolyline = a.Centerline.Clone();
                                poly = a.RawPickedPolyline;
                                upgraded++;
                            }
                            else continue;
                        }

                        var pl = RoadGeometryBridge.ToAutoCadPolyline(poly);
                        pl.Layer = HyRoadLayers.RawPolylineLayer;
                        pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0);
                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                        HyRoadXdata.Write(tr, db, pl, a.Id, HyRoadXdata.KindAlignmentRawPick, SchemaVersion.Current);
                        drawn++;
                    }
                }

                tr.Commit();
            }

            return (drawn, upgraded);
        }

        /// <summary>擦除当前图纸 ModelSpace 中所有 KIND=<see cref="HyRoadXdata.KindAlignmentRawPick"/> 的实体。</summary>
        public static int EraseAll(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var db = doc.Database;
            int n;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
                {
                    n = EraseAllInternal(tr, db);
                }
                tr.Commit();
            }
            return n;
        }

        /// <summary>
        /// 拾取流程专用：仅为 <paramref name="aln"/> 一条 alignment 绘制 RawPick 快照。
        /// 使用 <see cref="Alignment.RawPickedPolyline"/>（若为空则退化为 <see cref="Alignment.Centerline"/>）。
        /// 先按 (<see cref="HyRoadXdata.KindAlignmentRawPick"/>, aln.Id) 擦旧再画新，幂等。
        /// </summary>
        /// <returns>成功绘制返回 1；几何无效 / 文档关闭返回 0。</returns>
        public static int DrawForAlignment(Document doc, Alignment aln)
        {
            if (doc == null) return 0;
            if (aln == null) return 0;

            var poly = aln.RawPickedPolyline;
            if (poly == null || poly.VertexCount < 2)
            {
                if (aln.Centerline != null && aln.Centerline.VertexCount >= 2)
                {
                    aln.RawPickedPolyline = aln.Centerline.Clone();
                    poly = aln.RawPickedPolyline;
                }
                else return 0;
            }

            try
            {
                var db = doc.Database;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
                    {
                        EraseForAlignmentInternal(tr, db, aln.Id);

                        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        var pl = RoadGeometryBridge.ToAutoCadPolyline(poly);
                        pl.Layer = HyRoadLayers.RawPolylineLayer;
                        pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0);
                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                        HyRoadXdata.Write(tr, db, pl, aln.Id, HyRoadXdata.KindAlignmentRawPick, SchemaVersion.Current);
                    }

                    tr.Commit();
                }
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        private static int EraseForAlignmentInternal(Transaction tr, Database db, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, HyRoadXdata.KindAlignmentRawPick, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        private static int EraseAllInternal(Transaction tr, Database db)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, HyRoadXdata.KindAlignmentRawPick, StringComparison.Ordinal)) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }
    }
}
