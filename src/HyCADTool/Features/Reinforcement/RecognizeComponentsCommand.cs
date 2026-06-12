using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Features.Reinforcement.Services;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.Shell.ViewModels;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 识别截面构件并在预览图层着色（hyCompRec）。
    /// </summary>
    public sealed class RecognizeComponentsCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateComponentParameters() : new ComponentParameters();

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线: " },
                filter);
            if (selResult.Status != PromptStatus.OK) return;

            var rawBoundaries = new List<Polyline2D>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var oid in selResult.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(oid, OpenMode.ForRead) as Polyline;
                    if (entity == null) continue;
                    var boundary = entity.ToDomainPolyline();
                    boundary.RemoveDuplicateVertices();
                    boundary.IsClosed = true;
                    rawBoundaries.Add(boundary);
                }
                tr.Commit();
            }

            if (rawBoundaries.Count == 0)
            {
                ed.WriteMessage("\n未找到有效闭合多段线。");
                return;
            }

            var classified = ReinRegionBuilder.ClassifyBoundaries(rawBoundaries);
            var reinRegions = ReinRegionBuilder.BuildRegions(classified);
            if (reinRegions.Count == 0)
            {
                ed.WriteMessage("\n未找到有效外轮廓。");
                return;
            }

            var reinRegionList = new List<ReinRegion>();
            foreach (var (region, _) in reinRegions)
                reinRegionList.Add(region);

            var allComponents = ComponentRecognizer.Recognize(reinRegionList, parameters);

            if (allComponents.Count == 0)
            {
                ed.WriteMessage("\n未识别到构件区域，请调整识别参数。");
                return;
            }

            var sessionId = Guid.NewGuid();
            if (ComponentSession.CurrentSessionId != Guid.Empty)
                ComponentPreviewService.EraseSession(ComponentSession.CurrentSessionId);

            ComponentSession.Set(sessionId, reinRegionList, allComponents);
            int drawn = ComponentPreviewService.DrawSession(sessionId, allComponents);
            ed.WriteMessage($"\n构件识别完成：{allComponents.Count} 个区域，预览 {drawn} 个实体。");
        }
    }
}
