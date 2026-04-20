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
    /// 路线工作台「主预览」实体服务：把当前选中 Alignment 的 <see cref="Alignment.Centerline"/>
    /// 以 Polyline 形式写到图层 <see cref="HyRoadLayers.LivePreviewLayer"/>，颜色 ByLayer
    /// （黄色 / ACI <see cref="HyRoadLayers.LivePreviewColor"/>），挂 HY_ROAD Xdata
    /// （KIND=<see cref="HyRoadXdata.KindAlignmentLivePreview"/>、ID=AlignmentId）。
    ///
    /// <para>与 <see cref="RoadAlignmentPreviewService"/>（Transient）的分工：</para>
    /// <list type="bullet">
    ///   <item>本服务 = 「主预览」：低频（切换 Alignment / Apply / Reverse 成功后重画），
    ///     用户可 <c>ERASE</c> / <c>LAYOFF</c>，面板关闭由 <see cref="EraseAll"/> 托底。</item>
    ///   <item>Transient 服务 = 「PI 实时预览」：高频（滑块每挪一下都重画），继续用
    ///     <see cref="TransientManager"/>，避免每次改参数都污染 AutoCAD Undo 栈。</item>
    /// </list>
    ///
    /// <para>不变量：任意时刻当前 ModelSpace 里最多存在 <b>一条</b> <c>KindAlignmentLivePreview</c> 实体。
    /// 写入前先 <c>EraseAllInternal</c>，保证幂等。</para>
    ///
    /// <para>与 <see cref="RoadAlignmentRawPolylineService"/> 的区别：原线是创建时刻快照 / 跨会话留存；
    /// 本服务只是工作台会话期间的视觉反馈，面板关闭即清。</para>
    /// </summary>
    public static class RoadAlignmentLivePreviewService
    {
        /// <summary>
        /// 为 <paramref name="aln"/> 绘制主预览实体。先擦除所有旧的 LivePreview 实体（幂等），
        /// 再按当前中心线重建一条 Polyline。传入 null / 中心线无效时退化为 <see cref="EraseAll"/>。
        /// </summary>
        /// <returns>成功绘制返回 1；仅擦除 / 无法绘制返回 0。失败（LockDocument 抛 / 文档销毁）静默返回 0。</returns>
        public static int DrawForAlignment(Document doc, Alignment aln)
        {
            if (doc == null) return 0;

            try
            {
                var db = doc.Database;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    EraseAllInternal(tr, db);

                    if (aln?.Centerline == null || aln.Centerline.VertexCount < 2)
                    {
                        tr.Commit();
                        return 0;
                    }

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var pl = RoadGeometryBridge.ToAutoCadPolyline(aln.Centerline);
                    pl.Layer = HyRoadLayers.LivePreviewLayer;
                    pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0);
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    HyRoadXdata.Write(tr, db, pl, aln.Id, HyRoadXdata.KindAlignmentLivePreview, SchemaVersion.Current);

                    tr.Commit();
                    return 1;
                }
            }
            catch
            {
                // 文档被关闭 / LockDocument 失败等：VM 线程不要把异常扩散出去
                return 0;
            }
        }

        /// <summary>
        /// 擦除当前文档 ModelSpace 中所有 KIND=<see cref="HyRoadXdata.KindAlignmentLivePreview"/> 的实体（幂等）。
        /// </summary>
        /// <returns>擦除条数；doc 为 null 或异常时返回 0。</returns>
        public static int EraseAll(Document doc)
        {
            if (doc == null) return 0;

            try
            {
                var db = doc.Database;
                int n;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    n = EraseAllInternal(tr, db);
                    tr.Commit();
                }
                return n;
            }
            catch
            {
                return 0;
            }
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
                if (!string.Equals(k, HyRoadXdata.KindAlignmentLivePreview, StringComparison.Ordinal)) continue;
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
