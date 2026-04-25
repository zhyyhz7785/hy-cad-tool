using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using HyCADTool.Presentation.ViewModels.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 项目树交互动作的 AutoCAD 实现（045 / M4）。
    ///
    /// 职责：
    /// <list type="bullet">
    ///   <item><see cref="ZoomTo"/> / <see cref="Highlight"/>：基于 <c>HY_ROAD</c> Xdata ID 扫描 ModelSpace，命中即调用
    ///         <c>Editor.Zoom</c> 或 <c>Entity.Highlight</c>；</item>
    ///   <item><see cref="Open"/>：按 <see cref="RoadTreeNode.Kind"/> 往 AutoCAD 命令行投递对应命令（命令在 ReCall/Refactored 注册），
    ///         不能自动打开时打印提示；</item>
    ///   <item><see cref="Rename"/> / <see cref="Delete"/> / <see cref="ExportLandXml"/>：本阶段打印提示，等 M6 接入具体命令。</item>
    /// </list>
    ///
    /// <para>实现坚持 <c>hycad-project-pitfalls</c> skill 的两条底线：
    /// <list type="bullet">
    ///   <item>全程通过 <see cref="Document.LockDocument"/> + <see cref="Transaction"/> 访问 AutoCAD 数据库；</item>
    ///   <item>任何异常都吞到 <see cref="Editor.WriteMessage"/>，不污染 UI 线程的 Tree 选择流程。</item>
    /// </list></para>
    /// </summary>
    public sealed class AutoCadRoadTreeInteractionHandler : IRoadTreeInteractionHandler
    {
        /// <summary>双击：按 Kind 投递对应 AutoCAD 命令。</summary>
        public void Open(RoadTreeNode node)
        {
            if (node == null) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;

            string cmd = MapKindToCommand(node.Kind);
            if (string.IsNullOrEmpty(cmd))
            {
                Tell(doc, $"[项目树] 暂无对应编辑器（Kind={node.Kind}，'{node.Header}'）。");
                return;
            }

            try
            {
                // 用 SendStringToExecute 避免直接调用 CommandMethod 的异步事务冲突；末尾空格触发回车。
                doc.SendStringToExecute($"_{cmd} ", true, false, true);
                Tell(doc, $"[项目树] 已投递命令：{cmd}（节点：{node.Header}）。");
            }
            catch (Exception ex)
            {
                Tell(doc, $"[项目树] 打开失败：{ex.Message}");
            }
        }

        /// <summary>ZoomTo：按 Xdata ID 找实体 → 取包围盒 → <see cref="Editor.Zoom"/>。</summary>
        public void ZoomTo(RoadTreeNode node)
        {
            if (node == null || node.TargetId == Guid.Empty) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                Extents3d? ext = ComputeExtents(doc, node.TargetId);
                if (!ext.HasValue)
                {
                    Tell(doc, $"[项目树] 未在图中找到 ID={node.TargetId:N} 的实体。");
                    return;
                }

                using (doc.LockDocument())
                {
                    // 在 viewport 上加一点边距（10%）再缩放
                    var min = ext.Value.MinPoint;
                    var max = ext.Value.MaxPoint;
                    double dx = (max.X - min.X) * 0.1;
                    double dy = (max.Y - min.Y) * 0.1;
                    var margined = new Extents3d(
                        new Point3d(min.X - dx, min.Y - dy, min.Z),
                        new Point3d(max.X + dx, max.Y + dy, max.Z));

                    ZoomToExtents(doc.Editor, margined);
                }
            }
            catch (Exception ex)
            {
                Tell(doc, $"[项目树] ZoomTo 失败：{ex.Message}");
            }
        }

        /// <summary>Highlight：按 Xdata ID 找实体 → <see cref="Entity.Highlight"/>，不动视图。</summary>
        public void Highlight(RoadTreeNode node)
        {
            if (node == null || node.TargetId == Guid.Empty) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                var db = doc.Database;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var oid in FindEntitiesByHyRoadId(tr, db, node.TargetId))
                    {
                        var ent = tr.GetObject(oid, OpenMode.ForRead) as Entity;
                        ent?.Highlight();
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // 高亮失败不吐槽 —— 选择过程中静默
            }
        }

        /// <summary>重命名：项目根 <see cref="RoadProject.Name"/> 或 <see cref="Alignment.Name"/> 并落盘 JSON。</summary>
        public void Rename(RoadTreeNode node)
        {
            if (node == null) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            try
            {
                if (node.Kind == RoadTreeNodeKind.Project && node.Tag is RoadProject prj)
                {
                    var so = new PromptStringOptions($"\n[项目树] 新项目名称 <{prj.Name}>：")
                    {
                        AllowSpaces = true,
                    };
                    var r = ed.GetString(so);
                    if (r.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(r.StringResult)) return;
                    prj.Name = r.StringResult.Trim();
                    var primary = prj.GetPrimaryDesign();
                    if (primary != null) primary.ProjectName = prj.Name;
                    prj.LastModifiedUtc = DateTime.UtcNow;
                    if (primary != null) primary.LastModifiedUtc = prj.LastModifiedUtc;

                    var ex = ServiceLocator.Resolve<RoadJsonExportService>();
                    var pathP = ex.SaveProjectForDocument(prj, doc.Name);
                    var reg = ServiceLocator.Resolve<RoadDesignRegistry>();
                    if (reg.TryGet(doc.Name, out var d) && d != null)
                    {
                        var pathD = ex.SaveForDocument(d, doc.Name);
                        if (pathD != null) Tell(doc, $"[项目树] 已重命名项目，已落盘：{pathD}");
                    }
                    else if (pathP != null)
                    {
                        Tell(doc, $"[项目树] 已重命名项目，已落盘：{pathP}");
                    }
                    else
                    {
                        ex.SaveProject(prj, RoadJsonExportService.GetDefaultProjectJsonPath(doc.Name));
                        Tell(doc, "[项目树] 项目名称已更新（.roadproject.json）。");
                    }
                    return;
                }

                if (node.Kind == RoadTreeNodeKind.Alignment && node.Tag is Alignment aln)
                {
                    var so = new PromptStringOptions($"\n[项目树] 新路线名称 <{aln.Name}>：")
                    {
                        AllowSpaces = true,
                    };
                    var r = ed.GetString(so);
                    if (r.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(r.StringResult)) return;
                    aln.Name = r.StringResult.Trim();
                    var reg = ServiceLocator.Resolve<RoadDesignRegistry>();
                    if (!reg.TryGet(doc.Name, out var design) || design == null) return;
                    design.LastModifiedUtc = DateTime.UtcNow;
                    var ex = ServiceLocator.Resolve<RoadJsonExportService>();
                    var path = ex.SaveForDocument(design, doc.Name);
                    if (path != null) Tell(doc, $"[项目树] 已重命名路线，已落盘：{path}");
                    else Tell(doc, "[项目树] 路线名称已更新（未写盘，请先保存 DWG）。");
                    return;
                }

                Tell(doc, $"[项目树] 此节点类型暂不支持重命名（Kind={node.Kind}）。");
            }
            catch (Exception ex)
            {
                Tell(doc, "[项目树] 重命名失败：" + ex.Message);
            }
        }

        /// <summary>删除（M4 仅占位）。</summary>
        public void Delete(RoadTreeNode node)
        {
            if (node == null) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;
            Tell(doc, $"[项目树] 删除 '{node.Header}'（Kind={node.Kind}）—— 该命令等 M6 接入。");
        }

        /// <summary>导出 LandXML（M4 仅占位）。</summary>
        public void ExportLandXml(RoadTreeNode node)
        {
            if (node == null) return;
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;

            // 若是 Alignment 节点，建议走 hyRoadAlnExportXml；其他类型 M6 再扩展
            if (node.Kind == RoadTreeNodeKind.Alignment)
            {
                Tell(doc, $"[项目树] 导出 LandXML（Alignment：{node.Header}）—— 请在命令行直接运行 hyRoadAlnExportXml。");
                return;
            }

            Tell(doc, $"[项目树] 暂不支持此类节点的 LandXML 导出（Kind={node.Kind}）。");
        }

        public void NewAlignment() => RunCmd("hyRoadAName");

        public void AssignCrossSection(RoadTreeNode contextNode) => RunCmd("hyRoadAlnAssign");

        public void GeneratePlanFromTree(RoadTreeNode contextNode) => RunCmd("hyRoadAlnPlan");

        public void DetectIntersections() => RunCmd("hyRoadAutoIntersection");

        private static void RunCmd(string cmd)
        {
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                doc.SendStringToExecute($"_{cmd} ", true, false, true);
            }
            catch
            { /* 忽略 */ }
        }

        // =============================================================
        //  私有：辅助
        // =============================================================

        private static string MapKindToCommand(RoadTreeNodeKind kind)
        {
            switch (kind)
            {
                case RoadTreeNodeKind.Alignment: return "hyRoadAlnWorkbench";
                case RoadTreeNodeKind.Profile: return "hyRoadProf";
                case RoadTreeNodeKind.Template: return "hyRoadCsLoad";
                case RoadTreeNodeKind.CrossSection: return "hyRoadCs";
                case RoadTreeNodeKind.Corridor: return "hyRoadCorridor";
                case RoadTreeNodeKind.Intersection: return "hyRoadIntersectionEdit";
                case RoadTreeNodeKind.CodeAudit: return "hyRoadCheckCode";
                default: return null;
            }
        }

        private static Extents3d? ComputeExtents(Document doc, Guid targetId)
        {
            var db = doc.Database;
            Extents3d? acc = null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var oid in FindEntitiesByHyRoadId(tr, db, targetId))
                {
                    var ent = tr.GetObject(oid, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    try
                    {
                        var e = ent.GeometricExtents;
                        if (acc.HasValue)
                        {
                            var a = acc.Value;
                            a.AddExtents(e);
                            acc = a;
                        }
                        else
                        {
                            acc = e;
                        }
                    }
                    catch { /* GeometricExtents 对某些实体会抛，忽略 */ }
                }
                tr.Commit();
            }
            return acc;
        }

        private static IEnumerable<ObjectId> FindEntitiesByHyRoadId(Transaction tr, Database db, Guid targetId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                DBObject obj;
                try { obj = tr.GetObject(id, OpenMode.ForRead); }
                catch { continue; }
                if (obj == null) continue;

                Guid gid;
                try { gid = HyRoadXdata.ReadId(tr, obj); }
                catch { continue; }

                if (gid == targetId) yield return id;
            }
        }

        private static void ZoomToExtents(Editor ed, Extents3d ext)
        {
            // AutoCAD 2020+ 原生 Editor 没有直接的 Zoom(extents) API；走命令行：_.zoom _w minx miny maxx maxy
            var min = ext.MinPoint;
            var max = ext.MaxPoint;
            string cmd = $"_.ZOOM _W {min.X} {min.Y} {max.X} {max.Y} ";
            ed.Document.SendStringToExecute(cmd, true, false, false);
        }

        private static void Tell(Document doc, string msg)
        {
            try { doc.Editor.WriteMessage("\n" + msg); }
            catch { /* 无 editor 场景吞掉 */ }
        }
    }
}
