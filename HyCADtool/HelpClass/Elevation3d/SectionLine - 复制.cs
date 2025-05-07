//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace HyCADTool.HelpClass.CreatBase
//{
//    public class SectionLine
//    {
//        public Line LineX { get; set; }
//        public Line LineY { get; set; }
//        public int Index { get; set; }
//        public double Scale { get; set; }
//        public bool InsertSymbol { get; set; }

//        public SectionLine()
//        {
//            Scale = 50;
//            InsertSymbol = true;
//        }

//        // 计算剖断线长度
//        public double GetLength()
//        {
//            Line line = LineX ?? LineY;
//            return line?.Length ?? 0;
//        }

//        // 选择剖断线
//        public static List<SectionLine> SelectLines(Editor ed)
//        {
//            List<SectionLine> sectionLines = new List<SectionLine>();
//            PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = "\n请选择剖切线 (直线): " };
//            TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Start, "LINE") };
//            SelectionFilter selFilter = new SelectionFilter(filter);
//            PromptSelectionResult psr = ed.GetSelection(pso, selFilter);

//            if (psr.Status != PromptStatus.OK) return sectionLines;

//            foreach (ObjectId objId in psr.Value.GetObjectIds())
//            {
//                using (Transaction tr = ed.Document.Database.TransactionManager.StartTransaction())
//                {
//                    Line line = tr.GetObject(objId, OpenMode.ForRead) as Line;
//                    if (line != null)
//                    {
//                        SectionLine sl = new SectionLine();
//                        Vector3d direction = line.EndPoint - line.StartPoint;
//                        if (Math.Abs(direction.X) > Math.Abs(direction.Y)) sl.LineX = line;
//                        else sl.LineY = line;
//                        sectionLines.Add(sl);
//                    }
//                    tr.Commit();
//                }
//            }
//            return sectionLines;
//        }

//        // 排序并赋值索引
//        public static void SortAndIndexLines(List<SectionLine> sectionLines)
//        {
//            // 分离 X 和 Y 方向的线
//            var xLines = sectionLines.Where(sl => sl.LineX != null).ToList();
//            var yLines = sectionLines.Where(sl => sl.LineY != null).ToList();

//            // 对 X 方向的线排序（按最小 X 坐标）
//            xLines.Sort((a, b) =>
//            {
//                double aMinX = Math.Min(a.LineX.StartPoint.X, a.LineX.EndPoint.X);
//                double bMinX = Math.Min(b.LineX.StartPoint.X, b.LineX.EndPoint.X);
//                int xCompare = aMinX.CompareTo(bMinX);
//                return xCompare == 0 ? a.LineX.StartPoint.Y.CompareTo(b.LineX.StartPoint.Y) : xCompare;
//            });

//            // 对 Y 方向的线排序（按最小 Y 坐标）
//            yLines.Sort((a, b) =>
//            {
//                double aMinY = Math.Min(a.LineY.StartPoint.Y, a.LineY.EndPoint.Y);
//                double bMinY = Math.Min(b.LineY.StartPoint.Y, b.LineY.EndPoint.Y);
//                int yCompare = aMinY.CompareTo(bMinY);
//                return yCompare == 0 ? a.LineY.StartPoint.X.CompareTo(b.LineY.StartPoint.X) : yCompare;
//            });

//            // 分配 Index
//            for (int i = 0; i < xLines.Count; i++) xLines[i].Index = i;
//            for (int i = 0; i < yLines.Count; i++) yLines[i].Index = i;

//            // 合并回原列表（可选，保持原顺序处理）
//            sectionLines.Clear();
//            sectionLines.AddRange(xLines);
//            sectionLines.AddRange(yLines);
//        }

//        // 添加端点符号
//        public void AddSymbols(Database db, Transaction tr, BlockTableRecord btr)
//        {
//            if (!InsertSymbol || (LineX == null && LineY == null)) return;

//            Line line = LineX ?? LineY;
//            bool isXDirection = LineX != null;
//            string label = isXDirection ? (Index + 1).ToString() : ((char)('A' + Index)).ToString();
//            double textHeight = 3 * Scale;
//            double extensionLength = 5 * Scale;

//            Vector3d direction = line.EndPoint - line.StartPoint;
//            double angle = Math.Atan2(direction.Y, direction.X);

//            foreach (var point in new[] { line.StartPoint, line.EndPoint })
//            {
//                Vector3d extDir = (point == line.StartPoint ? -direction : direction).GetNormal() * extensionLength;
//                Line extLine = new Line(point, point + extDir) { Layer = "00_hy_section" };
//                btr.AppendEntity(extLine);
//                tr.AddNewlyCreatedDBObject(extLine, true);

//                Point3d textPos = point + extDir + Vector3d.ZAxis.CrossProduct(direction).GetNormal() * textHeight * 0.5;
//                DBText text = new DBText
//                {
//                    TextString = label,
//                    Height = textHeight,
//                    Position = textPos,
//                    Rotation = angle,
//                    Layer = "00_hy_section"
//                };
//                btr.AppendEntity(text);
//                tr.AddNewlyCreatedDBObject(text, true);
//            }
//        }
//    }
//}