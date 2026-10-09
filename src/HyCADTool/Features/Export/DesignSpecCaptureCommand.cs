using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DesignSpec.Services;
using Newtonsoft.Json.Linq;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// hymdc：循环选择文字/表格，经管道发给 HyMd 转成 Markdown 并追加。
    /// </summary>
    public class DesignSpecCaptureCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;
            var ed = doc.Editor;

            HyMdCadBridge.Start();
            if (!HyMdCadBridge.EnsureProcess(ed))
                return;

            string dwgPath = null;
            try { dwgPath = doc.Name; } catch { }
            double scale = HyMdCadBridge.LastScale > 0 ? HyMdCadBridge.LastScale : 1.0;
            string styleName = HyMdCadBridge.LastStyleName;
            HyMdCadBridge.SendBindAndContext(dwgPath, scale, styleName);

            string sessionId = Guid.NewGuid().ToString("D");
            HyMdCadBridge.SendJson(new JObject
            {
                ["v"] = 1,
                ["op"] = "hymdcBegin",
                ["sessionId"] = sessionId,
                ["dwgPath"] = dwgPath ?? "",
                ["scale"] = scale,
                ["styleName"] = styleName ?? "0-hy-说明-T"
            });
            ed.WriteMessage("\nhymdc：选择文字/表格，回车发送一批；Esc 结束。");

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,POLYLINE,TEXT,MTEXT,ACAD_TABLE"),
            });
            int seq = 0;
            try
            {
                while (true)
                {
                    var pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n选择文字/表格（Esc 结束）",
                    };
                    var psr = ed.GetSelection(pso, filter);
                    if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                        break;

                    JObject snippet;
                    using (doc.LockDocument())
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        snippet = CadImportSnippetBuilder.Build(tr, doc.Database, ed, psr.Value, scale);
                        tr.Commit();
                    }

                    var items = snippet?["items"] as JArray;
                    if (items == null || items.Count == 0)
                    {
                        ed.WriteMessage("\n本批没有可导入的文字或表格。");
                        continue;
                    }

                    seq++;
                    HyMdCadBridge.SendJson(new JObject
                    {
                        ["v"] = 1,
                        ["op"] = "hymdcAppend",
                        ["sessionId"] = sessionId,
                        ["seq"] = seq,
                        ["snippet"] = snippet
                    });
                    ed.WriteMessage($"\n已发送第 {seq} 批（{items.Count} 项）。");
                }
            }
            finally
            {
                HyMdCadBridge.SendJson(new JObject
                {
                    ["v"] = 1,
                    ["op"] = "hymdcEnd",
                    ["sessionId"] = sessionId
                });
                ed.WriteMessage("\nhymdc 结束。");
            }
        }
    }
}
