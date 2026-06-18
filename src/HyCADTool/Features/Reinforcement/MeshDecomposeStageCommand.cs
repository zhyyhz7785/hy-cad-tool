using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Features.Reinforcement.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>N17–N21、N23–N24：区域网格化分步预览（阶段 B/C）。</summary>
    public sealed class MeshDecomposeStageCommand
    {
        public void ExecuteTrapezoids() => ExecuteStage(
            RegionMeshDecomposeStage.Trapezoids,
            "N17",
            "阶段 B.1 竖切条带 → 梯形",
            "  白=梯形条带  黄=夹平三角(A)");

        public void ExecuteRawParts() => ExecuteStage(
            RegionMeshDecomposeStage.RawParts,
            "N18",
            "阶段 B.2 梯形 → 矩形/三角（未合并）",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形");

        public void ExecuteYSplit() => ExecuteStage(
            RegionMeshDecomposeStage.YSplit,
            "N19",
            "阶段 C.1 Y 断点切格",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形");

        public void ExecuteHorizontalMerge() => ExecuteStage(
            RegionMeshDecomposeStage.HorizontalMerge,
            "N20",
            "阶段 C.2 横向合并",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形");

        public void ExecuteComplete() => ExecuteStage(
            RegionMeshDecomposeStage.Complete,
            "N21",
            "阶段 C.3 竖向合并+方向分类（等同 N15）",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形",
            "  合并顺序：横→竖  本步输出等同 N15，下一步 N22 构件初判");

        public void ExecuteVerticalMerge() => ExecuteStage(
            RegionMeshDecomposeStage.VerticalMerge,
            "N23",
            "阶段 C.2 竖向合并（先竖后横对比）",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形",
            "  合并顺序：竖（对比 N20 横先）");

        public void ExecuteCompleteVerticalFirst() => ExecuteStage(
            RegionMeshDecomposeStage.CompleteVerticalFirst,
            "N24",
            "阶段 C.3 横向合并+方向分类（先竖后横）",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形",
            "  合并顺序：竖→横  对比 N21/N15（横→竖），单元数/形状可能不同");

        /// <summary>N30：夹平三角 + 全顶点延长线裁至第一交点有限弦分割。</summary>
        public void ExecuteReflexPartition() => ExecuteStage(
            RegionMeshDecomposeStage.ReflexRectPartition,
            "N30",
            "有限弦延长线剖分",
            "  青=竖向  绿=横向  洋红=近方形  黄=三角形",
            "  先夹平斜边切三角；全顶点四向延长线裁至第一交点，仅沿有限弦分割(不合并)");

        private static void ExecuteStage(
            RegionMeshDecomposeStage stage,
            string commandTag,
            string stageTitle,
            string legend,
            string extraLine = null)
        {
            var ctx = MeshRegionPipelineHelper.TrySelectAndGroup(
                $"\n选择混凝土边界多段线（{commandTag} {stageTitle}）: ");
            if (ctx == null)
                return;

            var ed = ctx.Editor;
            ed.WriteMessage(
                $"\n[{commandTag}] {stageTitle}  分组距离={ctx.GroupDistanceMm:F0}mm");

            var allCells = new List<MeshCell>();
            var sb = new StringBuilder();
            sb.AppendLine($"\n── {commandTag} {stageTitle} ──");

            for (int g = 0; g < ctx.Groups.Count; g++)
            {
                var group = ctx.Groups[g];
                int verticalBefore = CountOrientation(allCells, MeshCellOrientation.Vertical);
                int horizontalBefore = CountOrientation(allCells, MeshCellOrientation.Horizontal);
                int squareBefore = CountOrientation(allCells, MeshCellOrientation.Square);
                int trapBefore = CountTrapezoids(allCells);
                int triBefore = allCells.Count(c => c.Kind == MeshCellKind.Triangle);

                foreach (var region in group)
                {
                    if (region?.Outer == null || region.Outer.VertexCount < 3)
                        continue;

                    allCells.AddRange(RegionMeshDecomposer.DecomposeToStage(region, stage));
                }

                int verticalCount = CountOrientation(allCells, MeshCellOrientation.Vertical) - verticalBefore;
                int horizontalCount = CountOrientation(allCells, MeshCellOrientation.Horizontal) - horizontalBefore;
                int squareCount = CountOrientation(allCells, MeshCellOrientation.Square) - squareBefore;
                int trapCount = CountTrapezoids(allCells) - trapBefore;
                int triCount = allCells.Count(c => c.Kind == MeshCellKind.Triangle) - triBefore;

                if (stage == RegionMeshDecomposeStage.Trapezoids)
                {
                    sb.AppendLine(
                        $"  G#{g}  区域={group.Count}  梯形={trapCount}  三角={triCount}");
                }
                else
                {
                    sb.AppendLine(
                        $"  G#{g}  区域={group.Count}  竖={verticalCount}  横={horizontalCount}  方={squareCount}  三角={triCount}");
                }
            }

            if (allCells.Count == 0)
            {
                ed.WriteMessage("\n未生成网格单元。");
                return;
            }

            if (stage == RegionMeshDecomposeStage.Trapezoids)
            {
                int totalTrap = CountTrapezoids(allCells);
                int totalTri = allCells.Count(c => c.Kind == MeshCellKind.Triangle);
                double totalArea = allCells.Sum(c => c.AreaMm2);
                sb.AppendLine(
                    $"  合计  梯形={totalTrap}  三角={totalTri}  单元={allCells.Count}  面积={totalArea:F0}mm²");
            }
            else
            {
                int totalVertical = CountOrientation(allCells, MeshCellOrientation.Vertical);
                int totalHorizontal = CountOrientation(allCells, MeshCellOrientation.Horizontal);
                int totalSquare = CountOrientation(allCells, MeshCellOrientation.Square);
                int totalTri = allCells.Count(c => c.Kind == MeshCellKind.Triangle);
                double totalArea = allCells.Sum(c => c.AreaMm2);
                sb.AppendLine(
                    $"  合计  竖={totalVertical}  横={totalHorizontal}  方={totalSquare}  三角={totalTri}  单元={allCells.Count}  面积={totalArea:F0}mm²");
            }

            int drawn = RegionMeshPreviewService.Draw(allCells);
            sb.AppendLine($"  预览实体={drawn}  图层「{RegionMeshPreviewService.PreviewLayerName}」");
            sb.AppendLine(legend);
            if (!string.IsNullOrEmpty(extraLine))
                sb.AppendLine(extraLine);

            ed.WriteMessage(sb.ToString());
        }

        private static int CountOrientation(IEnumerable<MeshCell> cells, MeshCellOrientation orientation)
        {
            return cells.Count(c => c.Orientation == orientation);
        }

        private static int CountTrapezoids(IEnumerable<MeshCell> cells)
        {
            return cells.Count(c =>
                c.Kind == MeshCellKind.Rectangle && c.Orientation == MeshCellOrientation.None);
        }
    }
}
