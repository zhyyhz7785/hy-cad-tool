using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables;
using HyCAD.Tables.Samples;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;
using HyCADTool.Features.Tables.Presentation;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>
    /// AC11 面板侧 Pick / Publish / 样表加载编排。
    /// </summary>
    public sealed class TablePanelService
    {
        private readonly Database _database;

        public TablePanelService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public TableGrid LoadPersonnelSample() => TableSamples.BuildPersonnelTable();

        public TableGrid LoadFamilySample() => TableSamples.BuildFamilyTable();

        public TablePanelPickResult TryPick(Editor ed)
        {
            var pickService = new TablePickService(_database);
            var result = pickService.TryPickSummary(ed);
            if (result == null)
                return null;

            if (!TryResolveInsertionPoint(result.CarrierId, out var insertionPoint, out var error))
            {
                ed.WriteMessage("\n[HyTable] 无法解析插入点：" + error);
                return null;
            }

            return new TablePanelPickResult(result.Grid, result.Summary, result.CarrierId, insertionPoint);
        }

        public TablePublishResult TryPublish(Editor ed, TableGrid grid, double scale = 1.0)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var ppr = ed.GetPoint("\n[HyTable] 指定表格左上角插入点: ");
            if (ppr.Status != PromptStatus.OK)
                return TablePublishResult.Cancelled;

            var insertionPoint = ppr.Value;
            var instanceGrid = grid.CloneWithNewId();

            var renderer = new AcadTableRenderer(_database, null, scale);
            TableCadHandle handle;
            try
            {
                handle = renderer.RenderAndAttach(instanceGrid, insertionPoint);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[HyTable] 渲染失败：" + ex.Message);
                return TablePublishResult.Failed;
            }

            var context = new TablePublishContext(
                instanceGrid.Id,
                insertionPoint,
                handle.CarrierId.Handle.ToString());

            ed.WriteMessage(
                $"\n[HyTable] 已写入 {handle.EntityCount} 个实体 @ ({insertionPoint.X:F1}, {insertionPoint.Y:F1})");

            return new TablePublishResult(context, handle);
        }

        /// <summary>加载网格时丢弃与当前 TableId 不匹配的拾取/发布上下文。</summary>
        internal static TablePublishContext CoalescePublishContext(
            TablePublishContext context,
            TableGrid grid)
        {
            if (context == null || grid == null)
                return null;

            return context.TableId == grid.Id ? context : null;
        }

        public bool TryResolveInsertionPoint(ObjectId carrierId, out Point3d insertionPoint, out string error)
        {
            insertionPoint = Point3d.Origin;
            error = null;

            if (carrierId.IsNull)
            {
                error = "载体无效。";
                return false;
            }

            using (var tr = _database.TransactionManager.StartTransaction())
            {
                var entity = tr.GetObject(carrierId, OpenMode.ForRead) as Entity;
                if (entity == null)
                {
                    error = "载体不是实体。";
                    return false;
                }

                if (entity is Polyline polyline && polyline.NumberOfVertices > 0)
                {
                    var pt = polyline.GetPoint3dAt(0);
                    insertionPoint = pt;
                    tr.Commit();
                    return true;
                }

                var ext = entity.GeometricExtents;
                insertionPoint = new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, 0);
                tr.Commit();
                return true;
            }
        }
    }

    public sealed class TablePanelPickResult
    {
        public TablePanelPickResult(
            TableGrid grid,
            TableSummary summary,
            ObjectId carrierId,
            Point3d insertionPoint)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            CarrierId = carrierId;
            InsertionPoint = insertionPoint;
        }

        public TableGrid Grid { get; }

        public TableSummary Summary { get; }

        public ObjectId CarrierId { get; }

        public Point3d InsertionPoint { get; }
    }

    public sealed class TablePublishResult
    {
        public static TablePublishResult Cancelled { get; } = new TablePublishResult(null, null, true, false);

        public static TablePublishResult Failed { get; } = new TablePublishResult(null, null, false, true);

        private TablePublishResult(
            TablePublishContext context,
            TableCadHandle handle,
            bool cancelled,
            bool failed)
        {
            Context = context;
            Handle = handle;
            IsCancelled = cancelled;
            IsFailed = failed;
        }

        public TablePublishResult(TablePublishContext context, TableCadHandle handle)
            : this(context, handle, false, false)
        {
        }

        public TablePublishContext Context { get; }

        public TableCadHandle Handle { get; }

        public bool IsCancelled { get; }

        public bool IsFailed { get; }

        public bool IsSuccess => !IsCancelled && !IsFailed && Context != null;
    }
}
