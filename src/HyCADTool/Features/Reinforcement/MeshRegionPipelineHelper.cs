using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.Shell.ViewModels;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement
{
    internal sealed class MeshRegionPipelineContext
    {
        public ComponentParameters Parameters { get; set; }
        public double GroupDistanceMm { get; set; }
        public List<List<ReinRegion>> Groups { get; set; }
        public Editor Editor { get; set; }
    }

    internal static class MeshRegionPipelineHelper
    {
        public static MeshRegionPipelineContext TrySelectAndGroup(string promptMessage)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return null;

            var db = doc.Database;
            var ed = doc.Editor;

            SettingsPanelViewModel.CommitFocusedTextBoxValue();
            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateComponentParameters() : new ComponentParameters();
            vm?.SaveSettings();

            double groupDistanceMm = parameters.RegionGroupDistanceMm;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = promptMessage },
                filter);
            if (selResult.Status != PromptStatus.OK)
                return null;

            var rawBoundaries = new List<Polyline2D>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var oid in selResult.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(oid, OpenMode.ForRead) as Polyline;
                    if (entity == null)
                        continue;

                    var boundary = entity.ToDomainPolyline();
                    boundary.RemoveDuplicateVertices();
                    boundary.IsClosed = true;
                    rawBoundaries.Add(boundary);
                }

                tr.Commit();
            }

            if (rawBoundaries.Count == 0)
            {
                ed.WriteMessage("\n未找到有效多段线。");
                return null;
            }

            var classified = ReinRegionBuilder.ClassifyBoundaries(rawBoundaries);
            var groups = ReinRegionBuilder.BuildGroupedIndependentRegions(classified, groupDistanceMm);
            if (groups.Count == 0)
            {
                ed.WriteMessage("\n未识别到外轮廓。");
                return null;
            }

            return new MeshRegionPipelineContext
            {
                Parameters = parameters,
                GroupDistanceMm = groupDistanceMm,
                Groups = groups,
                Editor = ed
            };
        }

        public static double ComputeGroupMinY(IReadOnlyList<ReinRegion> group)
        {
            double minY = double.MaxValue;
            foreach (var region in group)
            {
                if (region?.Outer == null)
                    continue;

                for (int i = 0; i < region.Outer.VertexCount; i++)
                    minY = System.Math.Min(minY, region.Outer.GetPointAt(i).Y);
            }

            return minY == double.MaxValue ? 0 : minY;
        }
    }
}
