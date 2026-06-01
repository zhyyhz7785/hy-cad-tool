using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// <c>hyRoadAlnOffset</c> — 基于 <see cref="Alignment.Centerline"/> 生成"平行偏移"辅助 Polyline。
    ///
    /// 典型用途：
    /// · 绘制左右路缘示意（半幅路宽 W/2）；
    /// · 路幅 / 硬路肩边线临时参考；
    /// · 三块板、两块板的绿化带分界。
    ///
    /// 交互流程：
    /// 1) 拾取 HY_ROAD Alignment Polyline（复用 <see cref="HyRoadXdata"/> 标识校验）；
    /// 2) 提示输入偏移量（正=左侧，负=右侧；单位 m）；
    /// 3) 调用 <see cref="CenterlineOffsetService.Offset(Polyline3D, double, double)"/> 生成新的 Polyline3D；
    /// 4) 转 AutoCAD <see cref="Autodesk.AutoCAD.DatabaseServices.Polyline"/>，挂到 <see cref="HyRoadLayers.OffsetLayer"/>，
    ///    同时写 HY_ROAD Xdata：KIND=<see cref="OffsetAuxiliaryKind"/>，ID=父 Alignment.Id，
    ///    便于后续"按父追踪 / 删除 Alignment 自动清理辅助线"。
    /// 5) 写回命令行："已生成偏移线 +5.000 m，顶点 N 个"。
    ///
    /// <b>已知局限</b>：
    /// · 当 |offset| ≥ 最小曲率半径时会产生自交 / 回环；本命令不做拓扑修正，靠用户约束输入合理。
    /// · 生成的辅助线不跟随原 Alignment 自动更新；中心线修改后需要重跑 <c>hyRoadAlnOffset</c>。
    /// </summary>
    public sealed class RoadAlignmentOffsetCommand
    {
        /// <summary>
        /// HY_ROAD 辅助偏移线的 KIND。与 "Alignment" / "StationLabel" / "GeometryPointLabel" 并列。
        /// 用于"按父 Alignment 过滤 / 清理"。
        /// </summary>
        public const string OffsetAuxiliaryKind = "OffsetAuxiliary";

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取要偏移的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var optOff = new PromptDoubleOptions("\n[道路] 偏移距离 (m，正=左侧、负=右侧)：")
            {
                AllowNone = false,
                AllowZero = false,
            };
            var rOff = ed.GetDouble(optOff);
            if (rOff.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }
            double offset = rOff.Value;

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元没有 HY_ROAD Alignment 标识。");
                    return;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }
                var alignment = design.Alignments.FirstOrDefault(a => a.Id == id);
                if (alignment == null || alignment.Centerline == null || alignment.Centerline.VertexCount < 2)
                {
                    ed.WriteMessage("\n[道路] 该 Alignment 在 Domain 中无有效中心线。");
                    return;
                }

                Polyline3D offsetPoly;
                try
                {
                    offsetPoly = CenterlineOffsetService.Offset(alignment.Centerline, offset);
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 偏移失败：{ex.Message}");
                    return;
                }

                var acadPoly = RoadGeometryBridge.ToAutoCadPolyline(offsetPoly);
                // 挂到 OffsetLayer（若不存在则留在当前层）
                if (LayerExists(tr, db, HyRoadLayers.OffsetLayer))
                    acadPoly.Layer = HyRoadLayers.OffsetLayer;

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ms.AppendEntity(acadPoly);
                tr.AddNewlyCreatedDBObject(acadPoly, true);

                // 挂父 Alignment 的 HY_ROAD Xdata，记录 KIND=OffsetAuxiliary + ID=alignmentId。
                HyRoadXdata.Write(tr, db, acadPoly, alignment.Id, OffsetAuxiliaryKind, SchemaVersion.Current);

                tr.Commit();

                string side = offset > 0 ? "左侧" : "右侧";
                ed.WriteMessage(
                    $"\n[道路] 已生成偏移线 {offset:+0.000;-0.000} m（{side}），顶点 {offsetPoly.VertexCount} 个，挂到图层「{HyRoadLayers.OffsetLayer}」，已标记父 Alignment。");
            }
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
