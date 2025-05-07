using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

[assembly: CommandClass(typeof(HyCADTool.Command.HyCommand))]

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        private const double DEFAULT_TOLERANCE = 1e-3; // 默认公差值，用于判断几何关系的精度
        [CommandMethod("hyov")]
        public static void LineOverkill()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            const double DEFAULT_TOLERANCE = 1e-6;
            Tolerance tol = new Tolerance(DEFAULT_TOLERANCE, DEFAULT_TOLERANCE);
            double d = 10;

            PromptSelectionResult selRes = GetLineSelection(ed);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何线段，命令取消。");
                return;
            }

            // 第一步：使用独立事务复制直线
            List<Line> lines;
            List<ObjectId> lineIds;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    lines = CollectLines(tr, selRes, db, out lineIds);
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n复制直线失败: {ex.Message}");
                    tr.Abort();
                    return;
                }
            }

            // 第二步：开启新事务处理直线并更新图纸
            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = GetModelSpace(tr, db);
                    try
                    {
                        List<Line> mergedLines = ProcessOverlappingLines(lines, tol);
                        List<Line> finalLines = ProcessEndpointConnections(mergedLines, tol, d, tr, btr);
                        UpdateDrawing(tr, btr, lineIds, finalLines);
                        tr.Commit();
                        ed.WriteMessage($"\n处理完成，合并了 {lineIds.Count - finalLines.Count} 条重叠线段。");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n错误: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
        }
        private static List<Line> CollectLines(Transaction tr, PromptSelectionResult selRes, Database db, out List<ObjectId> lineIds)
        {
            lineIds = new List<ObjectId>();
            List<Line> newLines = new List<Line>();

            var lines = (from SelectedObject selObj in selRes.Value
                         let line = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line
                         where line != null
                         select new
                         {
                             OriginalId = selObj.ObjectId,
                             NewLine = new Line(line.StartPoint, line.EndPoint) // 复制线段
                         }).ToList();

            var sortedLines = lines.OrderBy(l => l.NewLine.StartPoint.X)
                                  .ThenBy(l => l.NewLine.StartPoint.Y)
                                  .ToList();

            foreach (var lineInfo in sortedLines)
            {
                lineIds.Add(lineInfo.OriginalId);
                newLines.Add(lineInfo.NewLine);
            }

            return newLines;
        }

        // 辅助方法：创建标准化的直线
        private static Line CreateNormalizedLine(Line line)
        {
            // 强制 Z 坐标为 0
            Point3d p1 = new Point3d(line.StartPoint.X, line.StartPoint.Y, 0);
            Point3d p2 = new Point3d(line.EndPoint.X, line.EndPoint.Y, 0);

            // 定义容差值（可根据需求调整）
            const double tolerance = DEFAULT_TOLERANCE;

            // 按 X 坐标较小点为起点，X 相等时按 Y 较小点为起点
            if (p1.X < p2.X - tolerance ||
                (Math.Abs(p1.X - p2.X) < tolerance && p1.Y < p2.Y - tolerance))
            {
                return new Line(p1, p2);
            }
            return new Line(p2, p1);
        }

    }


}