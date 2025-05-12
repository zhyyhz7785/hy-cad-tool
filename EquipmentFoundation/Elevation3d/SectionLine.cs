using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
namespace EquipmentFoundation.CreatBase
{
    public class SectionLine
    {
        public Line LineX { get; set; }
        public Line LineY { get; set; }
        public int Index { get; set; }
        public double Scale { get; set; }
        public bool InsertSymbol { get; set; }
        public SectionLine()
        {
            Scale = 50;
            InsertSymbol = true;
        }
        // 选择剖切线
        public static List<SectionLine> SelectLines(Editor ed)
        {
            List<SectionLine> sectionLines = new List<SectionLine>();
            PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = "\n请选择剖切线 (直线): " };
            TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Start, "LINE") };
            SelectionFilter selFilter = new SelectionFilter(filter);
            PromptSelectionResult psr = ed.GetSelection(pso, selFilter);
            if (psr.Status != PromptStatus.OK) return sectionLines;
            foreach (ObjectId objId in psr.Value.GetObjectIds())
            {
                using (Transaction tr = ed.Document.Database.TransactionManager.StartTransaction())
                {
                    Line line = tr.GetObject(objId, OpenMode.ForRead) as Line;
                    if (line != null)
                    {
                        SectionLine sl = new SectionLine();
                        Vector3d direction = line.EndPoint - line.StartPoint;
                        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                            sl.LineX = line;
                        else
                            sl.LineY = line;
                        sectionLines.Add(sl);
                    }
                    tr.Commit();
                }
            }
            return sectionLines;
        }
        // 排序并给 X/Y 方向的线赋值 Index
        public static void SortAndIndexLines(List<SectionLine> sectionLines)
        {
            var xLines = sectionLines.Where(sl => sl.LineX != null).ToList();
            var yLines = sectionLines.Where(sl => sl.LineY != null).ToList();
            // X 方向线 - 修改排序逻辑
            // 第一条x以x最小排序，当剖断线x坐标最小值差距小于静态值d=5000的时候，以y坐标升序排序
            // 1. 先按 aMinX 排序（不考虑 Y）
            xLines.Sort((a, b) =>
            {
                double aMinX = Math.Min(a.LineX.StartPoint.X, a.LineX.EndPoint.X);
                double bMinX = Math.Min(b.LineX.StartPoint.X, b.LineX.EndPoint.X);
                return aMinX.CompareTo(bMinX);
            });
            // 2. 取出第一个元素 data1
            var data1 = xLines[0];
            xLines.RemoveAt(0);
            // 3. 剩下的数据按原方法排序（考虑 Y）
            xLines.Sort((a, b) =>
            {
                double aMinX = Math.Min(a.LineX.StartPoint.X, a.LineX.EndPoint.X);
                double bMinX = Math.Min(b.LineX.StartPoint.X, b.LineX.EndPoint.X);
                if (Math.Abs(aMinX - bMinX) < 5000)
                {
                    return a.LineX.StartPoint.Y.CompareTo(b.LineX.StartPoint.Y);
                }
                else
                {
                    return aMinX.CompareTo(bMinX);
                }
            });
            // 4. 把 data1 放回最前面
            xLines.Insert(0, data1);
            // Y 方向线 - 保持您已测试通过的代码不变
            yLines.Sort((a, b) =>
            {
                // 按最小 X 坐标排序
                double aMinX = Math.Min(a.LineY.StartPoint.X, a.LineY.EndPoint.X);
                double bMinX = Math.Min(b.LineY.StartPoint.X, b.LineY.EndPoint.X);
                int xCompare = aMinX.CompareTo(bMinX);
                if (xCompare == 0)
                {
                    // 若 X 坐标相同，再比较最小 Y 坐标
                    double aMinY = Math.Min(a.LineY.StartPoint.Y, a.LineY.EndPoint.Y);
                    double bMinY = Math.Min(b.LineY.StartPoint.Y, b.LineY.EndPoint.Y);
                    return aMinY.CompareTo(bMinY);
                }
                return xCompare;
            });
            // 重新设置索引
            for (int i = 0; i < xLines.Count; i++)
            {
                xLines[i].Index = i;
            }
            for (int i = 0; i < yLines.Count; i++)
            {
                yLines[i].Index = i;
            }
            // 清空原 List 并按 X/Y 顺序将结果重新加入
            sectionLines.Clear();
            sectionLines.AddRange(xLines);
            sectionLines.AddRange(yLines);
        }
        public void AddSymbols(Database db, Transaction tr, BlockTableRecord btr)
        {
            if (!InsertSymbol || (LineX == null && LineY == null)) return;
            Line line = LineX ?? LineY;
            bool isXDirection = (LineX != null);
            // X 方向数字, Y 方向字母
            string label = isXDirection
                ? (Index + 1).ToString()     // 1,2,3,...
                : GetAlphaIndex(Index);      // A,B,...Z,A1,...
            double textHeight = 3 * Scale;
            double globalWidth = 0.7 * Scale; // 多段线的宽度
            // 按文字长度计算最小需要的线长（避免文字超出线外）
            double charWidth = 0.6 * textHeight * 0.7;
            double textWidth = label.Length * charWidth;
            // 原本的延伸长度
            double extensionLength = 4 * Scale;
            // 若过小，就增加到至少能容下文字宽度
            if (extensionLength < textWidth)
            {
                extensionLength = textWidth + (1 * Scale);
            }
            // 方向向量
            Vector3d direction = line.EndPoint - line.StartPoint;
            direction = direction.GetNormal();
            double angle = Math.Atan2(direction.Y, direction.X);
            foreach (var point in new[] { line.StartPoint, line.EndPoint })
            {
                // 多段线：从 point 开始延伸 extensionLength
                Point3d lineStart = point;
                Point3d lineEnd = point + (direction * extensionLength);
                // 创建 2D polyline
                Polyline pl = CreateWidePolyline(lineStart, lineEnd, globalWidth);
                pl.Layer = "00_hy_Section_Symbol";
                pl.ColorIndex = 7;
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                // 计算多段线的中点
                Point3d midPt = new Point3d(
                    (lineStart.X + lineEnd.X) * 0.5,
                    (lineStart.Y + lineEnd.Y) * 0.5,
                    (lineStart.Z + lineEnd.Z) * 0.5
                );
                // ========== 关键：向上移动文字对齐点以避免文字与线重叠 ==========
                // normal：垂直于线(direction)的单位向量，ZAxis × direction
                Vector3d normal = Vector3d.ZAxis.CrossProduct(direction).GetNormal();
                // 往上移动多少？您可以根据需要调整
                double textOffset = 0.2 * textHeight; // 例如，让文字下边缘比线高 0.2 * textHeight
                midPt = midPt + (normal * textOffset);
                // 创建文字(DBText)
                DBText text = new DBText
                {
                    TextString = label,
                    Height = textHeight,
                    Layer = "00_hy_Section_Symbol",
                    ColorIndex = 7,
                    WidthFactor = 0.7,
                    Rotation = angle // 与多段线平行
                };
                // 文字对齐设置：水平居中，底部对齐到 AlignmentPoint
                text.HorizontalMode = TextHorizontalMode.TextCenter;
                text.VerticalMode = TextVerticalMode.TextBottom;
                text.AlignmentPoint = midPt;
                text.Position = midPt;
                btr.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
            }
        }
        private static Polyline CreateWidePolyline(Point3d start, Point3d end, double globalWidth)
        {
            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(start.X, start.Y), 0, globalWidth, globalWidth);
            pl.AddVertexAt(1, new Point2d(end.X, end.Y), 0, globalWidth, globalWidth);
            return pl;
        }
        private static string GetAlphaIndex(int index)
        {
            int letterIndex = index % 26;
            int repeatCount = index / 26;
            char letter = (char)('A' + letterIndex);
            string suffix = repeatCount == 0 ? "" : repeatCount.ToString();
            return letter + suffix;
        }
        // 计算剖面线长度
        public double GetLength()
        {
            return (LineX ?? LineY)?.Length ?? 0;
        }
    }
}
