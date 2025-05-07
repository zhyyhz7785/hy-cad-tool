//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;

//namespace HyCADTool.HelpClass.CreatBase
//{
//    public class SectionLine
//    {
//        public Line LineX { get; set; } // X 向直线
//        public Line LineY { get; set; } // Y 向直线
//        public int Index { get; set; }  // 排序索引
//        public double Scale { get; set; } // 缩放比例
//        public bool InsertSymbol { get; set; } // 是否插入符号

//        // 构造函数
//        public SectionLine()
//        {
//            Scale = 50; // 默认缩放比例
//            InsertSymbol = true; // 默认插入符号
//        }

//        // 方法1：选择图形并赋值 LineX 和 LineY
//        public static List<SectionLine> SelectLines(Editor ed)
//        {
//            List<SectionLine> sectionLines = new List<SectionLine>();

//            PromptSelectionOptions pso = new PromptSelectionOptions();
//            pso.MessageForAdding = "\n请选择剖切线 (直线): ";
//            pso.AllowDuplicates = false;

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
//                        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
//                            sl.LineX = line; // X 向
//                        else
//                            sl.LineY = line; // Y 向
//                        sectionLines.Add(sl);
//                    }
//                    tr.Commit();
//                }
//            }
//            return sectionLines;
//        }

//        // 方法2：对 LineX 和 LineY 排序并赋值 Index
//        public static void SortAndIndexLines(List<SectionLine> sectionLines)
//        {
//            // 按最小 X 坐标升序，X 相同则按 Y 升序排序
//            sectionLines.Sort((a, b) =>
//            {
//                Point3d aPoint = a.LineX?.StartPoint ?? a.LineY?.StartPoint ?? Point3d.Origin;
//                Point3d bPoint = b.LineX?.StartPoint ?? b.LineY?.StartPoint ?? Point3d.Origin;
//                double aMinX = Math.Min(aPoint.X, a.LineX?.EndPoint.X ?? a.LineY?.EndPoint.X ?? double.MaxValue);
//                double bMinX = Math.Min(bPoint.X, b.LineX?.EndPoint.X ?? b.LineY?.EndPoint.X ?? double.MaxValue);
//                int xCompare = aMinX.CompareTo(bMinX);
//                if (xCompare == 0)
//                {
//                    double aMinY = Math.Min(aPoint.Y, a.LineX?.EndPoint.Y ?? a.LineY?.EndPoint.Y ?? double.MaxValue);
//                    double bMinY = Math.Min(bPoint.Y, b.LineX?.EndPoint.Y ?? b.LineY?.EndPoint.Y ?? double.MaxValue);
//                    return aMinY.CompareTo(bMinY);
//                }
//                return xCompare;
//            });

//            for (int i = 0; i < sectionLines.Count; i++)
//            {
//                sectionLines[i].Index = i;
//            }
//        }

//        public void AddSymbols(Database db, Transaction tr, BlockTableRecord btr)
//        {
//            if (!InsertSymbol || (LineX == null && LineY == null)) return;

//            Line line = LineX ?? LineY;
//            bool isXDirection = LineX != null;
//            string label = isXDirection ? (Index + 1).ToString() : ((char)('A' + Index)).ToString();
//            double textHeight = 3 * Scale;
//            double extensionLength = 5 * Scale;

//            // 计算直线角度
//            Vector3d direction = line.EndPoint - line.StartPoint;
//            double angle = Math.Atan2(direction.Y, direction.X);

//            // 在两端添加符号
//            foreach (var point in new[] { line.StartPoint, line.EndPoint })
//            {
//                // 计算短直线方向（沿直线方向向外延伸）
//                Vector3d extDir = (point == line.StartPoint ? -direction : direction).GetNormal() * extensionLength;

//                // 创建短直线
//                Line extLine = new Line(point, point + extDir);
//                extLine.Layer = "00_hy_section";
//                btr.AppendEntity(extLine);
//                tr.AddNewlyCreatedDBObject(extLine, true);

//                // 创建文字
//                // 使用 Vector3d.Cross 静态方法计算叉积
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