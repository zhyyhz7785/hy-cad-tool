using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// Alignment PI 类命令（<c>hyRoadAlnInsertPi</c> / <c>hyRoadAlnDeletePi</c>）的公共管线。
    ///
    /// 这些命令的形状都是：
    /// 1) 拾取 DWG 上一条 HY_ROAD Alignment Polyline；
    /// 2) 取出 <see cref="AlignmentSource.PiElements"/> 作为工作副本；
    /// 3) 修改 PI 表（插入 / 删除 / 改参数）；
    /// 4) <see cref="AlignmentPiDesigner.Build"/> 重建几何；
    /// 5) <see cref="RoadAlignmentService.RebuildCenterline"/> 替换 DWG Polyline 几何；
    /// 6) 覆盖 <see cref="Alignment.Source"/> 并让 <see cref="RoadJsonExportService"/> 落盘；
    /// 7) 跑 <see cref="Alignment.Validate"/> 报告自检。
    ///
    /// 本类把 (1/2) 和 (4-7) 的共用代码集中到一处，各命令只负责 (3) 的差异部分。
    /// </summary>
    internal static class RoadAlignmentPiPipeline
    {
        /// <summary>
        /// 拾取 Alignment Polyline，把 Domain 对象与 PI 表工作副本回出。
        /// 失败时会在命令行打印原因并返回 false。
        /// </summary>
        public static bool PickAlignment(
            Document doc,
            out Alignment alignment,
            out List<PiElement> elements,
            out ObjectId pickedPolylineId)
        {
            alignment = null;
            elements = null;
            pickedPolylineId = ObjectId.Null;

            if (doc == null) return false;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取要修改的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return false;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                pickedPolylineId = per.ObjectId;
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元没有 HY_ROAD Alignment 标识。请先用 hyRoadAlnByPi 重建。");
                    return false;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty)
                {
                    ed.WriteMessage("\n[道路] 拾取的图元缺少 HY_ROAD/ID。");
                    return false;
                }
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据，请先跑 hyRoadAlnByPi。");
                    return false;
                }
                var a = design.Alignments.FirstOrDefault(x => x.Id == id);
                if (a == null)
                {
                    ed.WriteMessage($"\n[道路] Domain 里找不到 AlignmentId={id:N} 的数据，可能是 JSON 未加载。");
                    return false;
                }
                if (a.Source == null || a.Source.PiElements == null || a.Source.PiElements.Count < 2)
                {
                    ed.WriteMessage(
                        "\n[道路] 该平面线位不是按 PI 创建（或老 JSON 无 PI 表快照）。"
                        + "\n[道路] 请先跑 hyRoadAlnByPi 重建。");
                    return false;
                }

                alignment = a;
                elements = a.Source.PiElements
                    .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                    .ToList();

                tr.Commit();
            }

            return true;
        }

        /// <summary>
        /// 统一尾部：Build → RebuildCenterline → 更新 Source → JSON → Validate。
        /// 任何一步失败都把错误回显到命令行，并返回 false。
        /// </summary>
        public static bool RebuildAndPersist(
            Document doc,
            Alignment alignment,
            IReadOnlyList<PiElement> elements,
            string successHeader)
        {
            if (doc == null) return false;
            if (alignment == null) return false;
            if (elements == null || elements.Count < 2) return false;

            var ed = doc.Editor;
            var db = doc.Database;

            PiDesignResult result;
            try
            {
                result = AlignmentPiDesigner.Build(elements, new PiDesignOptions());
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 几何重建失败：{ex.Message}");
                return false;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            bool rebuilt = false;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    rebuilt = svc.RebuildCenterline(doc.Name, tr, db, alignment.Id, result.Polyline);
                    if (rebuilt)
                    {
                        alignment.Source = BuildSource(elements);
                    }
                    tr.Commit();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    ed.WriteMessage(
                        $"\n[道路] DWG Polyline 更新失败：{ex.Message}（ErrorStatus={ex.ErrorStatus}）。");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] DWG Polyline 更新失败：{ex.Message}");
                }
            }

            if (!rebuilt)
            {
                ed.WriteMessage("\n[道路] DWG 中找不到同 AlignmentId 的 Polyline，已取消（考虑先跑 hyRoadLoad 回写）。");
                return false;
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            if (!string.IsNullOrEmpty(successHeader)) ed.WriteMessage("\n[道路] " + successHeader);
            ed.WriteMessage(
                $"\n[道路] 重建后 PI={elements.Count}, 顶点={alignment.Centerline.VertexCount}, "
                + $"圆角={result.CurvedPiCount}, 缓和={result.SpiraledPiCount}, "
                + $"跳过={result.SkippedCount}, 平面长度={alignment.Centerline.GetPlanarLength():F3} m。");

            foreach (var w in result.Warnings) ed.WriteMessage("\n[道路] ⚠ " + w);

            var v = alignment.Validate();
            if (!v.Ok)
            {
                ed.WriteMessage("\n[道路] ⚠ 自检未通过：");
                foreach (var e in v.Errors) ed.WriteMessage("\n  · " + e);
            }

            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS，再跑 hyRoadSave 即可。");

            return true;
        }

        public static void PrintPiTable(Editor ed, IReadOnlyList<PiElement> elements)
        {
            ed.WriteMessage($"\n[道路] 当前 PI 表（共 {elements.Count} 个）：");
            ed.WriteMessage("\n  idx  X          Y          R       Ls_in   Ls_out  Tag");
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                string tag = string.IsNullOrEmpty(e.Tag) ? "-" : e.Tag;
                string mark = (i == 0 || i == elements.Count - 1) ? "*" : " ";
                ed.WriteMessage(
                    $"\n  {mark}{i,-3} {e.P.X,-10:F3} {e.P.Y,-10:F3} {e.Radius,-7:F2} {e.SpiralIn,-7:F2} {e.SpiralOut,-7:F2} {tag}");
            }
            ed.WriteMessage("\n  (* = 端点 PI)");
        }

        private static AlignmentSource BuildSource(IReadOnlyList<PiElement> elements)
        {
            var s = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            foreach (var e in elements)
            {
                s.PiElements.Add(new AlignmentPiInput
                {
                    P = e.P,
                    Radius = e.Radius,
                    SpiralIn = e.SpiralIn,
                    SpiralOut = e.SpiralOut,
                    Tag = e.Tag,
                });
            }
            return s;
        }
    }
}
