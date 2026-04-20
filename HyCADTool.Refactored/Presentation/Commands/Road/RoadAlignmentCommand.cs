using System.Collections.Generic;
using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P1：拾取 AutoCAD 多段线 → 抽取顶点 → 创建 / 同步 Domain Alignment → 写 Xdata → 发布事件。
    ///
    /// 交互：
    /// - 在命令行提示"选择一条多段线作为平面线位中心线"；
    /// - 仅接受 2D <c>Polyline</c>（<c>Polyline3d</c> / <c>Line</c> 在 P1 先不支持，P3 再并入）；
    /// - ESC / 右键取消时优雅退出，不抛异常；
    /// - 同一对象重复拾取 → 更新现有 Alignment 的 Centerline（依靠 Xdata 中的 GUID 识别）。
    ///
    /// 架构：
    /// - 命令本身保持"薄壳"：只负责交互与 Transaction 生命周期；
    /// - 几何转换 + Xdata 由 <see cref="RoadAlignmentService.ImportFromPolyline"/> 完成；
    /// - JSON 持久化 v1.1 起改为命令收尾同步调用 <see cref="RoadJsonExportService.SaveForDocument"/>，无异步 Timer。
    ///
    /// v1.2 收尾策略变更：
    /// - 不再无条件打开「路线工作台」；
    /// - 命令末尾运行 <see cref="Alignment.Validate"/> + bulge 诊断，**仅当检测到"违规"时**，
    ///   追问用户是否打开路线工作台修复（Y/N）；
    /// - 违规：顶点不足 / 平面长度为 0 / Elements 桩号链断续 / 所有 bulge = 0 且无 Arc 段（疑似样条拟合）。
    /// - 无违规则命令行仅回显成功信息，不再弹面板。需要直接进面板请用 <c>hyRoadAw</c>（<c>rlaw</c>）。
    /// </summary>
    public sealed class RoadAlignmentCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 选择一条多段线作为平面线位中心线：");
            peo.SetRejectMessage("\n只能选择二维多段线（Polyline）。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: true);
            peo.AllowNone = false;

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment;
            int rawBulgeNonZeroCount = 0;
            bool rawHasSegmentArcs = false;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var picked = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!(picked is Polyline poly))
                {
                    // AddAllowedClass(exactMatch:true) 理论上拦住了这条路径，但保留兜底（SDK 历史版本有差异）
                    ed.WriteMessage(
                        $"\n[道路] 所选实体类型为 {picked?.GetType().Name ?? "null"}，当前仅支持 LWPOLYLINE（AcDbPolyline）。"
                        + "\n       若是 2dPolyline（heavyweight） / 3dPolyline / Spline / Line+Arc，请先 PEDIT 或 DXF 转为 LWPOLYLINE。");
                    tr.Abort();
                    return;
                }

                // 读原始 bulge 诊断信息（直接问 AutoCAD，反映"DWG 端真实存了什么"）。
                // 视觉像弧但 bulge==0 的典型场景：PEDIT → S（Spline）拟合 —— 顶点不变、曲线靠样条控制点计算，
                // bulge 一律为 0，Polyline3D.ShouldSerializeBulges 就会省略 JSON 字段。
                for (int i = 0; i < poly.NumberOfVertices; i++)
                {
                    var segType = poly.GetSegmentType(i);
                    if (segType == SegmentType.Arc) rawHasSegmentArcs = true;
                    if (Math.Abs(poly.GetBulgeAt(i)) > 1e-12) rawBulgeNonZeroCount++;
                }

                // Xdata 写入需要 ForWrite
                poly.UpgradeOpen();

                // 先 rebind：如果 SAVEAS 把 doc.Name 换成了新路径，Registry 里的 design 还挂在老 key，
                // 不 rebind 就会让 ImportFromPolyline 的 GetOrCreate 在新 key 下建空壳，Xdata GUID 找不到匹配 Alignment，
                // 最终这条 polyline 会被当作"新 Alignment"加进空壳 design，老 key 下的那条就变成 JSON 孤儿。
                svc.RebindForDocument(doc.Name, tr, db);

                alignment = svc.ImportFromPolyline(doc.Name, tr, db, poly, out var addedNewAlignment);
                // 与 hyRoadAlnByPi 一致：首次登记的线位起桩号取 hy-settings 默认
                if (addedNewAlignment)
                {
                    var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults();
                    if (defaults != null)
                        alignment.StartStation = defaults.DefaultStartStation;
                }

                tr.Commit();
            }

            // 命令收尾同步落盘（v1.1 取代原防抖持久化服务）。
            // 在事务外执行，避免 I/O 延迟拉长事务生命周期；此时 doc.Name 与命令上下文一致，不存在异步时序问题。
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 平面线位已登记 {alignment.Name}："
                + $"Id={alignment.Id:N}，顶点 {alignment.Centerline.VertexCount} 个，"
                + $"含弧段 {rawBulgeNonZeroCount} 段，"
                + $"平面长度 {alignment.Centerline.GetPlanarLength():F3} m。");

            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存或 RoadDesign 为空）。先保存 DWG 后再次运行 hyRoadSave 即可。");

            // ── 违规 / 警告收集 ──────────────────────────────────────────
            // violations：会阻止后续 PI/ 桩号功能正常工作的问题，触发"是否打开工作台修复"追问。
            // advisories：仅提示，不触发追问。
            var violations = new List<string>();
            var advisories = new List<string>();

            // (V1) Alignment 连续性自检：顶点数 / 平面长度 / Elements 桩号链
            var validation = alignment.Validate();
            if (!validation.Ok)
            {
                foreach (var e in validation.Errors) violations.Add(e);
            }

            // (V2) 视觉疑似弧但 bulge 全 0：大概率是 PEDIT → S 样条拟合，Centerline 与屏幕不一致
            if (rawBulgeNonZeroCount == 0 && !rawHasSegmentArcs
                && alignment.Centerline.VertexCount >= 2)
            {
                violations.Add(
                    "未检测到任何弧段（所有 bulge=0）。若屏幕上是曲线，可能是 PEDIT → S 样条拟合或直线近似弧。"
                    + " 修复：PEDIT → D 还原折线 → PEDIT → F 圆弧拟合 → 重跑 hyRoadA；或用 hyRoadAlnByPi 走 PI 法。");
            }

            // (A1) 含弧段但没有 PI 源：本次登记成功，但后续 hyRoadAlnEditPi / InsertPi / DeletePi 不可用
            if (alignment.Centerline.HasArcs
                && (alignment.Source == null
                    || alignment.Source.PiElements == null
                    || alignment.Source.PiElements.Count < 2))
            {
                advisories.Add(
                    "当前多段线含弧段（bulge≠0），无法自动反推 PI 表。"
                    + " 若要使用 PI 编辑系列命令，请改用 hyRoadAlnByPi；当前线位的桩号 / 导出 / 反转 / 偏移仍可用。");
            }

            foreach (var a in advisories) ed.WriteMessage("\n[道路] 提示：" + a);

            if (violations.Count == 0)
            {
                // 无违规：静默收尾，不再弹面板。需要进面板请用 hyRoadAw（rlaw）。
                return;
            }

            ed.WriteMessage($"\n[道路] ⚠ 检测到 {violations.Count} 处问题：");
            foreach (var v in violations) ed.WriteMessage("\n  · " + v);

            // 追问：是否打开工作台修复
            var kw = new PromptKeywordOptions("\n[道路] 是否打开路线工作台修复？[是(Y)/否(N)] <是>：")
            {
                AllowNone = true,
            };
            kw.Keywords.Add("Y");
            kw.Keywords.Add("N");
            kw.Keywords.Default = "Y";
            var kwRes = ed.GetKeywords(kw);
            bool openWb =
                kwRes.Status == PromptStatus.None
                || (kwRes.Status == PromptStatus.OK && kwRes.StringResult == "Y");

            if (!openWb)
            {
                ed.WriteMessage("\n[道路] 已跳过修复。可稍后运行 hyRoadAw（rlaw）手动打开工作台。");
                return;
            }

            try
            {
                var panels = ServiceLocator.Resolve<PanelManager>();
                panels?.ShowAlignmentWorkbench(alignment.Id, piIndex: null);
            }
            catch
            {
                // 静默：面板未注册 / WPF 未初始化等场景不应让命令失败
            }
        }
    }
}
