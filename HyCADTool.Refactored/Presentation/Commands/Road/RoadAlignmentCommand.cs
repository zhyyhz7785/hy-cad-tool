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
    /// 交互（v1.2.1 起支持批量）：
    /// - 在命令行提示"选择多段线作为平面线位中心线（可多选）"；
    /// - 走 <see cref="Editor.GetSelection"/> + LWPOLYLINE 过滤器，支持框选、Ctrl 多选；
    /// - 仅接受 2D <c>Polyline</c>（<c>Polyline3d</c> / <c>Line</c> 在 P1 先不支持，P3 再并入）；
    /// - ESC / 右键取消时优雅退出，不抛异常；
    /// - 同一对象重复拾取 → 更新现有 Alignment 的 Centerline（依靠 Xdata 中的 GUID 识别）。
    ///
    /// 架构：
    /// - 命令本身保持"薄壳"：只负责交互与 Transaction 生命周期；
    /// - 多条 Polyline 在同一个事务里依次导入，减少 rebind / BlockTable 开锁开销；
    /// - 几何转换 + Xdata 由 <see cref="RoadAlignmentService.ImportFromPolyline"/> 完成；
    /// - JSON 持久化 v1.1 起改为命令收尾同步调用 <see cref="RoadJsonExportService.SaveForDocument"/>，无异步 Timer；
    ///   批量收尾 JSON 只写一次，避免多条 polyline 各自落盘导致 I/O 抖动。
    ///
    /// v1.2 收尾策略变更（多选兼容）：
    /// - 不再无条件打开「路线工作台」；
    /// - 命令末尾对"每条新登记 / 更新的 Alignment"运行 <see cref="Alignment.Validate"/> + bulge 诊断；
    /// - 若**全部**线位都无违规：静默收尾，只在命令行列出汇总信息；
    /// - 若**存在**违规的线位：追问用户是否打开路线工作台修复（Y/N）；
    ///   打开时预选**首条有违规的 Alignment**，帮助用户优先修复问题线位。
    /// - 违规：顶点不足 / 平面长度为 0 / Elements 桩号链断续 / 所有 bulge = 0 且无 Arc 段（疑似样条拟合）。
    /// - 无违规则命令行仅回显成功信息，不再弹面板。需要直接进面板请用 <c>hyRoadAw</c>（<c>rlaw</c>）。
    /// </summary>
    public sealed class RoadAlignmentCommand
    {
        /// <summary>
        /// 单条 Polyline 的导入结果摘要（命令行回显 / 违规汇总使用）。
        /// </summary>
        private sealed class ImportOutcome
        {
            public Alignment Alignment;
            public int RawBulgeNonZeroCount;
            public bool RawHasSegmentArcs;
            public bool RawHasSegmentLines;
            public bool WasNew;
            public readonly List<string> Violations = new List<string>();
            public readonly List<string> Advisories = new List<string>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            // 批量选择：LWPOLYLINE 过滤器等效旧逻辑 AddAllowedClass(typeof(Polyline), exactMatch:true)
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n[道路] 选择多段线作为平面线位中心线（可多选，支持框选）：",
                AllowDuplicates = false,
            };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });

            var psr = ed.GetSelection(pso, filter);
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            var outcomes = new List<ImportOutcome>();
            int skippedNotPolyline = 0;
            int skippedImportFailed = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 先 rebind 一次：如果 SAVEAS 把 doc.Name 换成了新路径，Registry 里的 design 还挂在老 key，
                // 不 rebind 就会让 ImportFromPolyline 的 GetOrCreate 在新 key 下建空壳，Xdata GUID 找不到匹配 Alignment，
                // 最终这条 polyline 会被当作"新 Alignment"加进空壳 design，老 key 下的那条就变成 JSON 孤儿。
                // 多条导入共享同一 rebind 结果，减少重复扫描开销。
                svc.RebindForDocument(doc.Name, tr, db);

                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;

                    DBObject picked;
                    try { picked = tr.GetObject(so.ObjectId, OpenMode.ForRead); }
                    catch { skippedImportFailed++; continue; }

                    if (!(picked is Polyline poly))
                    {
                        // SelectionFilter("LWPOLYLINE") 理论上拦住了这条路径，但保留兜底（SDK 历史版本有差异）
                        skippedNotPolyline++;
                        continue;
                    }

                    var outcome = new ImportOutcome();

                    // 读原始 bulge 诊断信息（直接问 AutoCAD，反映"DWG 端真实存了什么"）。
                    // 视觉像弧但 bulge==0 的典型场景：PEDIT → S（Spline）拟合 —— 顶点不变、曲线靠样条控制点计算，
                    // bulge 一律为 0，Polyline3D.ShouldSerializeBulges 就会省略 JSON 字段。
                    // GetSegmentType 对每段给出 Line / Arc / Coincident / Point / Empty 等。
                    // 纯直线折线应当返回 Line（非 Arc / 非异常），这是 V2 违规判定用来排除"直线被误报为样条拟合"的关键。
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        var segType = poly.GetSegmentType(i);
                        if (segType == SegmentType.Arc) outcome.RawHasSegmentArcs = true;
                        else if (segType == SegmentType.Line) outcome.RawHasSegmentLines = true;
                        if (Math.Abs(poly.GetBulgeAt(i)) > 1e-12) outcome.RawBulgeNonZeroCount++;
                    }

                    try
                    {
                        // Xdata 写入需要 ForWrite
                        poly.UpgradeOpen();
                        outcome.Alignment = svc.ImportFromPolyline(doc.Name, tr, db, poly, out outcome.WasNew);
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[道路] 导入 Handle={so.ObjectId.Handle} 失败：{ex.Message}");
                        skippedImportFailed++;
                        continue;
                    }

                    if (outcome.Alignment == null)
                    {
                        skippedImportFailed++;
                        continue;
                    }

                    // 与 hyRoadAlnByPi 一致：首次登记的线位起桩号取 hy-settings 默认
                    if (outcome.WasNew)
                    {
                        var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults();
                        if (defaults != null)
                            outcome.Alignment.StartStation = defaults.DefaultStartStation;
                    }

                    outcomes.Add(outcome);
                }

                tr.Commit();
            }

            if (outcomes.Count == 0)
            {
                ed.WriteMessage(
                    $"\n[道路] 未成功导入任何线位（选中 {psr.Value.Count} 条，"
                    + $"非 Polyline {skippedNotPolyline} 条，导入失败 {skippedImportFailed} 条）。");
                return;
            }

            // 命令收尾同步落盘（v1.1 取代原防抖持久化服务）。
            // 批量场景 JSON 只写一次，避免多条 polyline 各自落盘造成抖动 / 文件冲突。
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            // 对每条线位做 Validate + bulge 诊断，同时输出单行摘要到命令行
            int newCount = 0, updatedCount = 0;
            foreach (var o in outcomes)
            {
                if (o.WasNew) newCount++; else updatedCount++;

                ed.WriteMessage(
                    $"\n[道路] [{(o.WasNew ? "+" : "~")}] {o.Alignment.Name}："
                    + $"Id={o.Alignment.Id:N}，顶点 {o.Alignment.Centerline.VertexCount} 个，"
                    + $"含弧段 {o.RawBulgeNonZeroCount} 段，"
                    + $"平面长度 {o.Alignment.Centerline.GetPlanarLength():F3} m。");

                // (V1) Alignment 连续性自检：顶点数 / 平面长度 / Elements 桩号链
                var validation = o.Alignment.Validate();
                if (!validation.Ok)
                {
                    foreach (var e in validation.Errors) o.Violations.Add(e);
                }

                // (V2) 视觉疑似弧但 bulge 全 0：大概率是 PEDIT → S 样条拟合，Centerline 与屏幕不一致
                if (o.RawBulgeNonZeroCount == 0 && !o.RawHasSegmentArcs
                    && o.Alignment.Centerline.VertexCount >= 2)
                {
                    o.Violations.Add(
                        "未检测到任何弧段（所有 bulge=0）。若屏幕上是曲线，可能是 PEDIT → S 样条拟合或直线近似弧。"
                        + " 修复：PEDIT → D 还原折线 → PEDIT → F 圆弧拟合 → 重跑 hyRoadA；或用 hyRoadAlnByPi 走 PI 法。");
                }

                // (A1) 含弧段但没有 PI 源：本次登记成功，但后续 hyRoadAlnEditPi / InsertPi / DeletePi 不可用
                if (o.Alignment.Centerline.HasArcs
                    && (o.Alignment.Source == null
                        || o.Alignment.Source.PiElements == null
                        || o.Alignment.Source.PiElements.Count < 2))
                {
                    o.Advisories.Add(
                        "当前多段线含弧段（bulge≠0），无法自动反推 PI 表。"
                        + " 若要使用 PI 编辑系列命令，请改用 hyRoadAlnByPi；当前线位的桩号 / 导出 / 反转 / 偏移仍可用。");
                }
            }

            ed.WriteMessage(
                $"\n[道路] 批量导入完成：新建 {newCount} 条，更新 {updatedCount} 条"
                + (skippedNotPolyline + skippedImportFailed > 0
                    ? $"，跳过 {skippedNotPolyline + skippedImportFailed} 条"
                    : string.Empty)
                + "。");

            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存或 RoadDesign 为空）。先保存 DWG 后再次运行 hyRoadSave 即可。");

            // 打印 advisories（不阻断）
            foreach (var o in outcomes)
            {
                foreach (var a in o.Advisories)
                    ed.WriteMessage($"\n[道路] 提示（{o.Alignment.Name}）：{a}");
            }

            // 汇总违规并追问是否打开工作台（只对首条违规线位预选）
            int totalViolations = 0;
            ImportOutcome firstBad = null;
            foreach (var o in outcomes)
            {
                if (o.Violations.Count == 0) continue;
                if (firstBad == null) firstBad = o;
                totalViolations += o.Violations.Count;
                ed.WriteMessage($"\n[道路] ⚠ {o.Alignment.Name} 检测到 {o.Violations.Count} 处问题：");
                foreach (var v in o.Violations) ed.WriteMessage("\n  · " + v);
            }

            if (firstBad == null)
            {
                // 所有线位均无违规：静默收尾，不再弹面板。需要进面板请用 hyRoadAw（rlaw）。
                return;
            }

            // 追问：是否打开工作台修复（定位到首条违规线位）
            var kw = new PromptKeywordOptions(
                $"\n[道路] 共 {totalViolations} 处问题，是否打开路线工作台修复（将定位到 {firstBad.Alignment.Name}）？[是(Y)/否(N)] <是>：")
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
                panels?.ShowAlignmentWorkbench(firstBad.Alignment.Id, piIndex: null);
            }
            catch
            {
                // 静默：面板未注册 / WPF 未初始化等场景不应让命令失败
            }
        }
    }
}
