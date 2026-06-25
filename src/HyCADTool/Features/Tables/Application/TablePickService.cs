using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>
    /// AC7 拾取 + 读回 + 再渲染编排。
    /// </summary>
    public sealed class TablePickResult
    {
        public TablePickResult(TableGrid grid, TableSummary summary, ObjectId carrierId)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            CarrierId = carrierId;
        }

        public TableGrid Grid { get; }

        public TableSummary Summary { get; }

        public ObjectId CarrierId { get; }
    }

    public sealed class TablePickService
    {
        private readonly Database _database;

        public TablePickService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>拾取一次；取消或非 HyTable 返回 null。</summary>
        public TablePickResult TryPickSummary(Editor ed)
        {
            if (ed == null)
                throw new ArgumentNullException(nameof(ed));

            var peo = new PromptEntityOptions("\n[HyTable] 选择表格实体（外框/边框线/文字/组）: ")
            {
                AllowNone = false,
            };

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return null;

            using (var tr = _database.TransactionManager.StartTransaction())
            {
                var picked = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!AcadTableStore.TryResolveTableGrid(tr, _database, picked, out var grid, out var error))
                {
                    ed.WriteMessage("\n[HyTable] 读回失败：" + (error ?? "未知错误"));
                    return null;
                }

                if (!AcadTableStore.TryResolveCarrierId(tr, _database, picked, out var carrierId))
                {
                    ed.WriteMessage("\n[HyTable] 无法定位表格载体。");
                    return null;
                }

                var summary = TableSummaryBuilder.Build(grid);
                summary.CarrierHandle = carrierId.Handle.ToString();

                tr.Commit();
                return new TablePickResult(grid, summary, carrierId);
            }
        }

        /// <summary>克隆 grid 并换新 Id 后 RenderAndAttach。</summary>
        public TableCadHandle RerenderAt(TableGrid grid, Point3d insertionPoint)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var clone = grid.CloneWithNewId();

            var renderer = new AcadTableRenderer(_database);
            return renderer.RenderAndAttach(clone, insertionPoint);
        }
    }
}
