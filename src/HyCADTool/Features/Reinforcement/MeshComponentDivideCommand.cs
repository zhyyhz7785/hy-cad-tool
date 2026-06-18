using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Features.Reinforcement.Services;
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
            => RunClassify(MeshClassifyStage.Complete, "N16", "网格构件划分", isInitial: false, isUpperSplit: false, useBoundaryProfile: false);

        /// <summary>N22：构件初判（ClassifyRectangle，无 Slab 精修）。</summary>
        public void ExecuteInitialClassify()
            => RunClassify(MeshClassifyStage.Initial, "N22", "网格构件初判", isInitial: true, isUpperSplit: false, useBoundaryProfile: false);

        /// <summary>N25：N22 初判 + 上下皆实横条按上部结构 X 断点打断分段。</summary>
        public void ExecuteInitialClassifyUpperSplit()
            => RunClassify(
                MeshClassifyStage.InitialUpperSplit,
                "N25",
                "网格构件初判+上部X打断",
                isInitial: true,
                isUpperSplit: true,
                useBoundaryProfile: false);

        /// <summary>N26：5 级简化判型 + 土/气边界接触。</summary>
        public void ExecuteSimpleClassify()
            => RunClassify(
                MeshClassifyStage.InitialSimple,
                "N26",
                "网格构件简化判型",
                isInitial: true,
                isUpperSplit: false,
                useBoundaryProfile: true);

        /// <summary>N27：N21 网格 + 仅底板判型（土接触）。</summary>
        public void ExecuteBottomSlabClassify()
            => RunClassify(
                MeshClassifyStage.BottomSlabOnly,
                "N27",
                "网格底板判型",
                isInitial: true,
                isUpperSplit: false,
                useBoundaryProfile: true);

        /// <summary>N28：N21 网格 + 仅底板判型(清版，下侧土接触不限标高)。</summary>
        public void ExecuteBottomSlabClassifyV2()
            => RunClassify(
                MeshClassifyStage.BottomSlabOnlyV2,
                "N28",
                "网格底板判型V2",
                isInitial: true,
                isUpperSplit: false,
                useBoundaryProfile: true);

        /// <summary>N29：N28 底板 + 楼板判型(横条+h≤300+上下皆气接触→楼板)。</summary>
        public void ExecuteBottomSlabAndSlabClassifyV3()
            => RunClassify(
                MeshClassifyStage.BottomSlabAndSlabV3,
                "N29",
                "网格底板+楼板判型V3",
                isInitial: true,
                isUpperSplit: false,
                useBoundaryProfile: true);

        private static void RunClassify(
            MeshClassifyStage stage,
            string commandTag,
            string stageTitle,
            bool isInitial,
            bool isUpperSplit,
            bool useBoundaryProfile)
        {
            var ctx = MeshRegionPipelineHelper.TrySelectAndGroup(
                $"\n选择混凝土边界多段线（{commandTag} {stageTitle}）: ");
            if (ctx == null)
                return;

            var ed = ctx.Editor;
            ed.WriteMessage(
                $"\n[{commandTag}] {stageTitle}  分组距离={ctx.GroupDistanceMm:F0}mm");

            List<GroupBoundaryProfile> groupProfiles = null;
            if (useBoundaryProfile)
            {
                SettingsPanelViewModel.CommitFocusedTextBoxValue();
                var vm = SettingsPanelViewModel.Current;
                double foundationElevation = vm?.CompFoundationBottomElevation ?? ctx.Parameters.FoundationBottomElevationMm;
                double embedmentDepth = vm?.CompEmbedmentDepth ?? ctx.Parameters.EmbedmentDepthMm;
                groupProfiles = RegionBoundaryAnalyzer.AnalyzeAllGroups(
                    ctx.Groups, embedmentDepth, foundationElevation);
                ed.WriteMessage(
                    $"\n  土气边界：埋深={embedmentDepth:F2}m  基础底标={foundationElevation:F2}m");
            }

            var reinRegionList = new List<ReinRegion>();
            var allComponents = new List<ComponentRegion>();
            var sb = new StringBuilder();
            sb.AppendLine($"\n── {commandTag} {stageTitle} ──");

            for (int g = 0; g < ctx.Groups.Count; g++)
            {
                var group = ctx.Groups[g];
                double groupMinY = MeshRegionPipelineHelper.ComputeGroupMinY(group);
                var groupCells = new List<MeshCell>();

                foreach (var region in group)
                {
                    if (region?.Outer == null || region.Outer.VertexCount < 3)
                        continue;

                    reinRegionList.Add(region);
                    groupCells.AddRange(RegionMeshDecomposer.DecomposeToStage(
                        region, RegionMeshDecomposeStage.Complete));
                }

                var groupComponents = MeshComponentClassifier.Classify(
                    groupCells,
                    group,
                    groupMinY,
                    ctx.Parameters,
                    stage,
                    useBoundaryProfile && groupProfiles != null && g < groupProfiles.Count
                        ? groupProfiles[g]
                        : null);
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
            if (isUpperSplit)
                sb.AppendLine("  上下皆实之横条已按上部结构 X 断点打断分段（侧段保留）；对比 N22 看差异");
            else if (stage == MeshClassifyStage.InitialSimple)
                sb.AppendLine("  5级简化：底板(土)/楼板(上下气)/墙/梁(上板下气)/大体积；无局部混凝土；对比 N13/N22");
            else if (stage == MeshClassifyStage.BottomSlabOnly)
                sb.AppendLine("  仅底板：横条+贴组底+h≤1500+下侧土接触→蓝；其余红；上游=N21网格");
            else if (stage == MeshClassifyStage.BottomSlabOnlyV2)
                sb.AppendLine("  仅底板(清版)：横条+h≤1500+下侧土接触→蓝(不限标高)；其余红；上游=N21网格");
            else if (stage == MeshClassifyStage.BottomSlabAndSlabV3)
                sb.AppendLine("  底板+楼板：横条+h≤1500+下侧土→蓝；横条+h≤300+上下气段重叠(底面探针)→青；其余红；上游=N21网格");
            else if (isInitial)
                sb.AppendLine("  局部混凝土需 N16 精修后显现；下一步执行 N16");

            var sessionId = Guid.NewGuid();
            if (ComponentSession.CurrentSessionId != Guid.Empty)
                ComponentPreviewService.EraseSession(ComponentSession.CurrentSessionId);

            ComponentSession.Set(sessionId, reinRegionList, allComponents);
            int drawn = ComponentPreviewService.DrawSession(sessionId, allComponents);
            sb.AppendLine($"  预览实体={drawn}  图层「{ComponentPreviewService.PreviewLayerName}」");

            ed.WriteMessage(sb.ToString());
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
