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
using System.Linq;
using System.Text;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// N16：网格构件划分 — 基于 N15 网格分解，按尺寸+位置判楼板/底板/墙/梁/大体积/局部并着色标注。
    /// </summary>
    public sealed class MeshComponentDivideCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            SettingsPanelViewModel.CommitFocusedTextBoxValue();
            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateComponentParameters() : new ComponentParameters();
            vm?.SaveSettings();

            double groupDistanceMm = parameters.RegionGroupDistanceMm;

            ed.WriteMessage(
                $"\n[N16] 网格构件划分  分组距离={groupDistanceMm:F0}mm");

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线（N16 网格构件划分）: " },
                filter);
            if (selResult.Status != PromptStatus.OK)
                return;

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
                return;
            }

            var classified = ReinRegionBuilder.ClassifyBoundaries(rawBoundaries);
            var groups = ReinRegionBuilder.BuildGroupedIndependentRegions(classified, groupDistanceMm);
            if (groups.Count == 0)
            {
                ed.WriteMessage("\n未识别到外轮廓。");
                return;
            }

            var reinRegionList = new List<ReinRegion>();
            var allComponents = new List<ComponentRegion>();
            var sb = new StringBuilder();
            sb.AppendLine("\n── N16 网格构件划分 ──");

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                double groupMinY = ComputeGroupMinY(group);
                var groupCells = new List<MeshCell>();

                foreach (var region in group)
                {
                    if (region?.Outer == null || region.Outer.VertexCount < 3)
                        continue;

                    reinRegionList.Add(region);
                    groupCells.AddRange(RegionMeshDecomposer.Decompose(region));
                }

                var groupComponents = MeshComponentClassifier.Classify(groupCells, groupMinY, parameters);
                allComponents.AddRange(groupComponents);

                sb.AppendLine(
                    $"  G#{g}  区域={group.Count}  单元={groupCells.Count}  构件={groupComponents.Count}  {FormatTypeCounts(groupComponents)}");
            }

            if (allComponents.Count == 0)
            {
                ed.WriteMessage("\n未识别到构件区域。");
                return;
            }

            sb.AppendLine($"  合计  构件={allComponents.Count}  {FormatTypeCounts(allComponents)}");
            sb.AppendLine("  青=楼板  绿=墙体  蓝=底板  红=大体积  黄=梁  洋红=局部  N9可切换类型");

            var sessionId = Guid.NewGuid();
            if (ComponentSession.CurrentSessionId != Guid.Empty)
                ComponentPreviewService.EraseSession(ComponentSession.CurrentSessionId);

            ComponentSession.Set(sessionId, reinRegionList, allComponents);
            int drawn = ComponentPreviewService.DrawSession(sessionId, allComponents);
            sb.AppendLine($"  预览实体={drawn}  图层「{ComponentPreviewService.PreviewLayerName}」");

            ed.WriteMessage(sb.ToString());
        }

        private static double ComputeGroupMinY(IReadOnlyList<ReinRegion> group)
        {
            double minY = double.MaxValue;
            foreach (var region in group)
            {
                if (region?.Outer == null)
                    continue;

                for (int i = 0; i < region.Outer.VertexCount; i++)
                    minY = Math.Min(minY, region.Outer.GetPointAt(i).Y);
            }

            return minY == double.MaxValue ? 0 : minY;
        }

        private static string FormatTypeCounts(IEnumerable<ComponentRegion> regions)
        {
            int slab = 0, wall = 0, bottom = 0, mass = 0, beam = 0, local = 0;
            foreach (var r in regions)
            {
                switch (r.Type)
                {
                    case ComponentType.Slab: slab++; break;
                    case ComponentType.Wall: wall++; break;
                    case ComponentType.BottomSlab: bottom++; break;
                    case ComponentType.MassConcrete: mass++; break;
                    case ComponentType.Beam: beam++; break;
                    case ComponentType.LocalConcrete: local++; break;
                }
            }

            return $"楼板={slab}  墙={wall}  底板={bottom}  大体积={mass}  梁={beam}  局部={local}";
        }
    }
}
