using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables;
using HyCAD.Tables.Data;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Shared.AutoCAD.Services;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// TableGrid → 模型空间线框 + 文字 + AC3 真相源（Carrier JSON / XData / Group）。
    /// </summary>
    public sealed class AcadTableRenderer
    {
        /// <summary>AutoCAD 线+文字渲染能力。</summary>
        public static AcadTableCapability Capability => AcadTableCapability.LineFrameDefaults;

        private readonly Database _database;
        private readonly AcadTableRenderOptions _options;

        public AcadTableRenderer(Database database, AcadTableRenderOptions options = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _options = options ?? AcadTableRenderOptions.Default;
        }

        /// <summary>
        /// 渲染并 Attach 真相源（AC3）。
        /// </summary>
        public TableCadHandle RenderAndAttach(TableGrid grid, Point3d insertionPoint)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var layout = TableLayout.Create(grid, insertionPoint.X, insertionPoint.Y);
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无活动文档。");

            using (doc.LockDocument())
            using (var tr = _database.TransactionManager.StartTransaction())
            {
                var layerManager = new LayerManager(_database);
                layerManager.EnsureLayer(tr, _options.GridLayerName, _options.GridLayerColor);
                layerManager.EnsureLayer(tr, _options.TextLayerName, _options.TextLayerColor);

                var bt = (BlockTable)tr.GetObject(_database.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var carrier = CreateCellBorder(layout.TableBounds);
                ms.AppendEntity(carrier);
                tr.AddNewlyCreatedDBObject(carrier, true);

                var members = new List<(ObjectId Id, string Kind, CellAddr? Cell)>();

                AppendGridBorders(layout, ms, tr, members);
                foreach (var cell in layout.EnumerateVisibleCells())
                    RenderCellContent(grid, cell, ms, tr, members);

                var handle = AcadTableStore.Attach(tr, _database, grid, carrier.ObjectId, members);
                tr.Commit();
                return handle;
            }
        }

        /// <summary>
        /// 渲染表格到模型空间（兼容 AC2；内部走 RenderAndAttach）。
        /// </summary>
        public ObjectIdCollection Render(TableGrid grid, Point3d insertionPoint)
        {
            var handle = RenderAndAttach(grid, insertionPoint);
            var ids = new ObjectIdCollection { handle.CarrierId };
            return ids;
        }

        private void AppendGridBorders(
            TableLayout layout,
            BlockTableRecord ms,
            Transaction tr,
            List<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            var segments = BorderGridResolver.Resolve(layout, _options.DefaultInnerBorderWidthMm);
            var z = _options.ZElevation;

            foreach (var seg in segments)
            {
                var segment = CreateBorderSegment(seg, z);
                ms.AppendEntity(segment);
                tr.AddNewlyCreatedDBObject(segment, true);
                members.Add((segment.ObjectId, HyTableXdata.KindGrid, null));
            }
        }

        private Polyline CreateBorderSegment(BorderGridSegment seg, double z)
        {
            Point2d p0;
            Point2d p1;

            if (seg.Orientation == GridLineOrientation.Vertical)
            {
                p0 = new Point2d(seg.FixedCoord, seg.Start);
                p1 = new Point2d(seg.FixedCoord, seg.End);
            }
            else
            {
                p0 = new Point2d(seg.Start, seg.FixedCoord);
                p1 = new Point2d(seg.End, seg.FixedCoord);
            }

            var width = Math.Max(seg.WidthMm, _options.MinBorderWidthMm);
            var polyline = new Polyline(2);
            polyline.AddVertexAt(0, p0, 0, 0, 0);
            polyline.AddVertexAt(1, p1, 0, 0, 0);
            polyline.Layer = _options.GridLayerName;
            polyline.Elevation = z;
            polyline.ConstantWidth = width;
            return polyline;
        }

        private void RenderCellContent(
            TableGrid grid,
            VisibleCellLayout cell,
            BlockTableRecord ms,
            Transaction tr,
            List<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            var structure = grid.Structure;
            if (structure.Roles.TryGetValue(cell.Addr, out var role))
            {
                if (role == CellRole.PhotoSlot || role == CellRole.Spacer)
                    return;
            }

            if (structure.Diagonals.TryGetValue(cell.Addr, out var split))
            {
                AppendDiagonalCell(structure, cell, split, ms, tr, members);
                return;
            }

            var effective = AcadTableRoleStyle.ResolveEffectiveStyle(structure, cell.Addr, _options);

            if (effective.Style.Orientation == TextOrientation.VerticalStacked)
            {
                AppendVerticalStackedText(grid, cell, effective, ms, tr, members);
                return;
            }

            AppendHorizontalText(grid, cell, effective, ms, tr, members);
        }

        private void AppendDiagonalCell(
            GridStructure structure,
            VisibleCellLayout cell,
            DiagonalSplit split,
            BlockTableRecord ms,
            Transaction tr,
            List<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            var effective = AcadTableRoleStyle.ResolveEffectiveStyle(structure, cell.Addr, _options);

            var diagonalLine = AcadTableDiagonalRenderer.CreateDiagonalLine(
                cell.Bounds,
                split.Direction,
                _options);
            ms.AppendEntity(diagonalLine);
            tr.AddNewlyCreatedDBObject(diagonalLine, true);
            members.Add((diagonalLine.ObjectId, HyTableXdata.KindGrid, null));

            foreach (var text in AcadTableDiagonalRenderer.CreateSubCellTexts(
                         split,
                         cell.Bounds,
                         effective,
                         _options,
                         _database))
            {
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                members.Add((text.ObjectId, HyTableXdata.KindText, cell.Addr));
            }
        }

        private void AppendVerticalStackedText(
            TableGrid grid,
            VisibleCellLayout cell,
            EffectiveCellStyle effective,
            BlockTableRecord ms,
            Transaction tr,
            List<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            var displayText = GetDisplayText(GridEditor.GetValue(grid, cell.Addr));
            if (string.IsNullOrEmpty(displayText) && !_options.DrawEmptyCellText)
                return;

            var positions = AcadTableVerticalText.ComputePositions(
                displayText,
                cell.Bounds,
                effective.Style,
                _options);

            foreach (var (character, x, y) in positions)
            {
                var text = new DBText
                {
                    Height = effective.Style.TextHeight,
                    TextString = character.ToString(),
                    Layer = _options.TextLayerName,
                    WidthFactor = effective.WidthFactor,
                };

                AcadTableTextMapper.ApplyAtPoint(
                    text,
                    x,
                    y,
                    TextAlign.Center,
                    _options.ZElevation,
                    _database);

                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                members.Add((text.ObjectId, HyTableXdata.KindText, cell.Addr));
            }
        }

        private void AppendHorizontalText(
            TableGrid grid,
            VisibleCellLayout cell,
            EffectiveCellStyle effective,
            BlockTableRecord ms,
            Transaction tr,
            List<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            var displayText = GetDisplayText(GridEditor.GetValue(grid, cell.Addr));
            if (string.IsNullOrEmpty(displayText) && !_options.DrawEmptyCellText)
                return;

            var text = new DBText
            {
                Height = effective.Style.TextHeight,
                TextString = displayText,
                Layer = _options.TextLayerName,
                WidthFactor = effective.WidthFactor,
            };

            AcadTableTextMapper.ApplyAlignment(
                text,
                cell.Bounds,
                effective.Style,
                _options.TextPaddingMm,
                _options.ZElevation,
                _database);

            ms.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
            members.Add((text.ObjectId, HyTableXdata.KindText, cell.Addr));
        }

        private Polyline CreateCellBorder(LayoutRect bounds)
        {
            var z = _options.ZElevation;
            var polyline = new Polyline(4);
            polyline.AddVertexAt(0, new Point2d(bounds.Left, bounds.Top), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(bounds.Right, bounds.Top), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(bounds.Right, bounds.Bottom), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(bounds.Left, bounds.Bottom), 0, 0, 0);
            polyline.Closed = true;
            polyline.Layer = _options.GridLayerName;
            polyline.Elevation = z;
            polyline.ConstantWidth = 0;
            return polyline;
        }

        private static string GetDisplayText(CellValue value)
        {
            if (value.Runs != null && value.Runs.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var run in value.Runs)
                    sb.Append(run.Text);
                return sb.ToString();
            }

            return value.Text ?? string.Empty;
        }
    }
}
