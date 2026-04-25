using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// M10.2 平面分段扫掠落图服务：把 <see cref="CorridorPlanPolylines"/> 写入 AutoCAD ModelSpace，
    /// 按图层分类（红线 / 分界 / 标线），挂 HY_ROAD Xdata（KIND=<c>CorridorPlan</c>，ID=AlignmentId）。
    ///
    /// <para><b>双模式</b></para>
    /// <list type="bullet">
    ///   <item><see cref="CrossSectionDrawMode.SingleLine"/>：只画中心线 + 左右红线。</item>
    ///   <item><see cref="CrossSectionDrawMode.WithStructureThickness"/>：+ 板块分界线。</item>
    /// </list>
    ///
    /// <para>幂等：同一 AlignmentId 反复调 <see cref="Draw"/> 前会先 <see cref="Clear"/>。</para>
    /// </summary>
    public sealed class RoadPlanDrawService
    {
        /// <summary>KIND 常量 — 与 <see cref="HyRoadXdata.KindCorridorPlan"/> 对齐。</summary>
        public const string PlanKind = "CorridorPlan";

        /// <summary>绘制。返回新增实体数量。</summary>
        public int Draw(Transaction tr, Database db, CorridorPlanPolylines plan, CrossSectionDrawMode mode)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            HyRoadXdata.EnsureRegApp(tr, db);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int added = 0;

            added += AppendPoly(tr, db, ms, plan.CenterLine, HyRoadLayers.AlignmentLayer, plan.AlignmentId);
            added += AppendPoly(tr, db, ms, plan.LeftRedLine, HyRoadLayers.PlanRedLineLayer, plan.AlignmentId);
            added += AppendPoly(tr, db, ms, plan.RightRedLine, HyRoadLayers.PlanRedLineLayer, plan.AlignmentId);

            if (mode == CrossSectionDrawMode.WithStructureThickness)
            {
                foreach (var p in plan.LeftBandDividers ?? new List<Polyline3D>())
                    added += AppendPoly(tr, db, ms, p, HyRoadLayers.PlanBandDividerLayer, plan.AlignmentId);
                foreach (var p in plan.RightBandDividers ?? new List<Polyline3D>())
                    added += AppendPoly(tr, db, ms, p, HyRoadLayers.PlanBandDividerLayer, plan.AlignmentId);
            }

            return added;
        }

        /// <summary>清除与 <paramref name="alignmentId"/> 绑定的全部 CorridorPlan 实体。</summary>
        public int Clear(Transaction tr, Database db, Guid alignmentId)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (alignmentId == Guid.Empty) return 0;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var victims = new List<ObjectId>();
            foreach (ObjectId oid in ms)
            {
                var ent = tr.GetObject(oid, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, PlanKind, StringComparison.Ordinal)) continue;
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id != alignmentId) continue;
                victims.Add(oid);
            }

            foreach (var id in victims)
            {
                var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                ent.Erase();
            }
            return victims.Count;
        }

        private static int AppendPoly(Transaction tr, Database db, BlockTableRecord ms,
            Polyline3D domainPoly, string layer, Guid alignmentId)
        {
            if (domainPoly == null || domainPoly.VertexCount < 2) return 0;

            var acPoly = RoadGeometryBridge.ToAutoCadPolyline(domainPoly);
            acPoly.Layer = layer;
            acPoly.ColorIndex = 256; // ByLayer

            ms.AppendEntity(acPoly);
            tr.AddNewlyCreatedDBObject(acPoly, true);

            HyRoadXdata.Write(tr, db, acPoly, alignmentId, PlanKind, Domain.Models.Road.SchemaVersion.Current);
            return 1;
        }
    }
}
