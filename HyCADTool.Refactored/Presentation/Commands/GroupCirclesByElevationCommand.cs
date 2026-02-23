using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 圆分组标注命令（按标高分组）
    /// 移植自 HYz_GroupCirclesZFinal
    /// 功能：选择圆和文字，按标高分组，创建图层、引线标注和统计表格
    /// </summary>
    public class GroupCirclesByElevationCommand
    {
        private const string NoTextLayerName = "00_hy_Z_NoText";
        private const string LeaderLayerName = "00_hy_3公共_标注3_引线";
        private const string ElevationTextLayerName = "00_hy_3公共_标注4_标高";

        private readonly Document _doc;
        private readonly Database _db;
        private readonly Editor _ed;

        public GroupCirclesByElevationCommand()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            _db = _doc.Database;
            _ed = _doc.Editor;
        }

        public void Execute()
        {
            try
            {
                // 获取 Scale
                double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;

                // 1. 询问是否在引线中标注标高
                var pko = new PromptKeywordOptions("\n是否在引线中标注标高？ [是(Y)/否(N)]: ", "是 否")
                {
                    AllowNone = false
                };
                var pkr = _ed.GetKeywords(pko);
                if (pkr.Status != PromptStatus.OK) return;
                bool includeElevation = pkr.StringResult == "是";

                // 2. 选择圆/闭合多段线和文字
                var filter = new[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                };
                var psr = _ed.GetSelection(new SelectionFilter(filter));
                if (psr.Status != PromptStatus.OK) return;

                int totalCount = 0;
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForWrite);
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);

                    // 3. 提取桩对象（圆 + 闭合多段线）和文字
                    var piles = new List<(Entity Entity, Point3d Anchor)>();
                    var texts = new List<(string Content, Point3d Position)>();

                    foreach (SelectedObject sel in psr.Value)
                    {
                        if (sel == null || sel.ObjectId.IsNull) continue;
                        var ent = tr.GetObject(sel.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent is Circle c)
                        {
                            piles.Add((c, c.Center));
                        }
                        else if (ent is Polyline pl && pl.Closed && pl.NumberOfVertices >= 3)
                        {
                            piles.Add((pl, GetPolylineCentroid(pl)));
                        }
                        else if (ent is DBText t) texts.Add((t.TextString, t.Position));
                        else if (ent is MText mt) texts.Add((mt.Text, mt.Location));
                    }

                    // 4. 匹配桩对象和文字，解析标高
                    var items = new List<((Entity Entity, Point3d Anchor) pile, string text, double elevation, bool hasText)>();
                    foreach (var pile in piles)
                    {
                        var nearest = texts.OrderBy(t => t.Position.DistanceTo(pile.Anchor)).FirstOrDefault();
                        if (!string.IsNullOrEmpty(nearest.Content) &&
                            double.TryParse(nearest.Content, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        {
                            items.Add((pile, nearest.Content, Math.Round(val + 0.050, 3), true));
                        }
                        else
                        {
                            items.Add((pile, "", 0.0, false));
                        }
                    }

                    if (items.Count == 0) return;

                    // 5. 按标高分组
                    var elevationGroups = items.Where(i => i.hasText).GroupBy(i => i.elevation).ToList();
                    var noTextCircles = items.Where(i => !i.hasText).ToList();

                    // 6. 按最小 X → Y 排序 → 编号 A, B, C...
                    var orderedGroups = elevationGroups.Select(g =>
                    {
                        var minPt = g.Select(i => i.pile.Anchor).OrderBy(p => p.X).ThenBy(p => p.Y).First();
                        return new { Elevation = g.Key, Items = g.ToList(), KeyPoint = minPt };
                    })
                    .OrderBy(g => g.KeyPoint.X)
                    .ThenBy(g => g.KeyPoint.Y)
                    .Select((g, index) => new { Code = ((char)('A' + index)).ToString(), g.Elevation, g.Items })
                    .ToList();

                    // 7. 色彩函数（蓝→红）
                    double minElev = orderedGroups.Any() ? orderedGroups.Min(g => g.Elevation) : 0;
                    double maxElev = orderedGroups.Any() ? orderedGroups.Max(g => g.Elevation) : 0;
                    Func<double, short> getColor = elev =>
                    {
                        if (Math.Abs(maxElev - minElev) < 1e-6) return 7;
                        double t = (elev - minElev) / (maxElev - minElev);
                        return (short)(160 - t * 159);
                    };

                    // 8. 创建图层 + 修改圆图层
                    var elevToCode = new Dictionary<double, string>();
                    var codeToElevation = new Dictionary<string, double>();

                    foreach (var g in orderedGroups)
                    {
                        string layerName = $"00_hy_Z_{g.Code}";
                        short aciColor = getColor(g.Elevation);

                        CreateLayerIfNotExists(tr, lt, layerName, aciColor);

                        elevToCode[g.Elevation] = g.Code;
                        codeToElevation[g.Code] = g.Elevation;

                        foreach (var item in g.Items)
                        {
                            item.pile.Entity.UpgradeOpen();
                            item.pile.Entity.Layer = layerName;
                        }
                    }

                    // 9. 为无文字对象创建默认图层
                    if (noTextCircles.Any())
                    {
                        CreateLayerIfNotExists(tr, lt, NoTextLayerName, 7);
                    }

                    // 10. 创建引线图层
                    CreateLayerIfNotExists(tr, lt, LeaderLayerName, 7);

                    // 11. 编号顺序：所有对象按 XY 排序 → A1, B1, ... 或 1, 2, ...
                    var orderedItems = items.OrderBy(i => i.pile.Anchor.X).ThenBy(i => i.pile.Anchor.Y).ToList();
                    totalCount = orderedItems.Count;
                    var countPerCode = new Dictionary<string, int>();
                    int globalCount = 0;

                    foreach (var item in orderedItems)
                    {
                        string label;
                        if (item.hasText)
                        {
                            string code = elevToCode[item.elevation];
                            countPerCode.TryGetValue(code, out int cnt);
                            cnt++;
                            countPerCode[code] = cnt;
                            label = $"{code}{cnt}";
                        }
                        else
                        {
                            globalCount++;
                            label = globalCount.ToString();
                        }

                        string content = item.hasText && includeElevation ? $"{label}\\P标高 = {item.elevation:F3}" : label;
                        var pt = item.pile.Anchor;
                        var pt2 = new Point3d(pt.X + 5 * scale, pt.Y + 5 * scale, pt.Z);

                        // 使用 MLeaderExtensions
                        var mleader = MLeaderExtensions.CreateMLeaderSinglePoint(pt, pt2, content);
                        mleader.Layer = LeaderLayerName;
                        ms.AppendEntity(mleader);
                        tr.AddNewlyCreatedDBObject(mleader, true);

                        // 设置对象图层
                        item.pile.Entity.UpgradeOpen();
                        item.pile.Entity.Layer = item.hasText ? $"00_hy_Z_{elevToCode[item.elevation]}" : NoTextLayerName;
                    }

                    // 12. 创建统计表格
                    Table table = new Table();
                    table.TableStyle = _db.Tablestyle;
                    table.SetSize(codeToElevation.Count + 2, 3);
                    table.SetRowHeight(2.5 * scale);
                    table.SetColumnWidth(10 * scale);

                    table.Cells[0, 0].TextString = "代号";
                    table.Cells[0, 1].TextString = "标高";
                    table.Cells[0, 2].TextString = "数量";
                    for (int i = 0; i < 3; i++) table.Cells[0, i].TextHeight = scale;

                    int row = 1;
                    foreach (var kv in codeToElevation.OrderBy(k => k.Key))
                    {
                        table.Cells[row, 0].TextString = kv.Key;
                        table.Cells[row, 1].TextString = kv.Value.ToString("F3");
                        table.Cells[row, 2].TextString = countPerCode.TryGetValue(kv.Key, out int n) ? n.ToString() : "0";
                        for (int j = 0; j < 3; j++) table.Cells[row, j].TextHeight = scale;
                        row++;
                    }

                    // 总计行
                    table.Cells[row, 0].TextString = "总计";
                    table.Cells[row, 1].TextString = "";
                    table.Cells[row, 2].TextString = orderedItems.Count.ToString();
                    for (int j = 0; j < 3; j++) table.Cells[row, j].TextHeight = scale;

                    table.Position = new Point3d(0, 0, 0);
                    ms.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);

                    tr.Commit();
                }

                _ed.WriteMessage($"\n封闭图形分组标注完成，共 {totalCount} 个对象（圆+闭合多段线）。");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从面板读取输入标高，在选中圆心/闭合多段线形心处写入文字。
        /// </summary>
        public void ExecutePlaceElevationTextAtCentroids()
        {
            try
            {
                var vm = PilePanelViewModel.Current;
                if (vm == null)
                {
                    _ed.WriteMessage("\n桩基面板未初始化。");
                    return;
                }

                double scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;
                double textHeight = vm.MarkerTextHeightScale * scale;
                double elevation = vm.MarkerElevation;
                string textContent = elevation.ToString("F3", CultureInfo.InvariantCulture);

                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                });
                var psr = _ed.GetSelection(new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择要写入标高文字的圆或闭合多段线: "
                }, filter);
                if (psr.Status != PromptStatus.OK) return;

                int createdCount = 0;
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForWrite);
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                    CreateLayerIfNotExists(tr, lt, ElevationTextLayerName, 7);

                    foreach (SelectedObject sel in psr.Value)
                    {
                        if (sel == null || sel.ObjectId.IsNull) continue;
                        var ent = tr.GetObject(sel.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        Point3d targetPoint;
                        if (ent is Circle c)
                        {
                            targetPoint = c.Center;
                        }
                        else if (ent is Polyline pl && pl.Closed && pl.NumberOfVertices >= 3)
                        {
                            targetPoint = GetPolylineCentroid(pl);
                        }
                        else
                        {
                            continue;
                        }

                        var text = new DBText
                        {
                            Position = targetPoint,
                            Height = textHeight,
                            WidthFactor = 0.7,
                            TextString = textContent,
                            Layer = ElevationTextLayerName
                        };
                        ms.AppendEntity(text);
                        tr.AddNewlyCreatedDBObject(text, true);
                        createdCount++;
                    }

                    tr.Commit();
                }

                _ed.WriteMessage($"\n已在 {createdCount} 个对象中心写入标高文字: {textContent}");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n写入标高文字失败: {ex.Message}");
            }
        }

        private void CreateLayerIfNotExists(Transaction tr, LayerTable lt, string layerName, short aciColor)
        {
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, aciColor)
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        private static Point3d GetPolylineCentroid(Polyline pline)
        {
            double area = 0;
            double cx = 0;
            double cy = 0;
            int n = pline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                var p0 = pline.GetPoint2dAt(i);
                var p1 = pline.GetPoint2dAt((i + 1) % n);
                double cross = p0.X * p1.Y - p1.X * p0.Y;
                area += cross;
                cx += (p0.X + p1.X) * cross;
                cy += (p0.Y + p1.Y) * cross;
            }
            area *= 0.5;
            if (Math.Abs(area) < 1e-10)
            {
                return pline.GetPoint3dAt(0);
            }

            return new Point3d(cx / (6 * area), cy / (6 * area), pline.Elevation);
        }
    }
}
