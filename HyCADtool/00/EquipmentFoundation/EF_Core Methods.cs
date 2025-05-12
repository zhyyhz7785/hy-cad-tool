using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Models;
using HyCADTool.Utilities;
using System;
using System.Collections.Generic;
namespace HyCADTool.Core
{
    public static class CoreMethods
    {
        public static void ConstructBaseDataFilter(Database db, Editor ed)
        {
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>"),
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_轮廓"),
                new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓*"),
                new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_轮廓_编号"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selRes = ed.GetSelection(filter);
            if (selRes.Status != PromptStatus.OK) return;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var polylineList = new List<Polyline>();
                var circleList = new List<Circle>();
                var textList = new List<DBText>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pline && ent.Layer == "00_Hy_螺栓_轮廓")
                        polylineList.Add(pline);
                    else if (ent is Circle circle && ent.Layer.StartsWith("00_Hy_螺栓") && ent.Layer != "00_Hy_螺栓_轮廓_编号")
                        circleList.Add(circle);
                    else if (ent is DBText text && ent.Layer == "00_Hy_螺栓_轮廓_编号")
                        textList.Add(text);
                }
                polylineList.SortByStartPoint();
                circleList.SortByCenter();
                textList.Sort((t1, t2) => t1.Position.X.CompareTo(t2.Position.X));
                foreach (Polyline pline in polylineList)
                {
                    var baseData = new BaseData();
                    Guid plineGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, pline, ed);
                    if (plineGuid == Guid.Empty)
                    {
                        plineGuid = Guid.NewGuid();
                        pline.UpgradeOpen();
                        ExtensionDictionaryUtils.WriteGuidToExtensionDictionary(tr, pline, plineGuid, ed);
                    }
                    baseData.Id = plineGuid;
                    foreach (DBText text in textList)
                    {
                        if (GeometryUtils .IsPointInside(pline, text.Position))
                        {
                            baseData.SerialNumber = int.TryParse(text.TextString.TrimStart('0'), out int num) ? num : 0;
                            break;
                        }
                    }
                    foreach (Circle circle in circleList)
                    {
                        if (GeometryUtils.IsPointInside(pline, circle.Center))
                        {
                            Guid circleGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, circle, ed);
                            if (circleGuid == Guid.Empty)
                            {
                                circleGuid = Guid.NewGuid();
                                circle.UpgradeOpen();
                                ExtensionDictionaryUtils.WriteGuidToExtensionDictionary(tr, circle, circleGuid, ed);
                            }
                            string model = "1";
                            var anchorBolt = ExtensionDictionaryUtils.ReadAnchorBoltFromExtensionDictionary(tr, circle, ed);
                            if (anchorBolt != null)
                                model = anchorBolt.Model;
                            else
                            {
                                circle.UpgradeOpen();
                                circle.ColorIndex = 1;
                            }
                            var boltData = new BoltData { Id = circleGuid, Model = model };
                            ExtensionDictionaryUtils.WriteBoltDataToExtensionDictionary(tr, circle, boltData, ed);
                            baseData.AddBoltId(circleGuid);
                        }
                    }
                    pline.UpgradeOpen();
                    ExtensionDictionaryUtils.WriteBaseDataToExtensionDictionary(tr, pline, baseData, ed);
                    ed.WriteMessage($"\n初始化 BaseData，Guid: {baseData.Id}");
                }
                tr.Commit();
            }
        }
        public static void HighlightBoltData(Database db, Editor ed, Polyline pline)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var baseData = ExtensionDictionaryUtils.ReadBaseDataFromExtensionDictionary(tr, pline, ed);
                if (baseData == null)
                {
                    ed.WriteMessage($"\n多段线没有 BaseData。");
                    return;
                }
                if (baseData.BoltIds == null || baseData.BoltIds.Count == 0)
                {
                    ed.WriteMessage($"\n多段线 Guid {baseData.Id} 的 BaseData 中没有螺栓数据。");
                    return;
                }
                var equipment = new EquipmentDataManager().GetEquipmentByNumber(baseData.SerialNumber);
                if (equipment != null)
                {
                    ed.WriteMessage($"\n{equipment.EquipmentName}+{equipment.Number}+{equipment.Weight}+{equipment.HorizontalForce}");
                }
                ed.WriteMessage($"\n多段线 Guid {baseData.Id} 包含以下螺栓数据：");
                foreach (var boltGuid in baseData.BoltIds)
                {
                    var circle = baseData.GetCircle(db, tr, ed, boltGuid);
                    if (circle != null)
                    {
                        var boltData = ExtensionDictionaryUtils.ReadBoltDataFromExtensionDictionary(tr, circle, ed);
                        circle.UpgradeOpen();
                        circle.Highlight();
                        ed.WriteMessage($"\n - BoltData Guid: {boltGuid}, Model: {boltData?.Model ?? "未定义"}");
                    }
                }
                tr.Commit();
            }
        }
        public static List<AxisData> ConstructAxis(Database db, Editor ed)
        {
            var psoY = new PromptSelectionOptions { MessageForAdding = "\n请选择 Y 向轴线相关的直线和文字: " };
            TypedValue[] filterListY = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>"),
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.LayerName, "00_hy_3公共_轴线_总"),
                new TypedValue((int)DxfCode.LayerName, "00_hy_3公共_轴线_总_文字"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filterY = new SelectionFilter(filterListY);
            PromptSelectionResult selResY = ed.GetSelection(psoY, filterY);
            if (selResY.Status != PromptStatus.OK) return null;
            var peoX = new PromptEntityOptions("\n请选择一条 X 向轴线（直线）: ");
            peoX.SetRejectMessage("\n必须选择一条直线！");
            peoX.AddAllowedClass(typeof(Line), true);
            PromptEntityResult perX = ed.GetEntity(peoX);
            var axes = new List<AxisData>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var yLines = new List<Line>();
                var texts = new List<DBText>();
                foreach (SelectedObject selObj in selResY.Value)
                {
                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent is Line line && ent.Layer == "00_hy_3公共_轴线_总")
                        yLines.Add(line);
                    else if (ent is DBText text && ent.Layer == "00_hy_3公共_轴线_总_文字")
                        texts.Add(text);
                    else if (ent is MText mtext && ent.Layer == "00_hy_3公共_轴线_总_文字")
                        texts.Add(new DBText { TextString = mtext.Contents, Position = mtext.Location });
                }
                Line xLine = perX.Status == PromptStatus.OK ? tr.GetObject(perX.ObjectId, OpenMode.ForRead) as Line : null;
                yLines.Sort((l1, l2) => l1.StartPoint.X.CompareTo(l2.StartPoint.X) != 0 ? l1.StartPoint.X.CompareTo(l2.StartPoint.X) : l1.StartPoint.Y.CompareTo(l2.StartPoint.Y));
                Dictionary<Line, DBText> lineTextPairs = MatchLinesWithTexts(yLines, texts, ed);
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                int serialNumber = 1;
                foreach (var yLine in yLines)
                {
                    var axisData = new AxisData();
                    Guid lineGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, yLine, ed);
                    if (lineGuid == Guid.Empty)
                    {
                        lineGuid = Guid.NewGuid();
                        yLine.UpgradeOpen();
                        ExtensionDictionaryUtils.WriteGuidToExtensionDictionary(tr, yLine, lineGuid, ed);
                    }
                    axisData.SerialNumber = serialNumber++;
                    axisData.Name = lineTextPairs.TryGetValue(yLine, out DBText matchedText) && matchedText != null ? matchedText.TextString : $"Axis_{axisData.SerialNumber}";
                    axisData.IntersectionPoint = xLine != null && GetIntersectionPoint(yLine, xLine).HasValue ? (x: GetIntersectionPoint(yLine, xLine).Value.X, y: GetIntersectionPoint(yLine, xLine).Value.Y) : (0, 0);
                    Point3d bottomPoint = yLine.StartPoint.Y < yLine.EndPoint.Y ? yLine.StartPoint : yLine.EndPoint;
                    Point3d circleCenter = new Point3d(bottomPoint.X, bottomPoint.Y - 4 * BaseConfig.Scale, 0);
                    double circleDiameter = 8 * BaseConfig.Scale;
                    Circle circle = new Circle(circleCenter, Vector3d.ZAxis, circleDiameter / 2.0) { Layer = "00_hy_3公共_轴线_总" };
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    DBText serialNumberText = new DBText
                    {
                        Position = circleCenter,
                        Height = 2.5 * BaseConfig.Scale,
                        TextString = axisData.SerialNumber.ToString(),
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextVerticalMid,
                        AlignmentPoint = circleCenter,
                        Layer = "00_hy_3公共_轴线_总_文字"
                    };
                    btr.AppendEntity(serialNumberText);
                    tr.AddNewlyCreatedDBObject(serialNumberText, true);
                    yLine.UpgradeOpen();
                    ExtensionDictionaryUtils.WriteAxisDataToExtensionDictionary(tr, yLine, axisData, ed);
                    axes.Add(axisData);
                }
                tr.Commit();
            }
            return axes;
        }
        private static Point3d? GetIntersectionPoint(Line line1, Line line2)
        {
            try
            {
                Point3dCollection intersections = new Point3dCollection();
                line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
                return intersections.Count > 0 ? intersections[0] : (Point3d?)null;
            }
            catch
            {
                return null;
            }
        }
        private static Dictionary<Line, DBText> MatchLinesWithTexts(List<Line> lines, List<DBText> texts, Editor ed)
        {
            var lineTextPairs = new Dictionary<Line, DBText>();
            double distanceThreshold = 2.5 * BaseConfig.Scale;
            foreach (var line in lines)
            {
                DBText closestText = null;
                double minDistance = double.MaxValue;
                foreach (var text in texts)
                {
                    if (lineTextPairs.ContainsValue(text)) continue;
                    Point3d textPoint = text.Position;
                    Point3d closestPoint = line.GetClosestPointTo(textPoint, false);
                    double distance = textPoint.DistanceTo(closestPoint);
                    if (distance < minDistance && distance <= distanceThreshold)
                    {
                        minDistance = distance;
                        closestText = text;
                    }
                }
                if (closestText != null)
                {
                    lineTextPairs[line] = closestText;
                }
            }
            return lineTextPairs;
        }
        public static AxisData InitializeAxis(Database db, Editor ed)
        {
            var peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
            peo.SetRejectMessage("\n必须选择直线！");
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;
            AxisData axis = null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Line;
                if (line == null) return null;
                axis = ExtensionDictionaryUtils.ReadAxisDataFromExtensionDictionary(tr, line, ed);
                if (axis == null)
                {
                    axis = new AxisData();
                    Guid lineGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, line, ed);
                    if (lineGuid == Guid.Empty)
                    {
                        lineGuid = Guid.NewGuid();
                        ExtensionDictionaryUtils.WriteGuidToExtensionDictionary(tr, line, lineGuid, ed);
                    }
                }
                var pso = new PromptSelectionOptions { MessageForAdding = "\n请选择多个底座 (Polyline): " };
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<AND"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_轮廓"),
                    new TypedValue((int)DxfCode.Operator, "AND>")
                };
                SelectionFilter filter = new SelectionFilter(filterList);
                PromptSelectionResult selRes = ed.GetSelection(pso, filter);
                if (selRes.Status != PromptStatus.OK) return axis;
                foreach (SelectedObject selObj in selRes.Value)
                {
                    var pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pline != null)
                    {
                        var baseData = ExtensionDictionaryUtils.ReadBaseDataFromExtensionDictionary(tr, pline, ed);
                        if (baseData == null)
                        {
                            baseData = new BaseData();
                            Guid plineGuid = ExtensionDictionaryUtils.ReadGuidFromExtensionDictionary(tr, pline, ed);
                            if (plineGuid == Guid.Empty)
                            {
                                plineGuid = Guid.NewGuid();
                                pline.UpgradeOpen();
                                ExtensionDictionaryUtils.WriteGuidToExtensionDictionary(tr, pline, plineGuid, ed);
                            }
                            baseData.Id = plineGuid;
                            pline.UpgradeOpen();
                            ExtensionDictionaryUtils.WriteBaseDataToExtensionDictionary(tr, pline, baseData, ed);
                        }
                        axis.Bases.Add(baseData);
                    }
                }
                line.UpgradeOpen();
                ExtensionDictionaryUtils.WriteAxisDataToExtensionDictionary(tr, line, axis, ed);
                tr.Commit();
            }
            return axis;
        }
        public static AxisData GetAxisFromLine(Database db, Editor ed)
        {
            var peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
            peo.SetRejectMessage("\n必须选择直线！");
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
                if (line == null) return null;
                var axis = ExtensionDictionaryUtils.ReadAxisDataFromExtensionDictionary(tr, line, ed);
                if (axis != null)
                {
                    axis.RebuildEntityIdMap(db, tr, ed);
                }
                return axis;
            }
        }
        public static void DisplayStructure(Database db, Editor ed, AxisData axis)
        {
            if (axis == null || axis.Bases == null || axis.Bases.Count == 0)
            {
                ed.WriteMessage("\nAxisData 或 Bases 为空，无法高亮显示结构！");
                return;
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var baseData in axis.Bases)
                {
                    var pline = baseData.GetPolyline(db, tr, ed);
                    if (pline != null)
                    {
                        try
                        {
                            pline.UpgradeOpen();
                            pline.Highlight();
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            ed.WriteMessage($"\n高亮多段线失败，Guid: {baseData.Id}, 错误: {ex.Message}");
                        }
                    }
                    else
                    {
                        ed.WriteMessage($"\n未找到匹配的多段线，Guid: {baseData.Id}");
                    }
                    foreach (var boltId in baseData.BoltIds)
                    {
                        var circle = baseData.GetCircle(db, tr, ed, boltId);
                        if (circle != null)
                        {
                            try
                            {
                                circle.UpgradeOpen();
                                circle.Highlight();
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {
                                ed.WriteMessage($"\n高亮螺栓圆失败，Guid: {boltId}, 错误: {ex.Message}");
                            }
                        }
                        else
                        {
                            ed.WriteMessage($"\n未找到匹配的螺栓圆，Guid: {boltId}");
                        }
                    }
                }
                tr.Commit();
            }
            ed.UpdateScreen();
        }
        //public static void CreateAxisTable(Database db, Editor ed, AxisData axis)
        //{
        //    if (axis == null || axis.Bases == null || axis.Bases.Count == 0)
        //    {
        //        ed.WriteMessage("\nAxisData 或 Bases 为空，无法创建表格！");
        //        return;
        //    }
        //    using (Transaction tr = db.TransactionManager.StartTransaction())
        //    {
        //        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
        //        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
        //        foreach (var baseData in axis.Bases)
        //        {
        //            Polyline pline = baseData.GetPolyline(db, tr, ed);
        //            if (pline == null)
        //            {
        //                ed.WriteMessage($"\n未找到 Polyline (Guid: {baseData.Id})，跳过表格创建。");
        //                continue;
        //            }
        //            Point3d bottomLeft = GetBottomLeftPoint(pline);
        //            // 使用 bottomLeft 替换 customLeft
        //            Point3d tablePosition = new Point3d(bottomLeft.X - 5 * BaseConfig.Scale, bottomLeft.Y - 5 * BaseConfig.Scale, 0);
        //            var circles = new List<(Circle Circle, int SerialNumber)>();
        //            for (int i = 0; i < baseData.BoltIds.Count; i++)
        //            {
        //                var circle = baseData.GetCircle(db, tr, ed, baseData.BoltIds[i]);
        //                if (circle != null)
        //                {
        //                    circles.Add((circle, i + 1));
        //                }
        //            }
        //            Table table = new Table();
        //            table.Position = tablePosition;
        //            // 设置表格大小
        //            table.SetSize(circles.Count + 1, 3);
        //            // 设置表格样式
        //            // 获取或创建表格样式
        //            TableStyle ts = new TableStyle();
        //            DBDictionary tsd = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForWrite);
        //            if (!tsd.Contains("CustomTableStyle"))
        //            {
        //                ts.Name = "CustomTableStyle";
        //                tsd.SetAt("CustomTableStyle", ts);
        //                tr.AddNewlyCreatedDBObject(ts, true);
        //            }
        //            else
        //            {
        //                ts = (TableStyle)tr.GetObject(tsd.GetAt("CustomTableStyle"), OpenMode.ForWrite);
        //            }
        //            // 设置文本高度（通过 TableStyle）
        //            ts.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.TitleRow);    // 250
        //            ts.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.HeaderRow); // 175
        //            ts.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.DataRow);   // 125
        //            // 设置单元格边距（使用现代方式）
        //            ts.HorizontalCellMargin = BaseConfig.TableStyleConfig.CellHorizontalMargin * BaseConfig.Scale; // 例如 0.1 * 50 = 5
        //            ts.VerticalCellMargin = BaseConfig.TableStyleConfig.CellVerticalMargin * BaseConfig.Scale;   // 例如 0.1 * 50 = 5
        //            // 设置标题行内容和样式
        //            table.Cells[0, 0].TextString = "序列号";
        //            table.Cells[0, 1].TextString = "X 相对坐标";
        //            table.Cells[0, 2].TextString = "Y 相对坐标";
        //            table.Cells[0, -1].BackgroundColor = Color.FromColorIndex(ColorMethod.ByAci, 251);
        //            table.Cells[0, -1].Alignment = CellAlignment.MiddleCenter;
        //            // 设置数据行
        //            for (int i = 0; i < circles.Count; i++)
        //            {
        //                var (circle, serialNumber) = circles[i];
        //                double relX = circle.Center.X - axis.IntersectionPoint.X;
        //                double relY = circle.Center.Y - axis.IntersectionPoint.Y;
        //                table.Cells[i + 1, 0].TextString = serialNumber.ToString();
        //                table.Cells[i + 1, 1].TextString = relX.ToString("F0");
        //                table.Cells[i + 1, 2].TextString = relY.ToString("F0");
        //                table.Cells[i + 1, -1].Alignment = CellAlignment.MiddleCenter;
        //                ed.WriteMessage($"\nRow {i + 1}: Serial={serialNumber}, X={relX:F0}, Y={relY:F0}");
        //            }
        //            // 设置行高和列宽
        //            table.SetRowHeight(5 * BaseConfig.Scale);
        //            table.Columns[0].Width = 500;
        //            table.Columns[1].Width = 500;
        //            table.Columns[2].Width = 500;
        //            table.Layer = "00_hy_4公共_表格";
        //            table.GenerateLayout();
        //            btr.AppendEntity(table);
        //            tr.AddNewlyCreatedDBObject(table, true);
        //        }
        //        tr.Commit();
        //    }
        //    ed.Regen();
        //}
        public static void CreateAxisTable(Database db, Editor ed, AxisData axis)
        {
            if (axis == null || axis.Bases == null || axis.Bases.Count == 0)
            {
                ed.WriteMessage("\nAxisData 或 Bases 为空，无法创建表格！");
                return;
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (var baseData in axis.Bases)
                {
                    Polyline pline = baseData.GetPolyline(db, tr, ed);
                    if (pline == null)
                    {
                        ed.WriteMessage($"\n未找到 Polyline (Guid: {baseData.Id})，跳过表格创建。");
                        continue;
                    }
                    Point3d bottomLeft = GetBottomLeftPoint(pline);
                    // 注意：这里仍使用 customLeft.X，应改为 bottomLeft.X
                    Point3d tablePosition = new Point3d(bottomLeft.X - 5 * BaseConfig.Scale, bottomLeft.Y - 5 * BaseConfig.Scale, 0);
                    var circles = new List<(Circle Circle, int SerialNumber)>();
                    for (int i = 0; i < baseData.BoltIds.Count; i++)
                    {
                        var circle = baseData.GetCircle(db, tr, ed, baseData.BoltIds[i]);
                        if (circle != null)
                        {
                            circles.Add((circle, i + 1));
                        }
                    }
                    Table table = new Table();
                    table.Position = tablePosition;
                    // 设置表格大小
                    table.SetSize(circles.Count + 1, 3);
                    // 直接设置文本高度（无需 TableStyle）
                    table.Rows[0].TextHeight = 2.5 * BaseConfig.Scale; // 标题行文字高度
                    table.Rows[0].TextStyleId = BaseConfig.TextStyleId; // 标题行文字高度
                    table.Rows[0].Height = 2.5 * BaseConfig.Scale; // 标题行文字高度
                    for (int i = 1; i < table.Rows.Count; i++)
                    {
                        table.Rows[i].TextHeight = 2.5 * BaseConfig.Scale; // 数据行文字高度
                        table.Rows[i].TextStyleId = BaseConfig.TextStyleId;
                        table.Rows[i].Height = 2.5 * BaseConfig.Scale;
                    }
                    // 设置标题行内容和样式
                    table.Cells[0, 0].TextString = "序号";
                    table.Cells[0, 1].TextString = "X";
                    table.Cells[0, 2].TextString = "Y";
                    table.Cells[0, -1].BackgroundColor = Color.FromColorIndex(ColorMethod.ByAci, 251);
                    table.Cells[0, -1].Alignment = CellAlignment.MiddleCenter;
                    // 设置数据行
                    for (int i = 0; i < circles.Count; i++)
                    {
                        var (circle, serialNumber) = circles[i];
                        double relX = circle.Center.X - axis.IntersectionPoint.X;
                        double relY = circle.Center.Y - axis.IntersectionPoint.Y;
                        table.Cells[i + 1, 0].TextString = serialNumber.ToString();
                        table.Cells[i + 1, 1].TextString = relX.ToString("F0");
                        table.Cells[i + 1, 2].TextString = relY.ToString("F0");
                        table.Cells[i + 1, -1].Alignment = CellAlignment.MiddleCenter;
                        ed.WriteMessage($"\nRow {i + 1}: Serial={serialNumber}, X={relX:F0}, Y={relY:F0}");
                    }
                    // 设置行高和列宽
                    table.SetRowHeight(5 * BaseConfig.Scale);
                    table.Columns[0].Width = 300;
                    table.Columns[1].Width = 400;
                    table.Columns[2].Width = 400;
                    table.Layer = "00_hy_4公共_表格";
                    table.GenerateLayout();
                    btr.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                }
                tr.Commit();
            }
            ed.Regen();
        }
        // 确保表格样式支持三列显示
        private static TableStyle EnsureTableStyle(Database db, Transaction tr, string styleName)
        {
            DBDictionary tsd = tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (tsd.Contains(styleName))
            {
                return tr.GetObject(tsd.GetAt(styleName), OpenMode.ForRead) as TableStyle;
            }
            tsd.UpgradeOpen();
            TableStyle ts = new TableStyle
            {
                Name = styleName,
            };
            tsd.SetAt(styleName, ts);
            tr.AddNewlyCreatedDBObject(ts, true);
            return ts;
        }
        private static Point3d GetBottomLeftPoint(Polyline pline)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                Point3d vertex = pline.GetPoint3dAt(i);
                if (vertex.X < minX) minX = vertex.X;
                if (vertex.Y < minY) minY = vertex.Y;
            }
            return new Point3d(minX, minY, 0);
        }
    }
    public static class Extensions
    {
        public static void SortByStartPoint(this List<Polyline> list)
        {
            list.Sort((p1, p2) =>
            {
                Point3d p1Start = p1.StartPoint;
                Point3d p2Start = p2.StartPoint;
                int xCompare = p1Start.X.CompareTo(p2Start.X);
                return xCompare != 0 ? xCompare : p1Start.Y.CompareTo(p2Start.Y);
            });
        }
        public static void SortByCenter(this List<Circle> list)
        {
            list.Sort((c1, c2) =>
            {
                Point3d c1Center = c1.Center;
                Point3d c2Center = c2.Center;
                int xCompare = c1Center.X.CompareTo(c2Center.X);
                return xCompare != 0 ? xCompare : c1Center.Y.CompareTo(c2Center.Y);
            });
        }
    }
}