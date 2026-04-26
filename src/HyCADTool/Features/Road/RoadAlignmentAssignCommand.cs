using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnAssign</c>：为当前路线在桩号区间上挂接标准横断面模板（<see cref="CrossSectionAssignment"/>）。
    /// </summary>
    public sealed class RoadAlignmentAssignCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取路线 Polyline (HY_ROAD Alignment)：");
            peo.SetRejectMessage("\n[道路] 仅 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            Alignment alignment;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    ed.WriteMessage("\n[道路] 无效图元。");
                    return;
                }
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取对象不是 Alignment。");
                    return;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (!registry.TryGet(doc.Name, out var design) || design == null)
                {
                    ed.WriteMessage("\n[道路] 无道路设计数据。");
                    return;
                }
                alignment = design.Alignments.FirstOrDefault(a => a.Id == id);
                if (alignment == null || alignment.Centerline == null)
                {
                    ed.WriteMessage("\n[道路] 未找到线位或中心线为空。");
                    return;
                }
                tr.Commit();
            }

            if (alignment.Centerline.VertexCount < 2)
            {
                ed.WriteMessage("\n[道路] 中心线不足 2 点。");
                return;
            }

            if (!registry.TryGet(doc.Name, out var d2) || d2.Templates.Count == 0)
            {
                ed.WriteMessage("\n[道路] 当前无横断面模板。请先 hyRoadCs 建模板。");
                return;
            }

            ed.WriteMessage("\n[道路] 可用模板：");
            for (int i = 0; i < d2.Templates.Count; i++)
            {
                var t = d2.Templates[i];
                ed.WriteMessage($"\n  {i + 1}: {t.Name ?? "未命名"}  Id={t.Id:N}");
            }

            var oi = new PromptIntegerOptions("\n[道路] 选模板序号：")
            {
                AllowNegative = false, AllowZero = false,
                LowerLimit = 1, UpperLimit = d2.Templates.Count,
            };
            var ri = ed.GetInteger(oi);
            if (ri.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }
            var tpl = d2.Templates[ri.Value - 1];
            double total = alignment.Centerline.GetPlanarLength();

            var oStart = new PromptDoubleOptions($"\n[道路] 区段起点桩号 (0 ~ {total:F3} m) <0>：")
            {
                DefaultValue = 0, UseDefaultValue = true, AllowNegative = false, AllowNone = true,
            };
            var rS = ed.GetDouble(oStart);
            double s0 = rS.Status == PromptStatus.OK ? rS.Value : 0;

            var oEnd = new PromptDoubleOptions($"\n[道路] 区段终点桩号 (0 ~ {total:F3} m) <{total:F3}>：")
            {
                DefaultValue = total, UseDefaultValue = true, AllowNegative = false, AllowNone = true,
            };
            var rE = ed.GetDouble(oEnd);
            double s1 = rE.Status == PromptStatus.OK ? rE.Value : total;

            var assign = new CrossSectionAssignment
            {
                TemplateId = tpl.Id,
                StartStation = s0,
                EndStation = s1,
            };
            var v = assign.Validate(total);
            if (!v.Ok)
            {
                ed.WriteMessage($"\n[道路] 区段非法：{v.Error}");
                return;
            }

            // 与已有区段不得重叠（硬规则）
            foreach (var ex in alignment.CrossSectionAssignments)
            {
                if (ex == null) continue;
                double loE = Math.Min(ex.StartStation, ex.EndStation);
                double hiE = Math.Max(ex.StartStation, ex.EndStation);
                double loN = Math.Min(assign.StartStation, assign.EndStation);
                double hiN = Math.Max(assign.StartStation, assign.EndStation);
                if (hiN >= loE - 1e-4 && loN <= hiE + 1e-4)
                {
                    ed.WriteMessage($"\n[道路] 与已有区段 {loE:F3}~{hiE:F3} 重叠，请先删除旧区段或调整范围。");
                    return;
                }
            }

            alignment.CrossSectionAssignments.Add(assign);
            alignment.CrossSectionAssignments.Sort((a, b) =>
            {
                double la = Math.Min(a.StartStation, a.EndStation);
                double lb = Math.Min(b.StartStation, b.EndStation);
                return la.CompareTo(lb);
            });

            d2.LastModifiedUtc = DateTime.UtcNow;
            var path = exporter.SaveForDocument(d2, doc.Name);
            ed.WriteMessage(path != null
                ? $"\n[道路] 已挂接模板「{tpl.Name}」，落盘 {path}"
                : "\n[道路] 已挂接（未写盘，请先保存 DWG）。");
        }
    }
}
