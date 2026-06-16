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
    /// N15：混凝土区域网格化 — 矩形 + 三角形分解预览（纯几何，最少矩形）。
    /// </summary>
    public sealed class MeshConcreteRegionCommand
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
                $"\n[N15] 区域网格化（条带方向配色）  分组距离={groupDistanceMm:F0}mm");

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线（N15 区域网格化）: " },
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

            var allCells = new List<MeshCell>();
            var sb = new StringBuilder();
            sb.AppendLine("\n── N15 区域网格化 ──");

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                int verticalBefore = CountOrientation(allCells, MeshCellOrientation.Vertical);
                int horizontalBefore = CountOrientation(allCells, MeshCellOrientation.Horizontal);
                int squareBefore = CountOrientation(allCells, MeshCellOrientation.Square);
                int triBefore = allCells.Count(c => c.Kind == MeshCellKind.Triangle);

                foreach (var region in group)
                {
                    if (region?.Outer == null || region.Outer.VertexCount < 3)
                        continue;

                    allCells.AddRange(RegionMeshDecomposer.Decompose(region));
                }

                int verticalCount = CountOrientation(allCells, MeshCellOrientation.Vertical) - verticalBefore;
                int horizontalCount = CountOrientation(allCells, MeshCellOrientation.Horizontal) - horizontalBefore;
                int squareCount = CountOrientation(allCells, MeshCellOrientation.Square) - squareBefore;
                int triCount = allCells.Count(c => c.Kind == MeshCellKind.Triangle) - triBefore;
                sb.AppendLine(
                    $"  G#{g}  区域={group.Count}  竖={verticalCount}  横={horizontalCount}  方={squareCount}  三角={triCount}");
            }

            if (allCells.Count == 0)
            {
                ed.WriteMessage("\n未生成网格单元。");
                return;
            }

            int totalVertical = CountOrientation(allCells, MeshCellOrientation.Vertical);
            int totalHorizontal = CountOrientation(allCells, MeshCellOrientation.Horizontal);
            int totalSquare = CountOrientation(allCells, MeshCellOrientation.Square);
            int totalTri = allCells.Count(c => c.Kind == MeshCellKind.Triangle);
            double totalArea = allCells.Sum(c => c.AreaMm2);

            sb.AppendLine(
                $"  合计  竖={totalVertical}  横={totalHorizontal}  方={totalSquare}  三角={totalTri}  单元={allCells.Count}  面积={totalArea:F0}mm²");

            int drawn = RegionMeshPreviewService.Draw(allCells);
            sb.AppendLine($"  预览实体={drawn}  图层「{RegionMeshPreviewService.PreviewLayerName}」");
            sb.AppendLine("  青=竖向  绿=横向  洋红=近方形  黄=三角形");

            ed.WriteMessage(sb.ToString());
        }

        private static int CountOrientation(IEnumerable<MeshCell> cells, MeshCellOrientation orientation)
        {
            return cells.Count(c => c.Orientation == orientation);
        }
    }
}
