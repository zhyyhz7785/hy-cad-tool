using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 剖切线（迁移自旧版 HelpClass/Elevation3d/SectionLine.cs）。
    /// 按方向分为 X 向（编号 1,2,3…）与 Y 向（编号 A,B,C…），
    /// 负责剖切线的选择、排序编号与剖切符号（短粗线 + 编号文字）绘制。
    /// </summary>
    public class SectionLine
    {
        /// <summary>X 向剖切线「同列」判定容差（mm）：最小 X 差距小于此值时按起点 Y 排序（旧版语义）。</summary>
        private const double SameColumnToleranceMm = 5000;

        public Line LineX { get; set; }
        public Line LineY { get; set; }
        public int Index { get; set; }
        public double Scale { get; set; } = 50;
        public bool InsertSymbol { get; set; } = true;

        /// <summary>当前剖切线（X 向优先）。</summary>
        public Line Line => LineX ?? LineY;

        /// <summary>剖面编号标签：X 向 1,2,3…；Y 向 A,B,…Z,A1…</summary>
        public string Label => LineX != null ? (Index + 1).ToString() : GetAlphaIndex(Index);

        /// <summary>
        /// 选择剖切线，只返回 ObjectId（实体在调用方事务内打开，避免跨事务实体引用）。
        /// </summary>
        public static List<ObjectId> SelectLineIds(Editor ed)
        {
            var pso = new PromptSelectionOptions { MessageForAdding = "\n请选择剖切线 (直线): " };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE") });
            var psr = ed.GetSelection(pso, filter);
            return psr.Status == PromptStatus.OK
                ? psr.Value.GetObjectIds().ToList()
                : new List<ObjectId>();
        }

        /// <summary>
        /// 在事务内根据 ObjectId 构建 SectionLine 列表（按主方向分量归类 X/Y 向）。
        /// </summary>
        public static List<SectionLine> BuildFrom(Transaction tr, IEnumerable<ObjectId> lineIds, double scale)
        {
            var result = new List<SectionLine>();
            foreach (var id in lineIds)
            {
                var line = tr.GetObject(id, OpenMode.ForRead) as Line;
                if (line == null) continue;

                var sl = new SectionLine { Scale = scale };
                Vector3d direction = line.EndPoint - line.StartPoint;
                if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                    sl.LineX = line;
                else
                    sl.LineY = line;
                result.Add(sl);
            }
            return result;
        }

        /// <summary>
        /// 排序并赋 Index：
        /// X 向 — 按最小 X 升序；X 差距小于 5000 时按起点 Y 升序（与旧版最终行为一致）；
        /// Y 向 — 按最小 X 升序，相同再按最小 Y 升序。
        /// </summary>
        public static void SortAndIndexLines(List<SectionLine> sectionLines)
        {
            var xLines = sectionLines.Where(sl => sl.LineX != null).ToList();
            var yLines = sectionLines.Where(sl => sl.LineY != null).ToList();

            xLines.Sort((a, b) =>
            {
                double aMinX = Math.Min(a.LineX.StartPoint.X, a.LineX.EndPoint.X);
                double bMinX = Math.Min(b.LineX.StartPoint.X, b.LineX.EndPoint.X);
                if (Math.Abs(aMinX - bMinX) < SameColumnToleranceMm)
                    return a.LineX.StartPoint.Y.CompareTo(b.LineX.StartPoint.Y);
                return aMinX.CompareTo(bMinX);
            });

            yLines.Sort((a, b) =>
            {
                double aMinX = Math.Min(a.LineY.StartPoint.X, a.LineY.EndPoint.X);
                double bMinX = Math.Min(b.LineY.StartPoint.X, b.LineY.EndPoint.X);
                int xCompare = aMinX.CompareTo(bMinX);
                if (xCompare != 0) return xCompare;
                double aMinY = Math.Min(a.LineY.StartPoint.Y, a.LineY.EndPoint.Y);
                double bMinY = Math.Min(b.LineY.StartPoint.Y, b.LineY.EndPoint.Y);
                return aMinY.CompareTo(bMinY);
            });

            for (int i = 0; i < xLines.Count; i++) xLines[i].Index = i;
            for (int i = 0; i < yLines.Count; i++) yLines[i].Index = i;

            sectionLines.Clear();
            sectionLines.AddRange(xLines);
            sectionLines.AddRange(yLines);
        }

        /// <summary>
        /// 在剖切线两端绘制剖切符号：延伸短粗多段线 + 编号文字（与线平行、居中、避让重叠）。
        /// </summary>
        public void AddSymbols(Transaction tr, BlockTableRecord btr)
        {
            if (!InsertSymbol || Line == null) return;

            var line = Line;
            string label = Label;

            double textHeight = 3 * Scale;
            double globalWidth = 0.7 * Scale;

            // 按文字长度计算最小延伸长度（避免文字超出线外）
            double charWidth = SectionGenerationService.ApproxCharWidthFactor * textHeight;
            double textWidth = label.Length * charWidth;
            double extensionLength = 4 * Scale;
            if (extensionLength < textWidth)
                extensionLength = textWidth + 1 * Scale;

            Vector3d direction = (line.EndPoint - line.StartPoint).GetNormal();
            double angle = Math.Atan2(direction.Y, direction.X);

            foreach (var point in new[] { line.StartPoint, line.EndPoint })
            {
                Point3d lineStart = point;
                Point3d lineEnd = point + direction * extensionLength;

                var pl = SectionGenerationService.CreateWidePolyline(lineStart, lineEnd, globalWidth);
                pl.Layer = SectionGenerationService.SymbolLayer;
                pl.ColorIndex = 7;
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                var midPt = new Point3d(
                    (lineStart.X + lineEnd.X) * 0.5,
                    (lineStart.Y + lineEnd.Y) * 0.5,
                    (lineStart.Z + lineEnd.Z) * 0.5);

                // 沿法向上移，避免文字压线
                Vector3d normal = Vector3d.ZAxis.CrossProduct(direction).GetNormal();
                midPt = midPt + normal * (0.2 * textHeight);

                var text = new DBText
                {
                    TextString = label,
                    Height = textHeight,
                    Layer = SectionGenerationService.SymbolLayer,
                    ColorIndex = 7,
                    WidthFactor = 0.7,
                    Rotation = angle,
                    HorizontalMode = TextHorizontalMode.TextCenter,
                    VerticalMode = TextVerticalMode.TextBottom,
                };
                text.AlignmentPoint = midPt;
                btr.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
            }
        }

        /// <summary>将 index 转成 A, B, …, Z, A1, B1, … 的形式。</summary>
        public static string GetAlphaIndex(int index)
        {
            int letterIndex = index % 26;
            int repeatCount = index / 26;
            char letter = (char)('A' + letterIndex);
            string suffix = repeatCount == 0 ? string.Empty : repeatCount.ToString();
            return letter + suffix;
        }
    }
}
