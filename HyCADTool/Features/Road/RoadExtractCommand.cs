using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadExtract</c>（M8.1 + M8.2 聚合命令）：从 AutoCAD 闭合 Polyline 反解横断面条带参数。
    ///
    /// <para><b>工作流</b></para>
    /// <list type="number">
    ///   <item>命令行选择板块类型（机动车道 / 非机动车道 / 人行道 / 绿化带 / 立缘石 / 结构层）。</item>
    ///   <item>用 <c>SELECTONSCREEN</c> 让用户拾取一条闭合 Polyline。</item>
    ///   <item>调 <see cref="PolylineToBandExtractor.TryExtract"/> 反解出 <see cref="CrossSectionBand"/>。</item>
    ///   <item>用 <see cref="RoadAlignmentPreviewService"/>（黄色 ColorIndex=2）绘制瞬态预览。</item>
    ///   <item>命令行确认 <c>Y</c>：写入 Domain + 创建还原点（描述如 "从图形提取_绿化带"）+ 落盘 JSON。</item>
    ///   <item><c>N</c>：仅预览，不写入，清除瞬态。</item>
    /// </list>
    /// </summary>
    public sealed class RoadExtractCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            // 1) 选类型
            var kind = PromptKind(ed);
            if (kind == null)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            // 2) 选边
            var side = PromptSide(ed);
            if (side == null) return;

            // 3) 取名字
            var name = PromptName(ed, kind.Value);

            // 4) 拾取 Polyline
            var peo = new PromptEntityOptions("\n[道路] 拾取要反解的闭合 Polyline：");
            peo.SetRejectMessage("\n[道路] 只允许 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            // 5) 读取几何 → Domain Polyline3D
            HyCADTool.Shared.Geometry.Polyline3D domainPoly;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (ent == null)
                {
                    ed.WriteMessage("\n[道路] 拾取的不是 Polyline。");
                    return;
                }
                if (!ent.Closed)
                {
                    ed.WriteMessage("\n[道路] Polyline 必须闭合（右键或 PEDIT → Close）。");
                    return;
                }
                domainPoly = RoadGeometryBridge.ToDomain(ent);
                tr.Commit();
            }

            // 6) 反解
            if (!PolylineToBandExtractor.TryExtract(domainPoly, kind.Value, side.Value, name, out var band, out var error))
            {
                ed.WriteMessage($"\n[道路] 反解失败：{error}");
                return;
            }

            ed.WriteMessage(
                $"\n[道路] 反解成功："
                + $"\n  · 名称：{band.Name}"
                + $"\n  · 类型：{band.Kind}  侧别：{band.Side}"
                + $"\n  · 宽度：{band.Width:F3} m"
                + $"\n  · 横坡：{band.CrossSlopePct:F2} %");

            // 7) 黄线瞬态预览 + 命令行确认
            using (var preview = new RoadAlignmentPreviewService { ColorIndex = 2 /* Yellow */ })
            {
                preview.Update(domainPoly);

                var opts = new PromptKeywordOptions("\n[道路] 写入模型？")
                {
                    AllowNone = true,
                };
                opts.Keywords.Add("Yes");
                opts.Keywords.Add("No");
                opts.Keywords.Default = "Yes";
                var pkr = ed.GetKeywords(opts);
                if (pkr.Status != PromptStatus.OK || pkr.StringResult != "Yes")
                {
                    ed.WriteMessage("\n[道路] 已取消（未写入）。");
                    return;
                }
            }

            // 8) 写入 Domain + JSON 落盘 + 还原点
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var history = ServiceLocator.Resolve<HistoryService>();

            var design = registry.GetOrCreate(doc.Name);
            var templateId = PolylineToBandExtractor.ApplyToDesign(design, band);
            design.LastModifiedUtc = DateTime.UtcNow;

            // 还原点（M8.4）：描述遵循 01MASTER 的"从图形提取_{类型}_{名字}"约定
            string historyDir = HistoryService.GetHistoryDirectory(doc.Name);
            if (!string.IsNullOrWhiteSpace(historyDir))
            {
                try
                {
                    history.CreateSnapshot(historyDir, design, $"从图形提取_{band.Kind}_{band.Name}");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 还原点创建失败（不影响主流程）：{ex.Message}");
                }
            }

            string savedTo = exporter.SaveForDocument(design, doc.Name);
            if (savedTo != null)
                ed.WriteMessage($"\n[道路] 已写入 Template {templateId:N}，JSON 落盘：{savedTo}");
            else
                ed.WriteMessage($"\n[道路] 已写入 Template {templateId:N}（DWG 尚未保存，未落盘）。");
        }

        private static TemplateComponentKind? PromptKind(Editor ed)
        {
            var opts = new PromptKeywordOptions(
                "\n[道路] 选择板块类型 [机动车道(P)/非机动车道(N)/人行道(S)/绿化带(G)/立缘石(K)/结构层(L)]:")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Pavement");
            opts.Keywords.Add("NonMotor");
            opts.Keywords.Add("Sidewalk");
            opts.Keywords.Add("Green");
            opts.Keywords.Add("Kerb");
            opts.Keywords.Add("Layer");
            opts.Keywords.Default = "Pavement";

            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return null;
            switch (r.StringResult)
            {
                case "Pavement": return TemplateComponentKind.Pavement;
                case "NonMotor": return TemplateComponentKind.NonMotorized;
                case "Sidewalk": return TemplateComponentKind.Sidewalk;
                case "Green": return TemplateComponentKind.GreenStrip;
                case "Kerb": return TemplateComponentKind.Kerb;
                case "Layer": return TemplateComponentKind.Pavement; // 结构层附加到机动车道
                default: return TemplateComponentKind.Pavement;
            }
        }

        private static BandSide? PromptSide(Editor ed)
        {
            var opts = new PromptKeywordOptions("\n[道路] 条带侧别 [左(L)/右(R)/中心(C)]: ")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("Left");
            opts.Keywords.Add("Right");
            opts.Keywords.Add("Center");
            opts.Keywords.Default = "Left";
            var r = ed.GetKeywords(opts);
            if (r.Status != PromptStatus.OK) return null;
            switch (r.StringResult)
            {
                case "Right": return BandSide.Right;
                case "Center": return BandSide.Center;
                default: return BandSide.Left;
            }
        }

        private static string PromptName(Editor ed, TemplateComponentKind kind)
        {
            string def = kind.ToString();
            var opts = new PromptStringOptions($"\n[道路] 条带名称 <{def}>: ")
            {
                AllowSpaces = true,
                DefaultValue = def,
                UseDefaultValue = true,
            };
            var r = ed.GetString(opts);
            return r.Status == PromptStatus.OK ? (string.IsNullOrWhiteSpace(r.StringResult) ? def : r.StringResult.Trim()) : def;
        }
    }
}
