using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.EquipmentFoundation.Domain.Entities;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.EquipmentFoundation.Services
{
    /// <summary>
    /// 设备基础服务
    /// 迁移自旧代码 CoreMethods（EF_Core Methods.cs）
    /// </summary>
    public static class EquipmentFoundationService
    {
        #region 图层常量

        private const string LayerBoltOutline = "00_Hy_螺栓_轮廓";
        private const string LayerBoltPrefix = "00_Hy_螺栓";
        private const string LayerBoltNumber = "00_Hy_螺栓_轮廓_编号";
        private static string LayerAxisLine => UserLayerNameResolver.Get(LayerSemanticIds.PublicAxisMain, LayerBuiltinDefaults.PublicAxisMain);
        private static string LayerAxisText => UserLayerNameResolver.Get(LayerSemanticIds.PublicAxisText, LayerBuiltinDefaults.PublicAxisText);
        private static string LayerTable => UserLayerNameResolver.Get(LayerSemanticIds.PublicTableMain, LayerBuiltinDefaults.PublicTableMain);

        #endregion

        #region ConstructBaseData — 构建底座数据

        /// <summary>
        /// 选择多段线/圆/文字，构建底座数据并写入扩展字典
        /// </summary>
        public static void ConstructBaseData(Database db, Editor ed)
        {
            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>"),
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.LayerName, LayerBoltOutline),
                new TypedValue((int)DxfCode.LayerName, LayerBoltPrefix + "*"),
                new TypedValue((int)DxfCode.LayerName, LayerBoltNumber),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };

            var selRes = ed.GetSelection(new SelectionFilter(filterList));
            if (selRes.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var polylineList = new List<Polyline>();
                var circleList = new List<Circle>();
                var textList = new List<DBText>();

                foreach (SelectedObject selObj in selRes.Value)
                {
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pline && ent.Layer == LayerBoltOutline)
                        polylineList.Add(pline);
                    else if (ent is Circle circle && ent.Layer.StartsWith(LayerBoltPrefix) && ent.Layer != LayerBoltNumber)
                        circleList.Add(circle);
                    else if (ent is DBText text && ent.Layer == LayerBoltNumber)
                        textList.Add(text);
                }

                // 排序
                polylineList.Sort((p1, p2) =>
                {
                    int x = p1.StartPoint.X.CompareTo(p2.StartPoint.X);
                    return x != 0 ? x : p1.StartPoint.Y.CompareTo(p2.StartPoint.Y);
                });
                circleList.Sort((c1, c2) =>
                {
                    int x = c1.Center.X.CompareTo(c2.Center.X);
                    return x != 0 ? x : c1.Center.Y.CompareTo(c2.Center.Y);
                });
                textList.Sort((t1, t2) => t1.Position.X.CompareTo(t2.Position.X));

                foreach (var pline in polylineList)
                {
                    var baseData = new BaseData();

                    // 确保多段线有 Guid
                    pline.UpgradeOpen();
                    var plineGuid = ExtensionDictionaryService.EnsureGuid(tr, pline);
                    baseData.Id = plineGuid;

                    // 匹配编号文字
                    foreach (var text in textList)
                    {
                        if (IsPointInside(pline, text.Position))
                        {
                            baseData.SerialNumber = int.TryParse(text.TextString.TrimStart('0'), out int num) ? num : 0;
                            break;
                        }
                    }

                    // 匹配螺栓圆
                    foreach (var circle in circleList)
                    {
                        if (IsPointInside(pline, circle.Center))
                        {
                            circle.UpgradeOpen();
                            var circleGuid = ExtensionDictionaryService.EnsureGuid(tr, circle);

                            string model = "1";
                            var anchorBolt = ExtensionDictionaryService.ReadAnchorBolt(tr, circle);
                            if (anchorBolt != null)
                                model = anchorBolt.Model;
                            else
                                circle.ColorIndex = 1; // 红色标记未定义螺栓

                            var boltData = new BoltData { Id = circleGuid, Model = model };
                            ExtensionDictionaryService.WriteBoltData(tr, circle, boltData);
                            baseData.AddBoltId(circleGuid);
                        }
                    }

                    ExtensionDictionaryService.WriteBaseData(tr, pline, baseData);
                    ed.WriteMessage($"\n初始化 BaseData，Guid: {baseData.Id}");
                }

                tr.Commit();
            }
        }

        #endregion

        #region HighlightBoltData — 高亮螺栓数据

        /// <summary>
        /// 选择多段线，读取 BaseData 并高亮关联螺栓
        /// </summary>
        public static void HighlightBoltData(Database db, Editor ed)
        {
            var peo = new PromptEntityOptions("\n请选择一个多段线: ");
            peo.SetRejectMessage("\n必须选择多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (pline == null)
                {
                    ed.WriteMessage("\n未选择多段线，操作取消！");
                    return;
                }

                var baseData = ExtensionDictionaryService.ReadBaseData(tr, pline);
                if (baseData == null)
                {
                    ed.WriteMessage("\n多段线没有 BaseData。");
                    return;
                }

                if (baseData.BoltIds == null || baseData.BoltIds.Count == 0)
                {
                    ed.WriteMessage($"\n多段线 Guid {baseData.Id} 的 BaseData 中没有螺栓数据。");
                    return;
                }

                // 尝试读取设备信息
                var equipFilePath = SettingsPanelViewModel.Current?.EquipmentDataFilePath;
                if (!string.IsNullOrEmpty(equipFilePath))
                {
                    try
                    {
                        var manager = new EquipmentDataManager(equipFilePath);
                        var equipment = manager.GetEquipmentByNumber(baseData.SerialNumber);
                        if (equipment != null)
                        {
                            ed.WriteMessage($"\n{equipment.EquipmentName}+{equipment.Number}+{equipment.Weight}+{equipment.HorizontalForce}");
                        }
                    }
                    catch (System.Exception) { /* 文件不存在时忽略 */ }
                }

                ed.WriteMessage($"\n多段线 Guid {baseData.Id} 包含以下螺栓数据：");

                // 高亮螺栓圆
                var guidEntityMap = BuildGuidEntityMap(db, tr);
                foreach (var boltGuid in baseData.BoltIds)
                {
                    if (guidEntityMap.TryGetValue(boltGuid, out ObjectId circleId))
                    {
                        var circle = tr.GetObject(circleId, OpenMode.ForRead) as Circle;
                        if (circle != null)
                        {
                            var boltData = ExtensionDictionaryService.ReadBoltData(tr, circle);
                            circle.UpgradeOpen();
                            circle.Highlight();
                            ed.WriteMessage($"\n - BoltData Guid: {boltGuid}, Model: {boltData?.Model ?? "未定义"}");
                        }
                    }
                }

                tr.Commit();
            }
        }

        #endregion

        #region ConstructAxis — 构建轴线

        /// <summary>
        /// 选择 Y 向轴线和 X 向轴线，创建轴线数据并绘制序号圆+文字
        /// </summary>
        public static List<AxisData> ConstructAxis(Database db, Editor ed)
        {
            // 选择 Y 向轴线
            var psoY = new PromptSelectionOptions { MessageForAdding = "\n请选择 Y 向轴线相关的直线和文字: " };
            var filterListY = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>"),
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.LayerName, LayerAxisLine),
                new TypedValue((int)DxfCode.LayerName, LayerAxisText),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            var selResY = ed.GetSelection(psoY, new SelectionFilter(filterListY));
            if (selResY.Status != PromptStatus.OK) return null;

            // 选择 X 向轴线
            var peoX = new PromptEntityOptions("\n请选择一条 X 向轴线（直线）: ");
            peoX.SetRejectMessage("\n必须选择一条直线！");
            peoX.AddAllowedClass(typeof(Line), true);
            var perX = ed.GetEntity(peoX);

            double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;
            var axes = new List<AxisData>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var yLines = new List<Line>();
                var texts = new List<DBText>();

                foreach (SelectedObject selObj in selResY.Value)
                {
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent is Line line && ent.Layer == LayerAxisLine)
                        yLines.Add(line);
                    else if (ent is DBText text && ent.Layer == LayerAxisText)
                        texts.Add(text);
                    else if (ent is MText mtext && ent.Layer == LayerAxisText)
                        texts.Add(new DBText { TextString = mtext.Contents, Position = mtext.Location });
                }

                Line xLine = perX.Status == PromptStatus.OK
                    ? tr.GetObject(perX.ObjectId, OpenMode.ForRead) as Line
                    : null;

                // 按 X 排序
                yLines.Sort((l1, l2) =>
                {
                    int x = l1.StartPoint.X.CompareTo(l2.StartPoint.X);
                    return x != 0 ? x : l1.StartPoint.Y.CompareTo(l2.StartPoint.Y);
                });

                // 匹配线与文字
                var lineTextPairs = MatchLinesWithTexts(yLines, texts, scale);

                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                int serialNumber = 1;
                foreach (var yLine in yLines)
                {
                    var axisData = new AxisData();

                    yLine.UpgradeOpen();
                    ExtensionDictionaryService.EnsureGuid(tr, yLine);

                    axisData.SerialNumber = serialNumber++;
                    axisData.Name = lineTextPairs.TryGetValue(yLine, out DBText matchedText) && matchedText != null
                        ? matchedText.TextString
                        : $"Axis_{axisData.SerialNumber}";

                    // 计算交点
                    if (xLine != null)
                    {
                        var ip = GetIntersectionPoint(yLine, xLine);
                        axisData.IntersectionPoint = ip.HasValue ? (ip.Value.X, ip.Value.Y) : (0, 0);
                    }

                    // 在轴线底部绘制序号圆+文字
                    Point3d bottomPoint = yLine.StartPoint.Y < yLine.EndPoint.Y ? yLine.StartPoint : yLine.EndPoint;
                    Point3d circleCenter = new Point3d(bottomPoint.X, bottomPoint.Y - 4 * scale, 0);
                    double circleDiameter = 8 * scale;

                    var circle = new Circle(circleCenter, Vector3d.ZAxis, circleDiameter / 2.0)
                    {
                        Layer = LayerAxisLine
                    };
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);

                    var numText = new DBText
                    {
                        Position = circleCenter,
                        Height = 2.5 * scale,
                        TextString = axisData.SerialNumber.ToString(),
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextVerticalMid,
                        AlignmentPoint = circleCenter,
                        Layer = LayerAxisText
                    };
                    btr.AppendEntity(numText);
                    tr.AddNewlyCreatedDBObject(numText, true);

                    ExtensionDictionaryService.WriteAxisData(tr, yLine, axisData);
                    axes.Add(axisData);
                }

                tr.Commit();
            }

            return axes;
        }

        #endregion

        #region InitializeAxis — 初始化轴线（关联底座）

        /// <summary>
        /// 选择轴线，选择底座多段线，建立关联
        /// </summary>
        public static AxisData InitializeAxis(Database db, Editor ed)
        {
            var peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
            peo.SetRejectMessage("\n必须选择直线！");
            peo.AddAllowedClass(typeof(Line), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            AxisData axis = null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Line;
                if (line == null) return null;

                axis = ExtensionDictionaryService.ReadAxisData(tr, line);
                if (axis == null)
                {
                    axis = new AxisData();
                    ExtensionDictionaryService.EnsureGuid(tr, line);
                }

                // 选择底座
                var pso = new PromptSelectionOptions { MessageForAdding = "\n请选择多个底座 (Polyline): " };
                var filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<AND"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.LayerName, LayerBoltOutline),
                    new TypedValue((int)DxfCode.Operator, "AND>")
                };
                var selRes = ed.GetSelection(pso, new SelectionFilter(filterList));
                if (selRes.Status != PromptStatus.OK) return axis;

                foreach (SelectedObject selObj in selRes.Value)
                {
                    var pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pline == null) continue;

                    var baseData = ExtensionDictionaryService.ReadBaseData(tr, pline);
                    if (baseData == null)
                    {
                        baseData = new BaseData();
                        pline.UpgradeOpen();
                        var plineGuid = ExtensionDictionaryService.EnsureGuid(tr, pline);
                        baseData.Id = plineGuid;
                        ExtensionDictionaryService.WriteBaseData(tr, pline, baseData);
                    }

                    axis.Bases.Add(baseData);
                }

                line.UpgradeOpen();
                ExtensionDictionaryService.WriteAxisData(tr, line, axis);
                tr.Commit();
            }

            return axis;
        }

        #endregion

        #region DisplayStructure — 显示/高亮轴线结构

        /// <summary>
        /// 选择轴线并高亮显示其关联的底座和螺栓
        /// </summary>
        public static void DisplayStructure(Database db, Editor ed)
        {
            var axis = GetAxisFromLine(db, ed);
            if (axis == null)
            {
                ed.WriteMessage("\n无法读取轴线数据或直线没有附加数据！");
                return;
            }

            if (axis.Bases == null || axis.Bases.Count == 0)
            {
                ed.WriteMessage("\nAxisData 的 Bases 为空，无法高亮显示！");
                return;
            }

            var guidEntityMap = BuildGuidEntityMap(db);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var baseData in axis.Bases)
                {
                    // 高亮底座多段线
                    if (guidEntityMap.TryGetValue(baseData.Id, out ObjectId polyId))
                    {
                        var pline = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                        if (pline != null)
                        {
                            try { pline.UpgradeOpen(); pline.Highlight(); }
                            catch (System.Exception ex) { ed.WriteMessage($"\n高亮多段线失败: {ex.Message}"); }
                        }
                    }
                    else
                    {
                        ed.WriteMessage($"\n未找到多段线，Guid: {baseData.Id}");
                    }

                    // 高亮螺栓圆
                    foreach (var boltId in baseData.BoltIds)
                    {
                        if (guidEntityMap.TryGetValue(boltId, out ObjectId circleObjId))
                        {
                            var circle = tr.GetObject(circleObjId, OpenMode.ForRead) as Circle;
                            if (circle != null)
                            {
                                try { circle.UpgradeOpen(); circle.Highlight(); }
                                catch (System.Exception ex) { ed.WriteMessage($"\n高亮螺栓圆失败: {ex.Message}"); }
                            }
                        }
                    }
                }

                tr.Commit();
            }

            ed.UpdateScreen();
            ed.WriteMessage("\n结构高亮显示完成！");
        }

        #endregion

        #region CreateAxisTable — 创建轴线坐标表格

        /// <summary>
        /// 选择轴线，为每个底座的螺栓创建 XY 相对坐标表格
        /// </summary>
        public static void CreateAxisTable(Database db, Editor ed)
        {
            var axis = GetAxisFromLine(db, ed);
            if (axis == null)
            {
                ed.WriteMessage("\n未选择轴线或轴线数据无效！");
                return;
            }

            if (axis.Bases == null || axis.Bases.Count == 0)
            {
                ed.WriteMessage("\nAxisData 的 Bases 为空，无法创建表格！");
                return;
            }

            double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;
            var guidEntityMap = BuildGuidEntityMap(db);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                foreach (var baseData in axis.Bases)
                {
                    // 找到底座多段线
                    Polyline pline = null;
                    if (guidEntityMap.TryGetValue(baseData.Id, out ObjectId polyId))
                        pline = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;

                    if (pline == null)
                    {
                        ed.WriteMessage($"\n未找到 Polyline (Guid: {baseData.Id})，跳过。");
                        continue;
                    }

                    Point3d bottomLeft = GetBottomLeftPoint(pline);
                    Point3d tablePosition = new Point3d(bottomLeft.X - 5 * scale, bottomLeft.Y - 5 * scale, 0);

                    // 收集螺栓圆
                    var circles = new List<(Circle Circle, int SerialNumber)>();
                    for (int i = 0; i < baseData.BoltIds.Count; i++)
                    {
                        if (guidEntityMap.TryGetValue(baseData.BoltIds[i], out ObjectId circleObjId))
                        {
                            var circle = tr.GetObject(circleObjId, OpenMode.ForRead) as Circle;
                            if (circle != null)
                                circles.Add((circle, i + 1));
                        }
                    }

                    if (circles.Count == 0) continue;

                    // 创建表格
                    var table = new Table();
                    table.Position = tablePosition;
                    table.SetSize(circles.Count + 1, 3);

                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            try
                            {
                                var range = table.Cells[r, c].GetMergeRange();
                                if (range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn)
                                    table.UnmergeCells(range);
                            }
                            catch
                            {
                                // 仅用于消除默认标题行自动合并。
                            }
                        }
                    }

                    // 表头行
                    table.Rows[0].TextHeight = 2.5 * scale;
                    table.Rows[0].Height = 2.5 * scale;

                    for (int i = 1; i < table.Rows.Count; i++)
                    {
                        table.Rows[i].TextHeight = 2.5 * scale;
                        table.Rows[i].Height = 2.5 * scale;
                    }

                    table.Cells[0, 0].TextString = "序号";
                    table.Cells[0, 1].TextString = "X";
                    table.Cells[0, 2].TextString = "Y";
                    table.Cells[0, -1].BackgroundColor = Color.FromColorIndex(ColorMethod.ByAci, 251);
                    table.Cells[0, -1].Alignment = CellAlignment.MiddleCenter;

                    // 数据行
                    for (int i = 0; i < circles.Count; i++)
                    {
                        var (circle, serialNumber) = circles[i];
                        double relX = circle.Center.X - axis.IntersectionPoint.X;
                        double relY = circle.Center.Y - axis.IntersectionPoint.Y;

                        table.Cells[i + 1, 0].TextString = serialNumber.ToString();
                        table.Cells[i + 1, 1].TextString = relX.ToString("F0");
                        table.Cells[i + 1, 2].TextString = relY.ToString("F0");
                        table.Cells[i + 1, -1].Alignment = CellAlignment.MiddleCenter;
                    }

                    table.SetRowHeight(5 * scale);
                    table.Columns[0].Width = 300;
                    table.Columns[1].Width = 400;
                    table.Columns[2].Width = 400;
                    table.Layer = LayerTable;
                    table.GenerateLayout();

                    btr.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                }

                tr.Commit();
            }

            ed.Regen();
            ed.WriteMessage("\n轴线表格创建完成！");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 选择轴线直线并读取 AxisData
        /// </summary>
        public static AxisData GetAxisFromLine(Database db, Editor ed)
        {
            var peo = new PromptEntityOptions("\n请选择一条轴线（直线）: ");
            peo.SetRejectMessage("\n必须选择直线！");
            peo.AddAllowedClass(typeof(Line), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
                if (line == null) return null;
                return ExtensionDictionaryService.ReadAxisData(tr, line);
            }
        }

        /// <summary>
        /// 构建 Guid → ObjectId 映射（遍历模型空间）
        /// </summary>
        private static Dictionary<Guid, ObjectId> BuildGuidEntityMap(Database db, Transaction externalTr = null)
        {
            var map = new Dictionary<Guid, ObjectId>();

            void Process(Transaction tr)
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                foreach (ObjectId objId in btr)
                {
                    var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        var guid = ExtensionDictionaryService.ReadGuid(tr, entity);
                        if (guid != Guid.Empty)
                            map[guid] = objId;
                    }
                }
            }

            if (externalTr != null)
            {
                Process(externalTr);
            }
            else
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Process(tr);
                }
            }

            return map;
        }

        /// <summary>
        /// 判断点是否在多段线内部（射线法）
        /// </summary>
        private static bool IsPointInside(Polyline pline, Point3d point)
        {
            int intersections = 0;
            int nvert = pline.NumberOfVertices;
            for (int i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                Point3d pi = pline.GetPoint3dAt(i);
                Point3d pj = pline.GetPoint3dAt(j);
                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    intersections++;
                }
            }
            return (intersections % 2) == 1;
        }

        /// <summary>
        /// 两直线交点
        /// </summary>
        private static Point3d? GetIntersectionPoint(Line line1, Line line2)
        {
            try
            {
                var intersections = new Point3dCollection();
                line1.IntersectWith(line2, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
                return intersections.Count > 0 ? intersections[0] : (Point3d?)null;
            }
            catch { return null; }
        }

        /// <summary>
        /// 匹配直线与最近文字
        /// </summary>
        private static Dictionary<Line, DBText> MatchLinesWithTexts(List<Line> lines, List<DBText> texts, double scale)
        {
            var pairs = new Dictionary<Line, DBText>();
            double threshold = 2.5 * scale;

            foreach (var line in lines)
            {
                DBText closest = null;
                double minDist = double.MaxValue;

                foreach (var text in texts)
                {
                    if (pairs.ContainsValue(text)) continue;
                    Point3d closestPt = line.GetClosestPointTo(text.Position, false);
                    double dist = text.Position.DistanceTo(closestPt);
                    if (dist < minDist && dist <= threshold)
                    {
                        minDist = dist;
                        closest = text;
                    }
                }

                if (closest != null)
                    pairs[line] = closest;
            }

            return pairs;
        }

        /// <summary>
        /// 获取多段线左下角点
        /// </summary>
        private static Point3d GetBottomLeftPoint(Polyline pline)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                var v = pline.GetPoint3dAt(i);
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
            }
            return new Point3d(minX, minY, 0);
        }

        #endregion
    }
}
