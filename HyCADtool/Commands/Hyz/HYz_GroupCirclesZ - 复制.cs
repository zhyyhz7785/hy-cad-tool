using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("HYz_GroupCirclesZFinal")]
        public static void GroupCirclesZFinal()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            double scale = HyCADTool.Config.BaseConfig.Scale > 0 ? HyCADTool.Config.BaseConfig.Scale : 100;

            // 提示用户是否在引线中标注标高
            var pko = new PromptKeywordOptions("\n是否在引线中标注标高？ [是(Y)/否(N)]: ", "是 否")
            {
                AllowNone = false
            };
            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK) return;
            bool includeElevation = pkr.StringResult == "是";

            // 获取图形
            var filter = new[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            var psr = ed.GetSelection(new SelectionFilter(filter));
            if (psr.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                var circles = new List<Circle>();
                var texts = new List<(string Content, Point3d Position)>();

                foreach (SelectedObject sel in psr.Value)
                {
                    if (sel == null || sel.ObjectId.IsNull) continue;
                    var ent = tr.GetObject(sel.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent is Circle c) circles.Add(c);
                    else if (ent is DBText t) texts.Add((t.TextString, t.Position));
                    else if (ent is MText mt) texts.Add((mt.Text, mt.Location));
                }

                var items = new List<(Circle circle, string text, double elevation, bool hasText)>();
                foreach (var circle in circles)
                {
                    var nearest = texts.OrderBy(t => t.Position.DistanceTo(circle.Center)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(nearest.Content) && double.TryParse(nearest.Content, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        items.Add((circle, nearest.Content, Math.Round(val + 0.050, 3), true));
                    }
                    else
                    {
                        // 没有文字或文字无法解析为数字，标高设为 0，标记为无文字
                        items.Add((circle, "", 0.0, false));
                    }
                }

                if (items.Count == 0) return;

                // elevation 分组
                var elevationGroups = items.Where(i => i.hasText).GroupBy(i => i.elevation).ToList();
                var noTextCircles = items.Where(i => !i.hasText).ToList();

                // 按最小 X → Y 排序 → 编号 A, B, C
                var orderedGroups = elevationGroups.Select(g =>
                {
                    var minPt = g.Select(i => i.circle.Center).OrderBy(p => p.X).ThenBy(p => p.Y).First();
                    return new { Elevation = g.Key, Items = g.ToList(), KeyPoint = minPt };
                })
                .OrderBy(g => g.KeyPoint.X)
                .ThenBy(g => g.KeyPoint.Y)
                .Select((g, index) => new { Code = ((char)('A' + index)).ToString(), g.Elevation, g.Items })
                .ToList();

                // 色彩函数（蓝→红）
                double minElev = orderedGroups.Any() ? orderedGroups.Min(g => g.Elevation) : 0;
                double maxElev = orderedGroups.Any() ? orderedGroups.Max(g => g.Elevation) : 0;
                Func<double, short> getColor = elev =>
                {
                    if (Math.Abs(maxElev - minElev) < 1e-6) return 7;
                    double t = (elev - minElev) / (maxElev - minElev);
                    return (short)(160 - t * 159);
                };

                // 图层定义 + 圆图层修改
                var elevToCode = new Dictionary<double, string>();
                var codeToElevation = new Dictionary<string, double>();
                foreach (var g in orderedGroups)
                {
                    string layerName = $"00_hy_Z_{g.Code}";
                    short aciColor = getColor(g.Elevation);

                    // 创建图层
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

                    elevToCode[g.Elevation] = g.Code;
                    codeToElevation[g.Code] = g.Elevation;

                    foreach (var item in g.Items)
                    {
                        item.circle.UpgradeOpen();
                        item.circle.Layer = layerName;
                    }
                }

                // 为无文字的圆创建一个默认图层
                string noTextLayerName = "00_hy_Z_NoText";
                if (noTextCircles.Any() && !lt.Has(noTextLayerName))
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord
                    {
                        Name = noTextLayerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 7) // 默认白色
                    };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }

                // 创建引线图层
                string mleaderLayerName = "00_hy_3公共_标注3_引线";
                if (!lt.Has(mleaderLayerName))
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord
                    {
                        Name = mleaderLayerName,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
                    };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }

                // 编号顺序：所有圆按 XY 排序 → A1, B1, ... 或 1, 2, ...
                var orderedItems = items.OrderBy(i => i.circle.Center.X).ThenBy(i => i.circle.Center.Y).ToList();
                var countPerCode = new Dictionary<string, int>();
                int globalCount = 0; // 用于无文字圆的全局编号

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
                        // 无文字时使用全局数字编号
                        globalCount++;
                        label = globalCount.ToString();
                    }

                    string content = item.hasText && includeElevation ? $"{label}\\P标高 = {item.elevation:F3}" : label;

                    var pt = item.circle.Center;
                    var pt2 = new Point3d(pt.X + 5 * scale, pt.Y + 5 * scale, pt.Z);

                    var mleader = EtGpt.AddMleaderSinglePoint(pt, pt2, content);
                    mleader.Layer = mleaderLayerName;
                    ms.AppendEntity(mleader);
                    tr.AddNewlyCreatedDBObject(mleader, true);

                    // 设置圆的图层
                    item.circle.UpgradeOpen();
                    item.circle.Layer = item.hasText ? $"00_hy_Z_{elevToCode[item.elevation]}" : noTextLayerName;
                }

                // 表格
                Table table = new Table();
                table.TableStyle = db.Tablestyle;
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
        }
    }
}