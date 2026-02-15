using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Presentation.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 设计说明排版服务 v4：每栏独立 MText，各自独立宽度
    /// </summary>
    public class DesignSpecService
    {
        private const string LAYER_TEXT = "00_hy_1公共_文字";
        private const string XREC_KEY_MD = "HyDesignSpec_MD";
        private const string XREC_KEY_CFG = "HyDesignSpec_CFG";
        private const string XREC_KEY_GROUP = "HyDesignSpec_Group";

        /// <summary>
        /// 插入多个独立 MText（每栏一个），横向排列
        /// </summary>
        /// <param name="columnContents">每栏的 MText 内容数组</param>
        /// <param name="markdownSource">完整 Markdown 源码</param>
        /// <param name="config">配置</param>
        /// <param name="insertionPoint">左上角插入点</param>
        public void Insert(
            string[] columnContents,
            string markdownSource,
            DesignSpecConfig config,
            Point3d insertionPoint,
            string[] columnMarkdowns = null,
            LayoutResultModel layoutResult = null)
        {
            if (columnContents == null || columnContents.Length == 0)
                throw new ArgumentException("MText 内容不能为空");

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = CalculateArea(config, layoutResult);
            var ed = doc.Editor;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);

                    // 生成唯一组ID，标识这组 MText 属于同一个设计说明
                    string groupId = Guid.NewGuid().ToString("N");

                    double xOffset = 0;
                    int mtextCount = 0;
                    int tableCount = 0;
                    bool metadataWritten = false;

                    for (int i = 0; i < area.ColumnCount; i++)
                    {
                        string content = (i < columnContents.Length && !string.IsNullOrWhiteSpace(columnContents[i]))
                            ? columnContents[i]
                            : "";
                        string columnMarkdown = (columnMarkdowns != null && i < columnMarkdowns.Length)
                            ? (columnMarkdowns[i] ?? "")
                            : "";
                        double colWidth = area.ColumnWidths[i];
                        double colLeftX = insertionPoint.X + xOffset;

                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var mtext = new MText();
                            mtext.SetDatabaseDefaults();
                            mtext.Location = new Point3d(colLeftX, insertionPoint.Y, insertionPoint.Z);
                            mtext.Attachment = AttachmentPoint.TopLeft;
                            mtext.TextStyleId = textStyleId;
                            mtext.TextHeight = config.ActualTextHeight;
                            mtext.LineSpacingStyle = LineSpacingStyle.Exactly;
                            mtext.LineSpacingFactor = config.LineSpacingFactor;
                            mtext.Width = colWidth;
                            mtext.Contents = content;

                            SetLayer(db, tr, mtext, LAYER_TEXT);
                            btr.AppendEntity(mtext);
                            tr.AddNewlyCreatedDBObject(mtext, true);

                            WriteMetadataIfNeeded(tr, mtext, markdownSource, config, ref metadataWritten);
                            ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                            mtextCount++;
                        }

                        tableCount += InsertTablesForColumn(
                            tr,
                            btr,
                            db,
                            config,
                            columnMarkdown,
                            colLeftX,
                            insertionPoint.Y - area.TotalHeight - config.ActualTextHeight,
                            insertionPoint.Z,
                            colWidth,
                            groupId,
                            markdownSource,
                            ref metadataWritten);

                        xOffset += colWidth + area.ColumnGutter;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n已插入设计说明：MText={mtextCount}, Table={tableCount}（{area.ColumnCount}栏）");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n插入失败: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 更新已有 MText 组
        /// </summary>
        public void Update(
            ObjectId anchorEntityId,
            string[] columnContents,
            string markdownSource,
            DesignSpecConfig config,
            string[] columnMarkdowns = null,
            LayoutResultModel layoutResult = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = CalculateArea(config, layoutResult);
            var ed = doc.Editor;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 读取组ID，找到同组所有实体
                    var anchorEntity = tr.GetObject(anchorEntityId, OpenMode.ForRead) as Entity;
                    if (anchorEntity == null) throw new InvalidOperationException("选中实体无效");

                    string groupId = ExtensionDictionaryService.ReadLongString(tr, anchorEntity, XREC_KEY_GROUP);

                    // 删除旧的同组实体（MText + Table）
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        var toDelete = new List<ObjectId>();

                        foreach (ObjectId id in btr)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;
                            string gid = ExtensionDictionaryService.ReadLongString(tr, ent, XREC_KEY_GROUP);
                            if (gid == groupId)
                                toDelete.Add(id);
                        }

                        foreach (var id in toDelete)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForWrite);
                            ent.Erase();
                        }
                    }

                    // 重新插入，锚点使用用户选择实体的位置
                    var insertPt = GetAnchorPoint(anchorEntity);

                    var bt2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr2 = (BlockTableRecord)tr.GetObject(bt2[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);
                    string newGroupId = Guid.NewGuid().ToString("N");
                    bool metadataWritten = false;
                    int mtextCount = 0;
                    int tableCount = 0;

                    double xOffset = 0;
                    for (int i = 0; i < area.ColumnCount; i++)
                    {
                        string content = (i < columnContents.Length && !string.IsNullOrWhiteSpace(columnContents[i]))
                            ? columnContents[i] : "";
                        string columnMarkdown = (columnMarkdowns != null && i < columnMarkdowns.Length)
                            ? (columnMarkdowns[i] ?? "")
                            : "";
                        double colWidth = area.ColumnWidths[i];
                        double colLeftX = insertPt.X + xOffset;

                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var newMtext = new MText();
                            newMtext.SetDatabaseDefaults();
                            newMtext.Location = new Point3d(colLeftX, insertPt.Y, insertPt.Z);
                            newMtext.Attachment = AttachmentPoint.TopLeft;
                            newMtext.TextStyleId = textStyleId;
                            newMtext.TextHeight = config.ActualTextHeight;
                            newMtext.LineSpacingStyle = LineSpacingStyle.Exactly;
                            newMtext.LineSpacingFactor = config.LineSpacingFactor;
                            newMtext.Width = colWidth;
                            newMtext.Contents = content;

                            SetLayer(db, tr, newMtext, LAYER_TEXT);
                            btr2.AppendEntity(newMtext);
                            tr.AddNewlyCreatedDBObject(newMtext, true);

                            WriteMetadataIfNeeded(tr, newMtext, markdownSource, config, ref metadataWritten);
                            ExtensionDictionaryService.WriteLongString(tr, newMtext, newGroupId, XREC_KEY_GROUP);
                            mtextCount++;
                        }

                        tableCount += InsertTablesForColumn(
                            tr,
                            btr2,
                            db,
                            config,
                            columnMarkdown,
                            colLeftX,
                            insertPt.Y - area.TotalHeight - config.ActualTextHeight,
                            insertPt.Z,
                            colWidth,
                            newGroupId,
                            markdownSource,
                            ref metadataWritten);

                        xOffset += colWidth + area.ColumnGutter;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n已更新设计说明：MText={mtextCount}, Table={tableCount}（{area.ColumnCount}栏）");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n更新失败: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 从 MText 的 XRecord 读取 Markdown 源码
        /// </summary>
        public static string ReadMarkdownSource(Transaction tr, Entity entity)
        {
            return ExtensionDictionaryService.ReadLongString(tr, entity, XREC_KEY_MD);
        }

        /// <summary>
        /// 从 MText 的 XRecord 读取配置
        /// </summary>
        public static DesignSpecConfig ReadConfig(Transaction tr, Entity entity)
        {
            string json = ExtensionDictionaryService.ReadLongString(tr, entity, XREC_KEY_CFG);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<DesignSpecConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 判断 MText 是否含 Markdown 源码
        /// </summary>
        public static bool HasMarkdownSource(Transaction tr, Entity entity)
        {
            return ExtensionDictionaryService.HasKey(tr, entity, XREC_KEY_MD);
        }

        private static void WriteMetadataIfNeeded(
            Transaction tr,
            Entity entity,
            string markdownSource,
            DesignSpecConfig config,
            ref bool metadataWritten)
        {
            if (metadataWritten || entity == null)
                return;

            ExtensionDictionaryService.WriteLongString(tr, entity, markdownSource ?? "", XREC_KEY_MD);
            string cfgJson = JsonConvert.SerializeObject(config);
            ExtensionDictionaryService.WriteLongString(tr, entity, cfgJson, XREC_KEY_CFG);
            metadataWritten = true;
        }

        private int InsertTablesForColumn(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            string columnMarkdown,
            double columnLeftX,
            double startTopY,
            double z,
            double columnWidth,
            string groupId,
            string markdownSource,
            ref bool metadataWritten)
        {
            if (string.IsNullOrWhiteSpace(columnMarkdown))
                return 0;

            var tables = MarkdownTableExtractor.ExtractTopLevelTables(columnMarkdown);
            if (tables == null || tables.Count == 0)
                return 0;

            int created = 0;
            double y = startTopY;
            foreach (var tableData in tables)
            {
                int rows = tableData.Rows.Count;
                int cols = tableData.ColumnCount;
                if (rows <= 0 || cols <= 0)
                    continue;

                var table = new Table();
                table.SetDatabaseDefaults();
                table.TableStyle = db.Tablestyle;
                table.Position = new Point3d(columnLeftX, y, z);
                table.SetSize(rows, cols);

                double rowHeight = Math.Max(config.ActualTextHeight * config.LineSpacingFactor * 1.3, config.ActualTextHeight);
                for (int r = 0; r < rows; r++)
                {
                    table.Rows[r].Height = rowHeight;
                    table.Rows[r].TextHeight = config.ActualTextHeight;
                }

                double colWidth = Math.Max(config.ActualTextHeight * 3.0, columnWidth / cols);
                for (int c = 0; c < cols; c++)
                    table.Columns[c].Width = colWidth;

                for (int r = 0; r < rows; r++)
                {
                    var row = tableData.Rows[r];
                    for (int c = 0; c < cols; c++)
                    {
                        table.Cells[r, c].TextString = c < row.Count ? row[c] : "";
                    }
                }

                table.GenerateLayout();
                SetLayer(db, tr, table, LAYER_TEXT);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                WriteMetadataIfNeeded(tr, table, markdownSource, config, ref metadataWritten);
                ExtensionDictionaryService.WriteLongString(tr, table, groupId, XREC_KEY_GROUP);

                double tableHeight = table.Height > 0 ? table.Height : rowHeight * rows;
                y -= tableHeight + config.ActualTextHeight;
                created++;
            }

            return created;
        }

        private ObjectId EnsureTextStyle(Database db, Transaction tr, DesignSpecConfig config)
        {
            string styleName = $"0_Hy_{config.Scale}";

            var vm = SettingsPanelViewModel.Current;
            if (vm != null) styleName = vm.TextStyleName;

            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(styleName))
            {
                var rec = (TextStyleTableRecord)tr.GetObject(tst[styleName], OpenMode.ForWrite);
                rec.TextSize = 0;
                rec.XScale = config.TextXScale;
                return tst[styleName];
            }

            tst.UpgradeOpen();
            var newRec = new TextStyleTableRecord
            {
                Name = styleName,
                FileName = config.FontFileName,
                BigFontFileName = config.BigFontFileName,
                TextSize = 0,
                XScale = config.TextXScale
            };
            tst.Add(newRec);
            tr.AddNewlyCreatedDBObject(newRec, true);
            return newRec.ObjectId;
        }

        private void SetLayer(Database db, Transaction tr, Entity entity, string layerName)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName))
                entity.LayerId = lt[layerName];
        }

        private static Point3d GetAnchorPoint(Entity entity)
        {
            if (entity is MText mt)
                return mt.Location;

            if (entity is Table table)
                return table.Position;

            if (entity is BlockReference br)
                return br.Position;

            try
            {
                var ext = entity.GeometricExtents;
                return new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z);
            }
            catch
            {
                return Point3d.Origin;
            }
        }

        private static TextAreaCalculator.TextAreaResult CalculateArea(DesignSpecConfig config, LayoutResultModel layoutResult)
        {
            if (layoutResult?.ColumnWidthsMm != null && layoutResult.ColumnWidthsMm.Length > 0)
            {
                var widths = layoutResult.ColumnWidthsMm
                    .Where(w => w > 0)
                    .ToArray();
                if (widths.Length > 0)
                {
                    int cols = widths.Length;
                    double gutter = config.ActualColumnGutter;
                    return new TextAreaCalculator.TextAreaResult
                    {
                        ColumnCount = cols,
                        ColumnWidths = widths,
                        ColumnGutter = gutter,
                        TotalHeight = config.ActualTotalHeight,
                        TotalWidth = widths.Sum() + gutter * Math.Max(0, cols - 1)
                    };
                }
            }

            return TextAreaCalculator.Calculate(config);
        }
    }
}
