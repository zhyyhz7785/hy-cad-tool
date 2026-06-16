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
    /// N13：土气划分 — 独立图形分组 + 割线土/气分析，命令行报告并可视化。
    /// 逻辑复制自 DebugRegionBoundaryCommand（阶段 B）。
    /// </summary>
    public sealed class SoilAirBoundaryCommand
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
            double foundationElevation = vm?.CompFoundationBottomElevation ?? parameters.FoundationBottomElevationMm;
            double embedmentDepth = vm?.CompEmbedmentDepth ?? parameters.EmbedmentDepthMm;

            ed.WriteMessage(
                $"\n[N13] 土气划分  埋置深度={embedmentDepth:F2}m  基础底面标高={foundationElevation:F2}m  " +
                $"(割线=组ymin+{embedmentDepth:F2}m×1000)");

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线（N13 土气划分）: " },
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
                ed.WriteMessage("\n未识别到外轮廓（请确认选中闭合 LWPOLYLINE 且非纯孔洞）。");
                return;
            }

            var groupProfiles = RegionBoundaryAnalyzer.AnalyzeAllGroups(
                groups, embedmentDepth, foundationElevation);

            ReportToCommandLine(ed, groups, groupProfiles, foundationElevation, embedmentDepth);

            int drawn = BoundaryProfilePreviewService.Draw(groups, groupProfiles);
            ed.WriteMessage(
                $"\n[N13] 土气划分预览：{drawn} 个实体，图层「{BoundaryProfilePreviewService.DebugLayerName}」。" +
                $"\n  绿=土壤(基础图形 CCW弧)  橙=空气  组内其余独立图形外轮廓全橙  灰=孔洞  黄虚=割线");
        }

        private static void ReportToCommandLine(
            Editor ed,
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            IReadOnlyList<GroupBoundaryProfile> groupProfiles,
            double foundationElevation,
            double embedmentDepth)
        {
            var sb = new StringBuilder();

            sb.AppendLine("\n── N13 土气划分 ──");
            if (RegionElevationContext.IsFoundationElevationValid(foundationElevation))
                sb.AppendLine($"  基础底面标高={foundationElevation:F2}m（上报）  埋置深度={embedmentDepth:F2}m");
            else
                sb.AppendLine($"  基础底面标高=未填  埋置深度={embedmentDepth:F2}m");
            sb.AppendLine(
                $"  计算组={groups.Count}  独立图形={groups.Sum(g => g.Count)}  落地组=G#{FindPrimaryGroupIndex(groupProfiles)}");

            for (int g = 0; g < groups.Count && g < groupProfiles.Count; g++)
            {
                var profile = groupProfiles[g];
                string groupTag = profile.IsPrimarySoilGroup ? "落地" : "组";
                sb.AppendLine(
                    $"  G#{g}({groupTag})  组ymin={profile.GroupMinY:F1}mm  基础图形=R{profile.FoundationGraphicIndex}  " +
                    $"独立图形={profile.Regions?.Count ?? 0}");

                if (!double.IsNaN(profile.CutY))
                {
                    sb.AppendLine(
                        $"      割线 cutY={profile.CutY:F1}mm (=ymin+{embedmentDepth:F2}m×1000)  " +
                        $"交点X=[{profile.CutIntersectionMinX:F1}, {profile.CutIntersectionMaxX:F1}]");
                }

                if (profile.Regions == null)
                    continue;

                for (int r = 0; r < profile.Regions.Count; r++)
                {
                    var rp = profile.Regions[r];
                    string roleTag = rp.IsFoundationGraphic ? "基础" : "全空气";
                    sb.AppendLine(
                        $"    G{g}-R{r}({roleTag})  ymin={rp.GraphicMinY:F2}  " +
                        $"土={rp.SoilCount}  气={rp.AirCount}");
                }
            }

            ed.WriteMessage(sb.ToString());
        }

        private static int FindPrimaryGroupIndex(IReadOnlyList<GroupBoundaryProfile> profiles)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i]?.IsPrimarySoilGroup == true)
                    return i;
            }

            return profiles.Count > 0 ? 0 : -1;
        }
    }
}
