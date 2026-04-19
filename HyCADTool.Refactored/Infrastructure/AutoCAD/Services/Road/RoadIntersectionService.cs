using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 平面交叉口 AutoCAD 绘制服务（Infrastructure 层）—— 把 <see cref="Intersection"/> 域对象落到 DWG 上。
    ///
    /// <para><b>职责</b></para>
    /// <list type="bullet">
    /// <item><see cref="DrawIntersection"/>：把 <see cref="Intersection.CornerArcs"/> 逐个画为 AutoCAD
    /// <see cref="Arc"/>，挂 HY_ROAD Xdata（KIND="Intersection"，ID=Intersection.Id），放到 <see cref="HyRoadLayers.IntersectionLayer"/>；</item>
    /// <item><see cref="ClearIntersectionEntities"/>：按 HY_ROAD Xdata (ID=Intersection.Id, KIND="Intersection") 扫描模型空间，
    /// 擦除旧图元（用于幂等重建）；</item>
    /// <item><see cref="RebuildIntersection"/>：先 Clear 再 Draw，保证重入一致。</item>
    /// </list>
    ///
    /// <para><b>线程 / 事务约束</b></para>
    /// 所有方法均在调用方提供的 <see cref="Transaction"/> 中执行；本类不 StartTransaction，也不 Commit。
    /// 调用方负责事务边界、图层存在性（不存在则保留当前图层，不抛异常）。
    ///
    /// <para><b>决策</b></para>
    /// 与 <c>RoadAlignmentService</c> 不同，本类 <b>不持有状态</b>（没有 Registry / EventBus 字段）：
    /// 交叉口的 Domain 变更已在命令层写 RoadDesign + 调 RoadJsonExportService，这里只负责"把几何画出来"。
    /// </summary>
    public sealed class RoadIntersectionService
    {
        /// <summary>HY_ROAD Xdata KIND：交叉口转角圆弧。</summary>
        public const string IntersectionKind = "Intersection";

        /// <summary>HY_ROAD Xdata KIND：交叉口路缘外边线直段（v1.1 <c>hyRoadIntersectionKerbChain</c>）。</summary>
        public const string IntersectionKerbKind = "IntersectionKerb";

        /// <summary>
        /// 把 <paramref name="intersection"/> 的所有 <see cref="CornerArc"/> 画成 AutoCAD <see cref="Arc"/>。
        /// </summary>
        /// <param name="tr">已打开的事务。</param>
        /// <param name="db">当前 DWG 数据库。</param>
        /// <param name="intersection">域交叉口对象（Legs / CornerArcs 已由 Designer 填充）。</param>
        /// <param name="layerName">目标图层（若不存在则保留当前图层，不抛异常）。</param>
        /// <returns>新建的 Arc 的 <see cref="ObjectId"/> 列表（顺序与 <see cref="Intersection.CornerArcs"/> 一致）。</returns>
        public IReadOnlyList<ObjectId> DrawIntersection(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var results = new List<ObjectId>(intersection.CornerArcs.Count);
            if (intersection.CornerArcs.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var ca in intersection.CornerArcs)
            {
                var arc = ToAutoCadArc(ca);
                if (useLayer) arc.Layer = layerName;

                ms.AppendEntity(arc);
                tr.AddNewlyCreatedDBObject(arc, true);

                HyRoadXdata.Write(tr, db, arc, intersection.Id, IntersectionKind, SchemaVersion.Current);
                results.Add(arc.ObjectId);
            }

            return results;
        }

        /// <summary>
        /// 画 Kerb 链：把 <see cref="KerbChainDesigner.ComputeKerbSegments(Intersection)"/> 产出的直段
        /// 逐条画为 AutoCAD <see cref="Line"/>，挂 HY_ROAD Xdata（KIND=<see cref="IntersectionKerbKind"/>，
        /// ID=<see cref="Intersection.Id"/>），图层同 <paramref name="layerName"/>（与 CornerArc 共用图层）。
        /// </summary>
        public IReadOnlyList<ObjectId> DrawKerbChain(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var segments = KerbChainDesigner.ComputeKerbSegments(intersection);
            var results = new List<ObjectId>(segments.Count);
            if (segments.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var s in segments)
            {
                var line = new Line(
                    new Point3d(s.From.X, s.From.Y, 0),
                    new Point3d(s.To.X, s.To.Y, 0));
                if (useLayer) line.Layer = layerName;

                ms.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);

                HyRoadXdata.Write(tr, db, line, intersection.Id, IntersectionKerbKind, SchemaVersion.Current);
                results.Add(line.ObjectId);
            }
            return results;
        }

        /// <summary>
        /// 按 HY_ROAD Xdata 扫模型空间，擦除所有 KIND ∈ <c>{IntersectionKind, IntersectionKerbKind}</c>
        /// 且 ID=<paramref name="intersectionId"/> 的图元。
        /// </summary>
        /// <returns>被删除的图元数量。</returns>
        public int ClearIntersectionEntities(Transaction tr, Database db, Guid intersectionId)
            => ClearEntitiesByKinds(tr, db, intersectionId, new[] { IntersectionKind, IntersectionKerbKind });

        /// <summary>
        /// 仅擦除路缘链（保留 CornerArc）。用于 <c>HasKerbChain</c> 从 true 切换到 false 的场景。
        /// </summary>
        public int ClearKerbEntities(Transaction tr, Database db, Guid intersectionId)
            => ClearEntitiesByKinds(tr, db, intersectionId, new[] { IntersectionKerbKind });

        private static int ClearEntitiesByKinds(
            Transaction tr, Database db, Guid intersectionId, IReadOnlyList<string> kinds)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            int removed = 0;
            foreach (var entId in ms)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                var kind = HyRoadXdata.ReadKind(tr, ent);
                bool matched = false;
                for (int i = 0; i < kinds.Count; i++)
                {
                    if (string.Equals(kind, kinds[i], StringComparison.Ordinal)) { matched = true; break; }
                }
                if (!matched) continue;

                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty || id != intersectionId) continue;

                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// "擦旧 + 画新" 幂等重建：同时负责 CornerArc 与 Kerb 链（当 <see cref="Intersection.HasKerbChain"/> = true）。
        /// 返回新的 Arc ObjectId 列表。
        /// </summary>
        public IReadOnlyList<ObjectId> RebuildIntersection(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            ClearIntersectionEntities(tr, db, intersection.Id);
            var arcIds = DrawIntersection(tr, db, intersection, layerName);
            if (intersection.HasKerbChain)
            {
                DrawKerbChain(tr, db, intersection, layerName);
            }
            return arcIds;
        }

        /// <summary>
        /// 把 Domain <see cref="CornerArc"/> 转成 AutoCAD <see cref="Arc"/>。
        /// <para>语义映射：</para>
        /// <list type="bullet">
        /// <item><see cref="CornerArc.Center"/> → Arc.Center（Z=0）；</item>
        /// <item><see cref="CornerArc.Radius"/> → Arc.Radius；</item>
        /// <item>AutoCAD Arc 的 StartAngle / EndAngle 始终 <b>CCW 扫过</b>（从 StartAngle 到 EndAngle 沿 CCW）。
        /// Domain CornerArc 对 CCW 相邻臂总是出 <b>CW</b>（SweepAngle &lt; 0），因此我们交换起止角给 AutoCAD，
        /// 即 <c>Arc.StartAngle = Domain.EndAngle，Arc.EndAngle = Domain.StartAngle</c>。</item>
        /// </list>
        /// 对 SweepAngle &gt; 0 的少数 Domain 情况（理论边界），按原顺序保留。
        /// </summary>
        internal static Arc ToAutoCadArc(CornerArc ca)
        {
            double s = ca.StartAngle;
            double e = ca.EndAngle;
            double startAngle, endAngle;
            if (ca.SweepAngle < 0)
            {
                startAngle = e;
                endAngle = s;
            }
            else
            {
                startAngle = s;
                endAngle = e;
            }
            return new Arc(new Point3d(ca.Center.X, ca.Center.Y, 0), ca.Radius, startAngle, endAngle);
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
