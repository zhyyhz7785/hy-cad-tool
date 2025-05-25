using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.Models;
using HyCADTool.Tools;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hyabCT_CreateAnchorBoltTable")]
        public static void CreateAnchorBoltTable()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                BaseConfig.InitializeStyle(); // 初始化样式
                // 选择螺栓对象
                var boltIds = SelectBoltObjects(ed);
                if (boltIds == null) return;
                // 获取螺栓数据
                var (boltData, boltTypeCounts) = GetBoltData(db, boltIds, ed);
                if (boltData.Count == 0) return;
                // 获取表格插入点
                Point3d? insertPoint = GetTableInsertPoint(ed);
                if (!insertPoint.HasValue) return;
                // 创建并填充表格
                CreateAndPopulateTable(db, ed, boltData, boltTypeCounts, insertPoint.Value);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
                throw;
            }
        }
        // 1. 选择螺栓对象
        private static ObjectId[] SelectBoltObjects(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择螺栓圆或块: " };
            TypedValue[] filter = { new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT") };
            SelectionFilter sf = new SelectionFilter(filter);
            PromptSelectionResult selRes = ed.GetSelection(selOpts, sf);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n选择已取消。");
                return null;
            }
            return selRes.Value.GetObjectIds();
        }
        // 2. 获取螺栓数据和统计
        private static (List<(Point3d Position, AnchorBolt Bolt)>, Dictionary<string, int>) GetBoltData(Database db, ObjectId[] boltIds, Editor ed)
        {
            var boltData = new List<(Point3d, AnchorBolt)>();
            var boltTypeCounts = new Dictionary<string, int>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in boltIds)
                {
                    Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (ent == null || !ent.ExtensionDictionary.IsValid) continue;
                    DBDictionary extDict = tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
                    if (!extDict.Contains("AnchorBolt")) continue;
                    Xrecord xRec = tr.GetObject(extDict.GetAt("AnchorBolt"), OpenMode.ForRead) as Xrecord;
                    string jsonData = xRec.Data.AsArray()[0].Value.ToString();
                    AnchorBolt bolt = JsonConvert.DeserializeObject<AnchorBolt>(jsonData);
                    if (bolt == null) continue;
                    Point3d position = ent is Circle circle ? circle.Center : (ent as BlockReference).Position;
                    boltData.Add((position, bolt));
                    // 修正：使用 ContainsKey 检查键是否存在
                    if (!boltTypeCounts.ContainsKey(bolt.Model))
                    {
                        boltTypeCounts[bolt.Model] = 0;
                    }
                    boltTypeCounts[bolt.Model]++;
                }
                tr.Commit();
            }
            if (boltData.Count == 0)
            {
                ed.WriteMessage("\n错误: 未找到有效的螺栓数据。");
            }
            else
            {
                ed.WriteMessage($"\n找到 {boltData.Count} 个有效螺栓对象，型号数: {boltTypeCounts.Count}");
            }
            return (boltData, boltTypeCounts);
        }
        // 3. 获取表格插入点
        private static Point3d? GetTableInsertPoint(Editor ed)
        {
            PromptPointOptions ppo = new PromptPointOptions("\n指定表格插入点: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n插入点选择已取消。");
                return null;
            }
            return ppr.Value;
        }
        // 4. 创建并填充表格
        private static void CreateAndPopulateTable(Database db, Editor ed, List<(Point3d Position, AnchorBolt Bolt)> boltData,
            Dictionary<string, int> boltTypeCounts, Point3d insertPoint)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                Table table = InitializeTable(db, insertPoint, boltData.Count, boltTypeCounts.Count);
                table.Layer = "00_hy_4公共_表格";
                PopulateTableContent(table, boltData, boltTypeCounts, ed);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);
                tr.Commit();
            }
            ed.WriteMessage($"\n成功创建包含 {boltData.Count} 个螺栓的表格！");
        }
        // 5. 初始化表格结构
        private static Table InitializeTable(Database db, Point3d position, int boltCount, int typeCount)
        {
            Table table = new Table
            {
                Position = position,
                TableStyle = db.Tablestyle
            };
            int totalRows = 1 + 1 + typeCount + 1 + boltCount; // 标题 + 统计标题 + 统计行 + 表头 + 数据行
            int totalCols = 14; // 型号 + 11属性 + X/Y坐标 + 数量
            table.SetSize(totalRows, totalCols);
            double textHeight = Tools.Tools.TextStyleConfig.TextSize * BaseConfig.Scale;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                table.Rows[i].Height = 1.5 * textHeight;
                table.Rows[i].TextHeight = textHeight;
            }
            for (int j = 0; j < table.Columns.Count; j++)
            {
                table.Columns[j].Width = 3 * textHeight;
            }
            if (BaseConfig.TextStyleId != ObjectId.Null)
            {
                for (int i = 0; i < table.Rows.Count; i++)
                    for (int j = 0; j < table.Columns.Count; j++)
                        table.Cells[i, j].TextStyleId = BaseConfig.TextStyleId;
            }
            else
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n警告: 文字样式未初始化，使用默认样式。");
            }
            return table;
        }
        // 6. 填充表格内容
        private static void PopulateTableContent(Table table, List<(Point3d Position, AnchorBolt Bolt)> boltData,
            Dictionary<string, int> boltTypeCounts, Editor ed)
        {
            // 填充标题
            table.Cells[0, 0].TextString = "地脚螺栓规格表";
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.MergeCells(CellRange.Create(table, 0, 0, 0, 13));
            // 统计标题
            table.Cells[1, 0].TextString = "螺栓类型统计";
            table.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;
            table.MergeCells(CellRange.Create(table, 1, 0, 1, 13));
            // 统计详情
            int rowIndex = 2;
            foreach (var typeCount in boltTypeCounts)
            {
                table.Cells[rowIndex, 0].TextString = $"螺栓型号 {typeCount.Key} - 数量: {typeCount.Value} 个";
                table.Cells[rowIndex, 0].Alignment = CellAlignment.MiddleCenter;
                table.MergeCells(CellRange.Create(table, rowIndex, 0, rowIndex, 13));
                rowIndex++;
            }
            // 表头
            string[] headers = { "型号", "螺栓孔径(mm)", "螺栓直径(mm)", "螺帽总长(mm)", "螺栓长度(mm)", "栓底间距(mm)",
                "开孔直径(mm)", "垫层厚度(mm)", "垫板厚度(mm)", "开孔深度(mm)", "丝长(mm)", "X坐标(mm)", "Y坐标(mm)", "数量" };
            for (int j = 0; j < headers.Length; j++)
                table.Cells[rowIndex, j].TextString = headers[j];
            rowIndex++;
            // 数据
            var groupedBoltData = boltData.GroupBy(b => b.Bolt.Model);
            foreach (var group in groupedBoltData)
            {
                var boltsOfType = group.ToList();
                for (int i = 0; i < boltsOfType.Count; i++)
                {
                    var (pos, b) = boltsOfType[i];
                    table.Cells[rowIndex, 0].TextString = b.Model;
                    table.Cells[rowIndex, 1].TextString = b.D.ToString();
                    table.Cells[rowIndex, 2].TextString = b.D1.ToString();
                    table.Cells[rowIndex, 3].TextString = b.V.ToString();
                    table.Cells[rowIndex, 4].TextString = b.H1.ToString();
                    table.Cells[rowIndex, 5].TextString = b.H2.ToString();
                    table.Cells[rowIndex, 6].TextString = b.E.ToString();
                    table.Cells[rowIndex, 7].TextString = b.G.ToString();
                    table.Cells[rowIndex, 8].TextString = b.A.ToString();
                    table.Cells[rowIndex, 9].TextString = b.NutHeight.ToString();
                    table.Cells[rowIndex, 10].TextString = b.BoltLength.ToString();
                    table.Cells[rowIndex, 11].TextString = pos.X.ToString("F2");
                    table.Cells[rowIndex, 12].TextString = pos.Y.ToString("F2");
                    if (i == 0) table.Cells[rowIndex, 13].TextString = boltsOfType.Count.ToString();
                    rowIndex++;
                }
            }
            // 设置居中对齐
            for (int i = 1; i < table.Rows.Count; i++)
                for (int j = 0; j < table.Columns.Count; j++)
                    table.Cells[i, j].Alignment = CellAlignment.MiddleCenter;
        }
    }
}