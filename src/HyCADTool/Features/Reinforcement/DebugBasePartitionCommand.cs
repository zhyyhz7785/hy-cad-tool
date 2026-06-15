using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Features.Reinforcement.Services;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.Shell.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 阶段 B 续 C1：土气分界 + 基础/底板分区（每组 O*_g，S 落地门控）。
    /// </summary>
    public sealed class DebugBasePartitionCommand
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
            double foundationElevation = parameters.FoundationBottomElevationMm;
            double embedmentDepth = parameters.EmbedmentDepthMm;
            double bottomSlabMaxMm = vm?.CompBottomSlabMaxThickness ?? parameters.BottomSlabMaxThicknessMm;
            var partitionOptions = new BasePartitionOptions
            {
                BottomSlabMaxMm = bottomSlabMaxMm,
                MarchStepMm = vm?.CompBottomSlabMarchStep ?? parameters.BottomSlabMarchStepMm,
                WallMaxMm = parameters.WallMaxThicknessMm,
                MassMinMm = parameters.MassConcreteMinSizeMm
            };

            ed.WriteMessage(
                $"\n[基础分区] 埋深={embedmentDepth:F2}m  底板上限={bottomSlabMaxMm:F0}mm  " +
                $"步长={partitionOptions.EffectiveMarchStepMm:F0}mm  " +
                $"(S弧步进, 第1/2交点皆S跳过, 第2非S→TopSeg, H>h_b跳过, 人工顶边分类)");

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });

            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界（基础底板分区 C1）: " },
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

            var groupProfiles = RegionBoundaryAnalyzer.AnalyzeAllGroups(
                groups, embedmentDepth, foundationElevation);

            var partitionResults = GroupBasePartitioner.PartitionAllGroups(
                groups, groupProfiles, partitionOptions);

            ReportToCommandLine(ed, groups, groupProfiles, partitionResults, partitionOptions);

            int drawn = BasePartitionPreviewService.Draw(groupProfiles, partitionResults);
            ed.WriteMessage(
                $"\n基础分区预览：{drawn} 个实体，图层「{BasePartitionPreviewService.DebugLayerName}」。" +
                $"\n  蓝=底板  绿=墙  红=大体积  黄虚=土气割线 cutY  标注 t=厚度(mm)");
        }

        private static void ReportToCommandLine(
            Editor ed,
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            IReadOnlyList<GroupBoundaryProfile> groupProfiles,
            IReadOnlyList<GroupBasePartitionResult> partitionResults,
            BasePartitionOptions partitionOptions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n── 基础底板分区（C1）──");
            sb.AppendLine(
                $"  计算组={groups.Count}  底板上限={partitionOptions.BottomSlabMaxMm:F0}mm  " +
                $"步长={partitionOptions.EffectiveMarchStepMm:F0}mm  " +
                $"墙厚≤{partitionOptions.WallMaxMm:F0}  大体积≥{partitionOptions.MassMinMm:F0}");

            for (int g = 0; g < partitionResults.Count; g++)
            {
                var pr = partitionResults[g];
                var gp = g < groupProfiles.Count ? groupProfiles[g] : null;
                sb.AppendLine(
                    $"  G#{g}  基础=R{pr.FoundationRegionIndex}  cutY={pr.CutLineY:F1}mm  " +
                    $"底板cell={pr.BottomCells?.Count ?? 0}  合并多边形={pr.BasePolygons?.Count ?? 0}");

                if (pr.StripDiagnostics == null)
                    continue;

                foreach (var d in pr.StripDiagnostics)
                {
                    string tag = !string.IsNullOrEmpty(d.ProbeTag)
                        ? d.ProbeTag
                        : (d.HasBottomSlab ? "底板" : (d.SkippedTooThick ? "超厚跳过" : (d.IsGrounded ? "无cell" : "非S")));
                    string kindText = d.HasBottomSlab ? d.Kind.DisplayName() : "-";
                    string syntheticText = d.IsTopSynthetic
                        ? $"  人工边={d.SyntheticTopLengthMm:F0}"
                        : string.Empty;
                    sb.AppendLine(
                        $"    τ{d.StripIndex} x=[{d.X0:F0},{d.X1:F0}]  S={d.IsSoilContact}  " +
                        $"H={d.CandidateHeightMm:F0}  cut={d.CutY:F0}  t={d.ThicknessMm:F0}  " +
                        $"斜={d.IsSloped}  上限={d.EffectiveMaxHeightMm:F0}  " +
                        $"S-S跳过={d.SkippedSoilSoil}  TopSeg={d.TriggeredNonSoilTop}  " +
                        $"超厚={d.SkippedTooThick}  型={kindText}{syntheticText}  ({tag})");
                }

                if (pr.BottomCells != null)
                {
                    foreach (var cell in pr.BottomCells.Where(c => c != null && c.IsTopSynthetic))
                    {
                        sb.AppendLine(
                            $"    cell#{cell.Id}  人工顶边={cell.SyntheticTopLengthMm:F0}mm  " +
                            $"型={cell.Kind.DisplayName()}  x=[{cell.X0:F0},{cell.X1:F0}]");
                    }
                }
            }

            ed.WriteMessage(sb.ToString());
        }
    }
}
