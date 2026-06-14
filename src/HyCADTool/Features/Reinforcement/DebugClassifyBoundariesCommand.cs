using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.Features.Cluster.Domain.Services;
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
    /// 调试 §2.3：分组聚类 + 外轮廓 Union 合并（BuildGroupedRegions）。
    /// </summary>
    public sealed class DebugClassifyBoundariesCommand
    {
        private sealed class OuterClusterItem
        {
            public int ClassifiedIndex { get; set; }
            public BoundingBox Bbox { get; set; }
        }

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateComponentParameters() : new ComponentParameters();
            double groupDistanceMm = parameters.RegionGroupDistanceMm;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线（§2.3 聚类合并调试）: " },
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
            var outerClusters = ClusterOuterIndices(classified, groupDistanceMm);
            var groups = ReinRegionBuilder.BuildGroupedRegions(classified, groupDistanceMm);

            ReportToCommandLine(ed, classified, outerClusters, groups, groupDistanceMm);

            int drawn = BoundaryClassifyPreviewService.DrawGrouped(classified, groups, outerClusters);
            ed.WriteMessage(
                $"\n§2.3 聚类合并预览：{drawn} 个实体，图层「{BoundaryClassifyPreviewService.DebugLayerName}」。" +
                $"\n  聚类阈值={groupDistanceMm:F0}mm  计算组={groups.Count}  Union后区域={groups.Sum(g => g.Count)}");

            var legend = new StringBuilder("\n  计算组配色：");
            for (int g = 0; g < groups.Count; g++)
                legend.Append($" G{g}={BoundaryClassifyPreviewService.DescribeGroupColor(g)}");
            legend.Append("\n  细框=单外轮廓 bbox  粗框=计算组整体外包  同色填充=Union混凝土  红边=孔  黄圈=P0");
            ed.WriteMessage(legend.ToString());
        }

        private static List<List<int>> ClusterOuterIndices(
            IReadOnlyList<ReinRegionBuilder.ClassifiedBoundary> classified,
            double groupDistanceMm)
        {
            var items = new List<OuterClusterItem>();
            for (int i = 0; i < classified.Count; i++)
            {
                if (classified[i].IsHole || classified[i].Boundary.VertexCount < 3)
                    continue;

                items.Add(new OuterClusterItem
                {
                    ClassifiedIndex = i,
                    Bbox = GetBoundingBox(classified[i].Boundary)
                });
            }

            if (items.Count == 0)
                return new List<List<int>>();

            if (groupDistanceMm <= 0)
                groupDistanceMm = 1500.0;

            var clusteringService = new ClusteringService();
            var clusters = clusteringService.ClusterByBoundsDistance(
                items, o => o.Bbox, groupDistanceMm);

            return clusters
                .Select(c => c.Select(x => x.ClassifiedIndex).OrderBy(x => x).ToList())
                .ToList();
        }

        private static void ReportToCommandLine(
            Editor ed,
            IReadOnlyList<ReinRegionBuilder.ClassifiedBoundary> classified,
            IReadOnlyList<IReadOnlyList<int>> outerClusters,
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            double groupDistanceMm)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n── §2.2 嵌套分类（{classified.Count} 条）──");

            for (int i = 0; i < classified.Count; i++)
            {
                var item = classified[i];
                var b = item.Boundary;
                var p0 = b.GetPointAt(0);
                string role = item.IsHole ? "孔" : "外";
                sb.AppendLine(
                    $"  #{i} {role} depth={item.NestingDepth} 顶点={b.VertexCount}  首点=({p0.X:F1},{p0.Y:F1})");
            }

            sb.AppendLine($"── §2.3 聚类（阈值={groupDistanceMm:F0}mm，{outerClusters.Count} 组）──");

            var outerBboxes = new Dictionary<int, BoundingBox>();
            for (int i = 0; i < classified.Count; i++)
            {
                if (!classified[i].IsHole && classified[i].Boundary.VertexCount >= 3)
                    outerBboxes[i] = GetBoundingBox(classified[i].Boundary);
            }

            for (int g = 0; g < outerClusters.Count; g++)
            {
                var members = outerClusters[g];
                string memberList = string.Join(",", members.Select(m => $"#{m}"));
                string colorName = BoundaryClassifyPreviewService.DescribeGroupColor(g);
                sb.AppendLine($"  G#{g}({colorName})  外轮廓 {members.Count} 个: {memberList}");

                for (int a = 0; a < members.Count; a++)
                {
                    for (int b = a + 1; b < members.Count; b++)
                    {
                        int ia = members[a], ib = members[b];
                        if (!outerBboxes.TryGetValue(ia, out var ba) || !outerBboxes.TryGetValue(ib, out var bb))
                            continue;

                        double gap = ba.DistanceTo(bb);
                        sb.AppendLine($"      #{ia}↔#{ib} bbox间隙={gap:F1}mm");
                    }
                }
            }

            sb.AppendLine($"── §2.3 Union 合并（{groups.Count} 组 → {groups.Sum(gr => gr.Count)} 个 ReinRegion）──");

            for (int g = 0; g < groups.Count; g++)
            {
                var groupRegions = groups[g];
                sb.AppendLine($"  G#{g}  Union 后 {groupRegions.Count} 个区域:");

                for (int r = 0; r < groupRegions.Count; r++)
                {
                    var region = groupRegions[r];
                    var holeIndices = FindHoleIndices(classified, region.Holes);
                    string holeList = holeIndices.Count > 0
                        ? string.Join(",", holeIndices.Select(h => $"#{h}"))
                        : "无";

                    var op0 = region.Outer.GetPointAt(0);
                    sb.AppendLine(
                        $"    G{g}-R{r}  外顶点={region.Outer.VertexCount}  首点=({op0.X:F1},{op0.Y:F1})  " +
                        $"孔洞 {region.Holes.Count} 个: {holeList}");
                }
            }

            ed.WriteMessage(sb.ToString());
        }

        private static BoundingBox GetBoundingBox(Polyline2D poly)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = System.Math.Min(minX, p.X);
                maxX = System.Math.Max(maxX, p.X);
                minY = System.Math.Min(minY, p.Y);
                maxY = System.Math.Max(maxY, p.Y);
            }

            if (minX == double.MaxValue)
                return new BoundingBox(Point2D.Origin, Point2D.Origin);

            return new BoundingBox(new Point2D(minX, minY), new Point2D(maxX, maxY));
        }

        private static List<int> FindHoleIndices(
            IReadOnlyList<ReinRegionBuilder.ClassifiedBoundary> classified,
            IReadOnlyList<Polyline2D> holes)
        {
            var result = new List<int>();
            if (holes == null || holes.Count == 0)
                return result;

            for (int i = 0; i < classified.Count; i++)
            {
                if (!classified[i].IsHole)
                    continue;

                var hole = classified[i].Boundary;
                foreach (var assigned in holes)
                {
                    if (ReferenceEquals(hole, assigned) || SameFirstVertex(hole, assigned))
                    {
                        result.Add(i);
                        break;
                    }
                }
            }

            return result;
        }

        private static bool SameFirstVertex(Polyline2D a, Polyline2D b)
        {
            if (a == null || b == null)
                return false;

            var p0 = a.GetPointAt(0);
            var q0 = b.GetPointAt(0);
            return System.Math.Abs(p0.X - q0.X) < 0.01
                && System.Math.Abs(p0.Y - q0.Y) < 0.01;
        }
    }
}
