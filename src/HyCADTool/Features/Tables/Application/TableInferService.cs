using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables;
using HyCAD.Tables.Inference;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>AC9 线框识别编排：采集 → 推断 → Diff → 确认渲染。</summary>
    public sealed class TableInferService
    {
        private readonly Database _database;
        private readonly TableInferEngine _engine;
        private readonly AcadTableEntityCollector _collector;

        public TableInferService(Database database, TableInferOptions options = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _engine = new TableInferEngine(options);
            _collector = new AcadTableEntityCollector();
        }

        public TableInferResult InferFromSelection(Editor ed)
        {
            if (ed == null)
                throw new ArgumentNullException(nameof(ed));

            var input = _collector.CollectFromSelection(ed);
            if (input == null)
                return null;

            return _engine.Infer(input);
        }

        public TableInferDiff Compare(TableGrid inferred, TableGrid golden) =>
            TableInferDiff.Compare(inferred, golden);

        public TableCadHandle ConfirmAndRender(TableGrid grid, Point3d insertionPoint)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var renderer = new AcadTableRenderer(_database);
            return renderer.RenderAndAttach(grid, insertionPoint);
        }

        public static void WriteResult(Editor ed, TableInferResult result)
        {
            if (ed == null || result == null)
                return;

            if (result.Status != InferStatus.Success || result.Grid == null)
            {
                ed.WriteMessage($"\n[HyTable] 识别失败：{result.Status}");
                foreach (var message in result.Messages)
                    ed.WriteMessage($"\n[HyTable] {message}");
                return;
            }

            var grid = result.Grid;
            var topology = grid.Structure.Topology;
            ed.WriteMessage(
                $"\n[HyTable] 识别完成: {topology.RowCount}×{topology.ColCount} | 合并 {grid.Structure.Merges.Count} | 置信度 {result.OverallConfidence:F2}");

            if (result.UncertainCells.Count > 0)
            {
                var cells = string.Join(", ", result.UncertainCells.Select(c => $"({c.Row},{c.Col})"));
                ed.WriteMessage($"\n[HyTable] 存疑格: {cells}");
            }

            foreach (var message in result.Messages)
                ed.WriteMessage($"\n[HyTable] {message}");

            var summary = TableSummaryBuilder.Build(grid);
            TableSummaryWriter.WriteSummary(ed, summary);
        }

        public static void WriteDiff(Editor ed, TableInferDiff diff)
        {
            ed.WriteMessage(
                $"\n[HyTable] 对比: 拓扑={(diff.TopologyMatch ? "OK" : "NG")} | 合并={(diff.MergeMatch ? "OK" : "NG")} | 文字={diff.TextMatchRate:P0} | 综合={diff.OverallScore:P0}");
        }

        public static bool PromptConfirmRender(Editor ed)
        {
            var pko = new PromptKeywordOptions("\n[HyTable] 接受并渲染? [是(Y)/否(N)]: ")
            {
                AllowNone = true,
            };
            pko.Keywords.Add("是");
            pko.Keywords.Add("Y");
            pko.Keywords.Add("否");
            pko.Keywords.Add("N");
            pko.Keywords.Default = "否";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK)
                return false;

            return string.Equals(pkr.StringResult, "是", StringComparison.Ordinal)
                || string.Equals(pkr.StringResult, "Y", StringComparison.OrdinalIgnoreCase);
        }
    }
}
