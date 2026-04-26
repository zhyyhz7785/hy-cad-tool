using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.EquipmentFoundation.Domain.Entities;
using HyCADTool.Presentation.ViewModels;
using Newtonsoft.Json;
using AnchorBoltEntity = HyCADTool.Features.EquipmentFoundation.Domain.Entities.AnchorBolt;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.AnchorBolt
{
    /// <summary>
    /// 螺栓规格表命令（对应旧命令 hyabCT_CreateAnchorBoltTable）
    /// 流程：选螺栓 → 读取数据 → 插入表格
    /// </summary>
    public class AnchorBoltTableCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 选择螺栓
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择螺栓圆或块: " };
                var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT") });
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                // 读取数据
                var boltData = new List<(Point3d Pos, AnchorBoltEntity Bolt)>();
                var typeCounts = new Dictionary<string, int>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null || !ent.ExtensionDictionary.IsValid) continue;

                        var extDict = tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                        if (!extDict.Contains("AnchorBolt")) continue;

                        var xRec = tr.GetObject(extDict.GetAt("AnchorBolt"), OpenMode.ForRead) as Xrecord;
                        string json = xRec.Data.AsArray()[0].Value.ToString();
                        var bolt = JsonConvert.DeserializeObject<AnchorBoltEntity>(json);
                        if (bolt == null) continue;

                        Point3d pos = ent is Circle c ? c.Center : ((BlockReference)ent).Position;
                        boltData.Add((pos, bolt));

                        if (!typeCounts.ContainsKey(bolt.Model)) typeCounts[bolt.Model] = 0;
                        typeCounts[bolt.Model]++;
                    }
                    tr.Commit();
                }

                if (boltData.Count == 0) { ed.WriteMessage("\n未找到有效螺栓数据。"); return; }

                // 插入点
                var ppr = ed.GetPoint("\n指定表格插入点: ");
                if (ppr.Status != PromptStatus.OK) return;

                var vm = SettingsPanelViewModel.Current;
                double scale = vm != null ? vm.Scale : 40.0;
                double textHeight = (vm != null ? vm.TextSize : 2.5) * scale;

                // 创建表格
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int totalRows = 1 + 1 + typeCounts.Count + 1 + boltData.Count;
                    var table = new Table { Position = ppr.Value };
                    table.SetSize(totalRows, 14);

                    for (int i = 0; i < table.Rows.Count; i++)
                    {
                        table.Rows[i].Height = 1.5 * textHeight;
                        table.Rows[i].TextHeight = textHeight;
                    }
                    for (int j = 0; j < 14; j++)
                        table.Columns[j].Width = 3 * textHeight;

                    // 标题
                    table.Cells[0, 0].TextString = "地脚螺栓规格表";
                    table.MergeCells(CellRange.Create(table, 0, 0, 0, 13));

                    // 统计
                    table.Cells[1, 0].TextString = "螺栓类型统计";
                    table.MergeCells(CellRange.Create(table, 1, 0, 1, 13));

                    int row = 2;
                    foreach (var tc in typeCounts)
                    {
                        table.Cells[row, 0].TextString = $"螺栓型号 {tc.Key} - 数量: {tc.Value} 个";
                        table.MergeCells(CellRange.Create(table, row, 0, row, 13));
                        row++;
                    }

                    // 表头
                    string[] headers = { "型号", "螺栓孔径", "螺栓直径", "螺帽总长", "螺栓长度", "栓底间距",
                        "开孔直径", "垫层厚度", "垫板厚度", "开孔深度", "丝长", "X坐标", "Y坐标", "数量" };
                    for (int j = 0; j < headers.Length; j++)
                        table.Cells[row, j].TextString = headers[j];
                    row++;

                    // 数据
                    foreach (var group in boltData.GroupBy(b => b.Bolt.Model))
                    {
                        var list = group.ToList();
                        for (int i = 0; i < list.Count; i++)
                        {
                            var (pos, b) = list[i];
                            table.Cells[row, 0].TextString = b.Model;
                            table.Cells[row, 1].TextString = b.D.ToString();
                            table.Cells[row, 2].TextString = b.D1.ToString();
                            table.Cells[row, 3].TextString = b.V.ToString();
                            table.Cells[row, 4].TextString = b.H1.ToString();
                            table.Cells[row, 5].TextString = b.H2.ToString();
                            table.Cells[row, 6].TextString = b.E.ToString();
                            table.Cells[row, 7].TextString = b.G.ToString();
                            table.Cells[row, 8].TextString = b.A.ToString();
                            table.Cells[row, 9].TextString = b.NutHeight.ToString();
                            table.Cells[row, 10].TextString = b.BoltLength.ToString();
                            table.Cells[row, 11].TextString = pos.X.ToString("F2");
                            table.Cells[row, 12].TextString = pos.Y.ToString("F2");
                            if (i == 0) table.Cells[row, 13].TextString = list.Count.ToString();
                            row++;
                        }
                    }

                    table.GenerateLayout();
                    btr.AppendEntity(table);
                    tr.AddNewlyCreatedDBObject(table, true);
                    tr.Commit();
                }
                ed.WriteMessage($"\n成功创建包含 {boltData.Count} 个螺栓的表格。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
