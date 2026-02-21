using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Presentation.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;
using LayoutPageModel = HyCADTool.TextLayout.LayoutPage;
using SharedMarkdownBlockParser = HyCADTool.TextLayout.MarkdownBlockParser;
using SharedDocumentBlock = HyCADTool.TextLayout.DocumentBlock;
using SharedDocumentBlockType = HyCADTool.TextLayout.DocumentBlockType;
using SharedColumnSplitter = HyCADTool.TextLayout.MarkdownColumnSplitter;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 设计说明排版服务 v4：每栏独立 MText，各自独立宽度
    /// </summary>
    public class DesignSpecService
    {
        private const string LAYER_TEXT = "00_hy_1公共_文字";
        private const string LAYER_FRAME = "00_hy_1公共_图框";
        private const string XREC_KEY_MD = "HyDesignSpec_MD";
        private const string XREC_KEY_CFG = "HyDesignSpec_CFG";
        private const string XREC_KEY_GROUP = "HyDesignSpec_Group";

        private sealed class TablePlacement
        {
            public int PageIndex { get; set; }
            public int ColumnIndex { get; set; }
            public int BlockIndex { get; set; }
            public double TopOffsetMm { get; set; }
            public double EstimatedHeightMm { get; set; }
            public MarkdownTableData TableData { get; set; }
        }

        /// <summary>
        /// 每页每列的 MText 内容和 Markdown 源码
        /// </summary>
        private sealed class PageColumnData
        {
            public int PageIndex { get; set; }
            public string[] ColumnContents { get; set; }
            public string[] ColumnMarkdowns { get; set; }
        }

        /// <summary>
        /// 插入多个独立 MText（每栏一个），横向排列，支持多页
        /// </summary>
        public ObjectId Insert(
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
            var allPageData = BuildAllPageColumnData(columnContents, columnMarkdowns, markdownSource, config, layoutResult);
            var tablePlacements = BuildTablePlacements(markdownSource, layoutResult, config, area.ColumnWidths);
            var ed = doc.Editor;

            ObjectId anchorEntityId = ObjectId.Null;
            double pageHeightScaled = config.PageHeightMm * config.Scale;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);

                    string groupId = Guid.NewGuid().ToString("N");
                    int mtextCount = 0;
                    int tableCount = 0;
                    bool metadataWritten = false;
                    int totalPages = allPageData.Count;

                    for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
                    {
                        var pageData = allPageData[pageIdx];
                        double pageTopY = insertionPoint.Y - pageIdx * pageHeightScaled;

                        // 绘制图框
                        DrawTitleBlock(tr, btr, db, config, insertionPoint.X, pageTopY, insertionPoint.Z, groupId);

                        double contentTopY = pageTopY - config.MarginTopMm * config.Scale;
                        double contentLeftX = insertionPoint.X + config.MarginLeftMm * config.Scale;
                        double innerPad = Math.Max(0, config.ActualColumnInnerPadding);

                        double xOffset = 0;
                        for (int i = 0; i < area.ColumnCount; i++)
                        {
                            string content = (i < pageData.ColumnContents.Length && !string.IsNullOrWhiteSpace(pageData.ColumnContents[i]))
                                ? pageData.ColumnContents[i]
                                : "";
                            string colMd = (pageData.ColumnMarkdowns != null && i < pageData.ColumnMarkdowns.Length)
                                ? (pageData.ColumnMarkdowns[i] ?? "")
                                : "";
                            double colWidth = area.ColumnWidths[i];
                            double colLeftX = contentLeftX + xOffset;
                            double textLeftX = colLeftX + innerPad;
                            double textTopY = contentTopY - innerPad;
                            double textWidth = Math.Max(config.ActualTextHeight, colWidth - innerPad * 2.0);

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                var mtext = new MText();
                                mtext.SetDatabaseDefaults();
                                mtext.Location = new Point3d(textLeftX, textTopY, insertionPoint.Z);
                                mtext.Attachment = ParseAttachmentPoint(config.MTextAttachment);
                                mtext.TextStyleId = textStyleId;
                                mtext.TextHeight = config.ActualTextHeight;
                                mtext.LineSpacingStyle = ParseLineSpacingStyle(config.MTextLineSpacingStyle);
                                mtext.LineSpacingFactor = config.LineSpacingFactor;
                                mtext.Width = textWidth;
                                mtext.Contents = content;

                                SetLayer(db, tr, mtext, LAYER_TEXT);
                                btr.AppendEntity(mtext);
                                tr.AddNewlyCreatedDBObject(mtext, true);
                                if (anchorEntityId.IsNull)
                                    anchorEntityId = mtext.ObjectId;

                                WriteMetadataIfNeeded(tr, mtext, markdownSource, config, ref metadataWritten);
                                ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                                mtextCount++;
                            }

                            tableCount += InsertTablesForColumn(
                                tr, btr, db, config, tablePlacements,
                                pageIdx, i, colMd,
                                textLeftX, textTopY, insertionPoint.Z,
                                textWidth, groupId, markdownSource,
                                textStyleId,
                                ref metadataWritten, ref anchorEntityId);

                            xOffset += colWidth + area.ColumnGutter;
                        }
                    }

                    tr.Commit();
                    string widthInfo = string.Join("+", area.ColumnWidths.Select(w => w.ToString("0.0")));
                    ed.WriteMessage($"\n已插入设计说明：MText={mtextCount}, Table={tableCount}（{totalPages}页，{area.ColumnCount}栏，栏宽={widthInfo}mm，总宽={area.TotalWidth:0.0}mm）");
                    return anchorEntityId;
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
        /// 更新已有 MText 组，支持多页
        /// </summary>
        public ObjectId Update(
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
            var allPageData = BuildAllPageColumnData(columnContents, columnMarkdowns, markdownSource, config, layoutResult);
            var tablePlacements = BuildTablePlacements(markdownSource, layoutResult, config, area.ColumnWidths);
            var ed = doc.Editor;

            ObjectId newAnchorEntityId = ObjectId.Null;
            double pageHeightScaled = config.PageHeightMm * config.Scale;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var anchorEntity = tr.GetObject(anchorEntityId, OpenMode.ForRead) as Entity;
                    if (anchorEntity == null) throw new InvalidOperationException("选中实体无效");
                    var insertPt = GetAnchorPoint(anchorEntity);

                    string groupId = ExtensionDictionaryService.ReadLongString(tr, anchorEntity, XREC_KEY_GROUP);

                    // 删除旧的同组实体
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

                    var bt2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr2 = (BlockTableRecord)tr.GetObject(bt2[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);
                    string newGroupId = Guid.NewGuid().ToString("N");
                    bool metadataWritten = false;
                    int mtextCount = 0;
                    int tableCount = 0;
                    int totalPages = allPageData.Count;

                    for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
                    {
                        var pageData = allPageData[pageIdx];
                        double pageTopY = insertPt.Y - pageIdx * pageHeightScaled;

                        DrawTitleBlock(tr, btr2, db, config, insertPt.X, pageTopY, insertPt.Z, newGroupId);

                        double contentTopY = pageTopY - config.MarginTopMm * config.Scale;
                        double contentLeftX = insertPt.X + config.MarginLeftMm * config.Scale;
                        double innerPad = Math.Max(0, config.ActualColumnInnerPadding);

                        double xOffset = 0;
                        for (int i = 0; i < area.ColumnCount; i++)
                        {
                            string content = (i < pageData.ColumnContents.Length && !string.IsNullOrWhiteSpace(pageData.ColumnContents[i]))
                                ? pageData.ColumnContents[i] : "";
                            string colMd = (pageData.ColumnMarkdowns != null && i < pageData.ColumnMarkdowns.Length)
                                ? (pageData.ColumnMarkdowns[i] ?? "")
                                : "";
                            double colWidth = area.ColumnWidths[i];
                            double colLeftX = contentLeftX + xOffset;
                            double textLeftX = colLeftX + innerPad;
                            double textTopY = contentTopY - innerPad;
                            double textWidth = Math.Max(config.ActualTextHeight, colWidth - innerPad * 2.0);

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                var newMtext = new MText();
                                newMtext.SetDatabaseDefaults();
                                newMtext.Location = new Point3d(textLeftX, textTopY, insertPt.Z);
                                newMtext.Attachment = ParseAttachmentPoint(config.MTextAttachment);
                                newMtext.TextStyleId = textStyleId;
                                newMtext.TextHeight = config.ActualTextHeight;
                                newMtext.LineSpacingStyle = ParseLineSpacingStyle(config.MTextLineSpacingStyle);
                                newMtext.LineSpacingFactor = config.LineSpacingFactor;
                                newMtext.Width = textWidth;
                                newMtext.Contents = content;

                                SetLayer(db, tr, newMtext, LAYER_TEXT);
                                btr2.AppendEntity(newMtext);
                                tr.AddNewlyCreatedDBObject(newMtext, true);
                                if (newAnchorEntityId.IsNull)
                                    newAnchorEntityId = newMtext.ObjectId;

                                WriteMetadataIfNeeded(tr, newMtext, markdownSource, config, ref metadataWritten);
                                ExtensionDictionaryService.WriteLongString(tr, newMtext, newGroupId, XREC_KEY_GROUP);
                                mtextCount++;
                            }

                            tableCount += InsertTablesForColumn(
                                tr, btr2, db, config, tablePlacements,
                                pageIdx, i, colMd,
                                textLeftX, textTopY, insertPt.Z,
                                textWidth, newGroupId, markdownSource,
                                textStyleId,
                                ref metadataWritten, ref newAnchorEntityId);

                            xOffset += colWidth + area.ColumnGutter;
                        }
                    }

                    tr.Commit();
                    string widthInfo = string.Join("+", area.ColumnWidths.Select(w => w.ToString("0.0")));
                    ed.WriteMessage($"\n已更新设计说明：MText={mtextCount}, Table={tableCount}（{totalPages}页，{area.ColumnCount}栏，栏宽={widthInfo}mm，总宽={area.TotalWidth:0.0}mm）");
                    return newAnchorEntityId;
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
            Dictionary<long, List<TablePlacement>> tablePlacements,
            int pageIndex,
            int columnIndex,
            string columnMarkdown,
            double columnLeftX,
            double columnTopY,
            double z,
            double columnWidth,
            string groupId,
            string markdownSource,
            ObjectId textStyleId,
            ref bool metadataWritten,
            ref ObjectId anchorEntityId)
        {
            long key = ((long)pageIndex << 32) | (uint)columnIndex;
            List<TablePlacement> placements = null;
            if (tablePlacements != null && tablePlacements.TryGetValue(key, out var planned) && planned != null)
                placements = planned.OrderBy(x => x.TopOffsetMm).ToList();

            int created = 0;
            if (placements != null && placements.Count > 0)
            {
                foreach (var placement in placements)
                {
                    if (placement?.TableData == null)
                        continue;
                    double tableTopY = columnTopY - Math.Max(0, placement.TopOffsetMm);
                    if (TryCreateTable(
                        tr,
                        btr,
                        db,
                        config,
                        placement.TableData,
                        columnLeftX,
                        tableTopY,
                        z,
                        columnWidth,
                        groupId,
                        markdownSource,
                        textStyleId,
                        ref metadataWritten,
                        ref anchorEntityId))
                    {
                        created++;
                    }
                }
                return created;
            }

            if (string.IsNullOrWhiteSpace(columnMarkdown))
                return 0;

            var tables = MarkdownTableExtractor.ExtractTopLevelTables(columnMarkdown);
            if (tables == null || tables.Count == 0)
                return 0;

            // 回退路径：没有布局位置信息时，保持旧行为（顺序下排）。
            double y = columnTopY - config.ActualTextHeight;
            foreach (var tableData in tables)
            {
                if (TryCreateTable(
                    tr,
                    btr,
                    db,
                    config,
                    tableData,
                    columnLeftX,
                    y,
                    z,
                    columnWidth,
                    groupId,
                    markdownSource,
                    textStyleId,
                    ref metadataWritten,
                    ref anchorEntityId))
                {
                    created++;
                    var rowHeight = Math.Max(config.ActualTextHeight * config.LineSpacingFactor * 1.3, config.ActualTextHeight);
                    y -= Math.Max(rowHeight * Math.Max(1, tableData.Rows.Count), config.ActualTextHeight);
                }
            }

            return created;
        }

        private static Dictionary<long, List<TablePlacement>> BuildTablePlacements(
            string markdownSource,
            LayoutResultModel layoutResult,
            DesignSpecConfig config,
            double[] columnWidths)
        {
            var result = new Dictionary<long, List<TablePlacement>>();
            if (string.IsNullOrWhiteSpace(markdownSource) || layoutResult?.Pages == null || layoutResult.Pages.Length == 0)
                return result;

            var blocks = SharedMarkdownBlockParser.ParseTopLevelBlocks(markdownSource ?? string.Empty);
            if (blocks == null || blocks.Count == 0)
                return result;

            for (int pageIdx = 0; pageIdx < layoutResult.Pages.Length; pageIdx++)
            {
                var page = layoutResult.Pages[pageIdx];
                if (page?.ColumnBlockIndices == null || page.ColumnBlockIndices.Length == 0)
                    continue;

                for (int col = 0; col < page.ColumnBlockIndices.Length; col++)
                {
                    int[] indices = page.ColumnBlockIndices[col] ?? Array.Empty<int>();
                    double[] heights = (page.ColumnBlockHeightsMm != null && col < page.ColumnBlockHeightsMm.Length)
                        ? (page.ColumnBlockHeightsMm[col] ?? Array.Empty<double>())
                        : Array.Empty<double>();
                    double colWidth = (columnWidths != null && col < columnWidths.Length && columnWidths[col] > 0)
                        ? columnWidths[col]
                        : config.GetColumnWidth(col);

                    double topOffset = 0;
                    for (int i = 0; i < indices.Length; i++)
                    {
                        int blockIndex = indices[i];
                        SharedDocumentBlock block = (blockIndex >= 0 && blockIndex < blocks.Count) ? blocks[blockIndex] : null;
                        double blockHeight = (i < heights.Length && heights[i] > 0)
                            ? heights[i]
                            : EstimateBlockHeightFallback(block, config, colWidth);

                        if (block != null && block.Type == SharedDocumentBlockType.Table)
                        {
                            var tableData = MarkdownTableExtractor.ExtractTopLevelTables(block.SourceText ?? string.Empty).FirstOrDefault();
                            if (tableData != null && tableData.Rows.Count > 0 && tableData.ColumnCount > 0)
                            {
                                long key = ((long)pageIdx << 32) | (uint)col;
                                if (!result.TryGetValue(key, out var list))
                                {
                                    list = new List<TablePlacement>();
                                    result[key] = list;
                                }

                                list.Add(new TablePlacement
                                {
                                    PageIndex = pageIdx,
                                    ColumnIndex = col,
                                    BlockIndex = blockIndex,
                                    TopOffsetMm = Math.Max(0, topOffset),
                                    EstimatedHeightMm = Math.Max(config.ActualTextHeight, blockHeight),
                                    TableData = tableData
                                });
                            }
                        }

                        topOffset += Math.Max(config.ActualTextHeight, blockHeight);
                    }
                }
            }

            return result;
        }

        private static double EstimateBlockHeightFallback(SharedDocumentBlock block, DesignSpecConfig cfg, double columnWidth)
        {
            double lineHeight = cfg.ActualTextHeight * cfg.LineSpacingFactor;
            if (block == null)
                return lineHeight;

            double effectiveColumnWidth = Math.Max(cfg.ActualTextHeight, columnWidth - Math.Max(0, cfg.ActualColumnInnerPadding) * 2.0);
            int charsPerLine = Math.Max(1, (int)Math.Floor(effectiveColumnWidth / Math.Max(0.01, cfg.ActualTextHeight * cfg.TextXScale)));
            int displayUnits = Math.Max(1, block.DisplayUnits);
            int lines = Math.Max(1, (int)Math.Ceiling(displayUnits / (double)charsPerLine));

            switch (block.Type)
            {
                case SharedDocumentBlockType.Heading:
                    double headingHeight = cfg.ActualTextHeight;
                    if (block.HeadingLevel == 1) headingHeight = cfg.H1Height;
                    else if (block.HeadingLevel == 2) headingHeight = cfg.H2Height;
                    else if (block.HeadingLevel == 3) headingHeight = cfg.H3Height;
                    return lines * headingHeight * cfg.LineSpacingFactor
                        + cfg.GetHeadingSpaceBefore(block.HeadingLevel)
                        + cfg.GetHeadingSpaceAfter(block.HeadingLevel);
                case SharedDocumentBlockType.List:
                    return lines * lineHeight + cfg.ActualLiSpaceAfter * Math.Max(1, block.ParagraphCount);
                case SharedDocumentBlockType.BlockQuote:
                    return lines * lineHeight + cfg.ActualQuoteSpaceBefore + cfg.ActualQuoteSpaceAfter;
                case SharedDocumentBlockType.Table:
                    return Math.Max(lineHeight * 2, lineHeight * 1.3 * Math.Max(2, block.ParagraphCount));
                default:
                    return lines * lineHeight + cfg.ActualPSpaceAfter;
            }
        }

        private bool TryCreateTable(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            MarkdownTableData tableData,
            double columnLeftX,
            double tableTopY,
            double z,
            double columnWidth,
            string groupId,
            string markdownSource,
            ObjectId textStyleId,
            ref bool metadataWritten,
            ref ObjectId anchorEntityId)
        {
            int rows = tableData?.Rows?.Count ?? 0;
            int cols = tableData?.ColumnCount ?? 0;
            if (rows <= 0 || cols <= 0)
                return false;

            var table = new Table();
            table.SetDatabaseDefaults();
            table.TableStyle = db.Tablestyle;
            table.Position = new Point3d(columnLeftX, tableTopY, z);
            table.SetSize(rows, cols);

            // 应用与 MText 相同的文字样式（含 TextXScale）
            if (!textStyleId.IsNull)
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        table.Cells[r, c].TextStyleId = textStyleId;
                        table.Cells[r, c].Alignment = CellAlignment.MiddleCenter;
                    }
            }

            double[] colWidths = BuildTableColumnWidths(tableData, columnWidth, config);
            int[] rowLines = BuildTableRowLineCounts(tableData, colWidths, config);
            double baseRowHeight = Math.Max(config.ActualTextHeight * config.LineSpacingFactor * 1.3, config.ActualTextHeight);

            for (int r = 0; r < rows; r++)
            {
                int lineCount = (rowLines != null && r < rowLines.Length) ? Math.Max(1, rowLines[r]) : 1;
                table.Rows[r].Height = Math.Max(baseRowHeight, baseRowHeight * lineCount);
                table.Rows[r].TextHeight = config.ActualTextHeight;
            }

            for (int c = 0; c < cols; c++)
            {
                double width = (colWidths != null && c < colWidths.Length) ? colWidths[c] : (columnWidth / Math.Max(1, cols));
                table.Columns[c].Width = Math.Max(config.ActualTextHeight * config.TextXScale * 2.0, width);
            }

            for (int r = 0; r < rows; r++)
            {
                var row = tableData.Rows[r];
                for (int c = 0; c < cols; c++)
                    table.Cells[r, c].TextString = c < row.Count ? row[c] ?? string.Empty : string.Empty;
            }

            table.GenerateLayout();
            SetLayer(db, tr, table, LAYER_TEXT);
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            if (anchorEntityId.IsNull)
                anchorEntityId = table.ObjectId;

            WriteMetadataIfNeeded(tr, table, markdownSource, config, ref metadataWritten);
            ExtensionDictionaryService.WriteLongString(tr, table, groupId, XREC_KEY_GROUP);
            return true;
        }

        private static double[] BuildTableColumnWidths(MarkdownTableData tableData, double totalColumnWidth, DesignSpecConfig config)
        {
            int cols = Math.Max(0, tableData?.ColumnCount ?? 0);
            if (cols <= 0)
                return Array.Empty<double>();

            int[] units = GetTableColumnDisplayUnits(tableData, cols);
            double minWidth = Math.Max(config.ActualTextHeight * config.TextXScale * 2.0, totalColumnWidth * 0.08);
            double reserved = minWidth * cols;
            double flexible = Math.Max(0, totalColumnWidth - reserved);
            double unitSum = Math.Max(1, units.Sum(u => Math.Max(1, u)));
            var widths = new double[cols];

            double acc = 0;
            for (int i = 0; i < cols; i++)
            {
                double ratio = Math.Max(1, units[i]) / unitSum;
                double width = minWidth + flexible * ratio;
                widths[i] = width;
                acc += width;
            }

            if (cols > 0 && Math.Abs(acc - totalColumnWidth) > 0.01)
            {
                widths[cols - 1] = Math.Max(minWidth, widths[cols - 1] + (totalColumnWidth - acc));
            }

            return widths;
        }

        private static int[] BuildTableRowLineCounts(MarkdownTableData tableData, double[] colWidths, DesignSpecConfig config)
        {
            int rows = tableData?.Rows?.Count ?? 0;
            if (rows <= 0)
                return Array.Empty<int>();

            int cols = Math.Max(1, tableData.ColumnCount);
            var charsPerLine = new int[cols];
            for (int c = 0; c < cols; c++)
            {
                double width = (colWidths != null && c < colWidths.Length) ? colWidths[c] : config.GetColumnWidth(0) / cols;
                charsPerLine[c] = Math.Max(1, (int)Math.Floor(width / Math.Max(0.01, config.ActualTextHeight * config.TextXScale)));
            }

            var rowLines = new int[rows];
            for (int r = 0; r < rows; r++)
            {
                var row = tableData.Rows[r] ?? new List<string>();
                int maxLines = 1;
                for (int c = 0; c < cols; c++)
                {
                    int perLine = charsPerLine[c];
                    string text = c < row.Count ? row[c] ?? string.Empty : string.Empty;
                    int units = Math.Max(1, DisplayWidthCalculator.GetDisplayUnits(text));
                    int lines = Math.Max(1, (int)Math.Ceiling(units / (double)perLine));
                    if (lines > maxLines) maxLines = lines;
                }
                rowLines[r] = maxLines;
            }

            return rowLines;
        }

        private static int[] GetTableColumnDisplayUnits(MarkdownTableData tableData, int cols)
        {
            var units = Enumerable.Repeat(2, Math.Max(1, cols)).ToArray();
            if (tableData?.Rows == null)
                return units;

            foreach (var row in tableData.Rows)
            {
                if (row == null) continue;
                for (int c = 0; c < row.Count && c < units.Length; c++)
                {
                    int cellUnits = DisplayWidthCalculator.GetDisplayUnits(row[c] ?? string.Empty);
                    if (cellUnits > units[c]) units[c] = cellUnits;
                }
            }

            return units;
        }

        private ObjectId EnsureTextStyle(Database db, Transaction tr, DesignSpecConfig config)
        {
            string styleName = !string.IsNullOrWhiteSpace(config.TextStyleName)
                ? config.TextStyleName.Trim()
                : $"0_Hy_{config.Scale}";

            var vm = SettingsPanelViewModel.Current;
            if (string.IsNullOrWhiteSpace(styleName) && vm != null) styleName = vm.TextStyleName;

            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(styleName))
            {
                var rec = (TextStyleTableRecord)tr.GetObject(tst[styleName], OpenMode.ForWrite);
                rec.TextSize = 0;
                rec.XScale = config.TextXScale;
                rec.ObliquingAngle = config.MTextObliquingAngle * Math.PI / 180.0;
                ApplyTextStyleFont(rec, config);
                return tst[styleName];
            }

            tst.UpgradeOpen();
            var newRec = new TextStyleTableRecord
            {
                Name = styleName,
                TextSize = 0,
                XScale = config.TextXScale,
                ObliquingAngle = config.MTextObliquingAngle * Math.PI / 180.0
            };
            ApplyTextStyleFont(newRec, config);
            tst.Add(newRec);
            tr.AddNewlyCreatedDBObject(newRec, true);
            return newRec.ObjectId;
        }

        private static void ApplyTextStyleFont(TextStyleTableRecord rec, DesignSpecConfig config)
        {
            if (rec == null) return;

            string rawFont = (config?.FontFileName ?? string.Empty).Trim();
            if (IsShxFont(rawFont))
            {
                rec.FileName = rawFont;
                rec.BigFontFileName = config?.BigFontFileName ?? string.Empty;
                return;
            }

            string typeface = ResolveTrueTypeFontFile(rawFont);
            rec.FileName = typeface;
            rec.BigFontFileName = string.Empty;
        }

        private static AttachmentPoint ParseAttachmentPoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return AttachmentPoint.TopLeft;
            var v = value.Trim();
            if (string.Equals(v, "TopLeft", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.TopLeft;
            if (string.Equals(v, "TopCenter", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.TopCenter;
            if (string.Equals(v, "TopRight", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.TopRight;
            if (string.Equals(v, "MiddleLeft", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.MiddleLeft;
            if (string.Equals(v, "MiddleCenter", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.MiddleCenter;
            if (string.Equals(v, "MiddleRight", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.MiddleRight;
            if (string.Equals(v, "BottomLeft", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.BottomLeft;
            if (string.Equals(v, "BottomCenter", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.BottomCenter;
            if (string.Equals(v, "BottomRight", StringComparison.OrdinalIgnoreCase)) return AttachmentPoint.BottomRight;
            return AttachmentPoint.TopLeft;
        }

        private static LineSpacingStyle ParseLineSpacingStyle(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return LineSpacingStyle.Exactly;
            return string.Equals(value.Trim(), "AtLeast", StringComparison.OrdinalIgnoreCase)
                ? LineSpacingStyle.AtLeast : LineSpacingStyle.Exactly;
        }

        private static bool IsShxFont(string fontName)
        {
            return !string.IsNullOrWhiteSpace(fontName)
                && fontName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTrueTypeFontFile(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                return "微软雅黑";

            string value = fontName.Trim();
            if (string.Equals(value, "msyh.ttc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "msyh.ttf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "微软雅黑", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "微软雅黑体", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Microsoft YaHei", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Microsoft YaHei UI", StringComparison.OrdinalIgnoreCase))
                return "微软雅黑";

            if (value.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(value);

            return value;
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

        /// <summary>
        /// 根据 layoutResult 为每页生成列内容。单页时直接使用传入的 columnContents。
        /// </summary>
        private static List<PageColumnData> BuildAllPageColumnData(
            string[] columnContents,
            string[] columnMarkdowns,
            string markdownSource,
            DesignSpecConfig config,
            LayoutResultModel layoutResult)
        {
            var pages = new List<PageColumnData>();
            int pageCount = layoutResult?.Pages?.Length ?? 0;

            if (pageCount <= 1)
            {
                // 单页或无布局：直接使用传入的数据
                pages.Add(new PageColumnData
                {
                    PageIndex = 0,
                    ColumnContents = columnContents ?? Array.Empty<string>(),
                    ColumnMarkdowns = columnMarkdowns ?? Array.Empty<string>()
                });
                return pages;
            }

            // 多页：根据 layoutResult 的 ColumnBlockIndices 为每页切分 markdown
            var allBlocks = SharedColumnSplitter.ExtractTopLevelBlocks(markdownSource ?? "");
            if (allBlocks == null || allBlocks.Length == 0)
            {
                pages.Add(new PageColumnData
                {
                    PageIndex = 0,
                    ColumnContents = columnContents ?? Array.Empty<string>(),
                    ColumnMarkdowns = columnMarkdowns ?? Array.Empty<string>()
                });
                return pages;
            }

            for (int p = 0; p < pageCount; p++)
            {
                var page = layoutResult.Pages[p];
                int colCount = page?.ColumnBlockIndices?.Length ?? 0;
                var pageContents = new string[colCount];
                var pageMds = new string[colCount];

                for (int c = 0; c < colCount; c++)
                {
                    int[] indices = page.ColumnBlockIndices[c] ?? Array.Empty<int>();
                    var blockTexts = indices
                        .Where(idx => idx >= 0 && idx < allBlocks.Length)
                        .Select(idx => allBlocks[idx])
                        .ToArray();
                    string colMarkdown = string.Join("\n\n", blockTexts);
                    pageMds[c] = colMarkdown;

                    // 表格从 MText 中移除，单独插入
                    string mdWithoutTables = MarkdownTableExtractor.RemoveTopLevelTables(colMarkdown);
                    var renderer = new MarkdownToMTextRenderer(config);
                    pageContents[c] = renderer.Convert(mdWithoutTables);
                }

                pages.Add(new PageColumnData
                {
                    PageIndex = p,
                    ColumnContents = pageContents,
                    ColumnMarkdowns = pageMds
                });
            }

            return pages;
        }

        /// <summary>
        /// 绘制页面图框（外边框矩形）
        /// </summary>
        private void DrawTitleBlock(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            double pageLeftX,
            double pageTopY,
            double z,
            string groupId)
        {
            double w = config.PageWidthMm * config.Scale;
            double h = config.PageHeightMm * config.Scale;

            // 外边框
            var outerPoly = new Polyline(4);
            outerPoly.AddVertexAt(0, new Point2d(pageLeftX, pageTopY), 0, 0, 0);
            outerPoly.AddVertexAt(1, new Point2d(pageLeftX + w, pageTopY), 0, 0, 0);
            outerPoly.AddVertexAt(2, new Point2d(pageLeftX + w, pageTopY - h), 0, 0, 0);
            outerPoly.AddVertexAt(3, new Point2d(pageLeftX, pageTopY - h), 0, 0, 0);
            outerPoly.Closed = true;
            outerPoly.Elevation = z;
            SetLayer(db, tr, outerPoly, LAYER_FRAME);
            btr.AppendEntity(outerPoly);
            tr.AddNewlyCreatedDBObject(outerPoly, true);
            ExtensionDictionaryService.WriteLongString(tr, outerPoly, groupId, XREC_KEY_GROUP);

            // 内边框（页边距）
            double ml = config.MarginLeftMm * config.Scale;
            double mr = config.MarginRightMm * config.Scale;
            double mt = config.MarginTopMm * config.Scale;
            double mb = config.MarginBottomMm * config.Scale;

            var innerPoly = new Polyline(4);
            innerPoly.AddVertexAt(0, new Point2d(pageLeftX + ml, pageTopY - mt), 0, 0, 0);
            innerPoly.AddVertexAt(1, new Point2d(pageLeftX + w - mr, pageTopY - mt), 0, 0, 0);
            innerPoly.AddVertexAt(2, new Point2d(pageLeftX + w - mr, pageTopY - h + mb), 0, 0, 0);
            innerPoly.AddVertexAt(3, new Point2d(pageLeftX + ml, pageTopY - h + mb), 0, 0, 0);
            innerPoly.Closed = true;
            innerPoly.Elevation = z;
            SetLayer(db, tr, innerPoly, LAYER_FRAME);
            btr.AppendEntity(innerPoly);
            tr.AddNewlyCreatedDBObject(innerPoly, true);
            ExtensionDictionaryService.WriteLongString(tr, innerPoly, groupId, XREC_KEY_GROUP);
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
