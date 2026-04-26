using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using FontDescriptor = Autodesk.AutoCAD.GraphicsInterface.FontDescriptor;
using HyCADTool.Features.DesignSpec.Domain.Models;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Presentation.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LayoutResultModel = HyCADTool.TextLayout.LayoutResult;
using SharedMarkdownBlockParser = HyCADTool.TextLayout.MarkdownBlockParser;
using SharedDocumentBlock = HyCADTool.TextLayout.DocumentBlock;
using SharedDocumentBlockType = HyCADTool.TextLayout.DocumentBlockType;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// 设计说明排版服务 v4：每栏独立 MText，各自独立宽度
    /// </summary>
    public class DesignSpecService
    {
        private static string LayerText => UserLayerNameResolver.Get(LayerSemanticIds.PublicNoteGeneral, LayerBuiltinDefaults.NoteGeneral);
        private static string LayerFrame => UserLayerNameResolver.Get(LayerSemanticIds.TitleBlock, LayerBuiltinDefaults.TitleBlock);
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

        private sealed class ColumnMarkdownSegment
        {
            public bool IsTable { get; set; }
            public string Markdown { get; set; }
            public MarkdownTableData TableData { get; set; }
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
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            config.Normalize();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = CalculateArea(config, layoutResult);
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
                    int outputPageCount = 0;
                    int pageColCount = Math.Max(1, config.ColumnCount);
                    string pendingMarkdown = markdownSource ?? string.Empty;

                    while (!string.IsNullOrWhiteSpace(pendingMarkdown) || outputPageCount == 0)
                    {
                        double pageTopY = insertionPoint.Y - outputPageCount * pageHeightScaled;
                        DrawTitleBlock(tr, btr, db, config, insertionPoint.X, pageTopY, insertionPoint.Z, groupId);

                        double contentTopY = pageTopY - config.MarginTopMm * config.Scale;
                        double contentBottomY = pageTopY - (config.PageHeightMm - config.MarginBottomMm) * config.Scale;
                        double contentLeftX = insertionPoint.X + config.MarginLeftMm * config.Scale;
                        double innerPad = Math.Max(0, config.ActualColumnInnerPadding);
                        double xOffset = 0;
                        var pageLayout = layoutResult?.Pages != null && outputPageCount < layoutResult.Pages.Length
                            ? layoutResult.Pages[outputPageCount] : null;

                        for (int i = 0; i < pageColCount; i++)
                        {
                            double colWidth = ResolveColumnWidthMm(i, config, pageColCount);
                            double colLeftX = contentLeftX + xOffset;
                            double textLeftX = colLeftX + innerPad;
                            double colTopOffsetMm = pageLayout?.ColumnTopOffsetsMm != null && i < pageLayout.ColumnTopOffsetsMm.Length
                                ? Math.Max(0, pageLayout.ColumnTopOffsetsMm[i]) : 0;
                            double fullInnerHeightMm = config.PageHeightMm - config.MarginTopMm - config.MarginBottomMm;
                            double colHeightMm = pageLayout?.ColumnHeightsMm != null && i < pageLayout.ColumnHeightsMm.Length && pageLayout.ColumnHeightsMm[i] > 0
                                ? Math.Min(pageLayout.ColumnHeightsMm[i], fullInnerHeightMm - colTopOffsetMm) : fullInnerHeightMm - colTopOffsetMm;
                            double textTopY = contentTopY - colTopOffsetMm * config.Scale - innerPad;
                            double colContentBottomY = contentTopY - (colTopOffsetMm + colHeightMm) * config.Scale;
                            double textWidth = Math.Max(config.ActualTextHeight, colWidth - innerPad * 2.0);

                            var overflowSegments = InsertColumnEntities(
                                tr, btr, db, config,
                                outputPageCount, i, pendingMarkdown,
                                textLeftX, textTopY, insertionPoint.Z,
                                textWidth, colContentBottomY, groupId, markdownSource,
                                textStyleId,
                                ref metadataWritten, ref anchorEntityId,
                                ref mtextCount, ref tableCount);

                            pendingMarkdown = BuildMarkdownFromSegments(overflowSegments);
                            xOffset += colWidth + area.ColumnGutter;
                        }

                        outputPageCount++;
                    }

                    tr.Commit();
                    string widthInfo = string.Join("+", area.ColumnWidths.Select(w => w.ToString("0.0")));
                    ed.WriteMessage($"\n已插入设计说明：MText={mtextCount}, Table={tableCount}（{outputPageCount}页，{area.ColumnCount}栏，栏宽={widthInfo}mm，总宽={area.TotalWidth:0.0}mm）");
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
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            config.Normalize();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = CalculateArea(config, layoutResult);
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
                    int outputPageCount = 0;
                    int pageColCount = Math.Max(1, config.ColumnCount);
                    string pendingMarkdown = markdownSource ?? string.Empty;

                    while (!string.IsNullOrWhiteSpace(pendingMarkdown) || outputPageCount == 0)
                    {
                        double pageTopY = insertPt.Y - outputPageCount * pageHeightScaled;
                        DrawTitleBlock(tr, btr2, db, config, insertPt.X, pageTopY, insertPt.Z, newGroupId);

                        double contentTopY = pageTopY - config.MarginTopMm * config.Scale;
                        double contentBottomY = pageTopY - (config.PageHeightMm - config.MarginBottomMm) * config.Scale;
                        double contentLeftX = insertPt.X + config.MarginLeftMm * config.Scale;
                        double innerPad = Math.Max(0, config.ActualColumnInnerPadding);
                        double xOffset = 0;
                        var pageLayout = layoutResult?.Pages != null && outputPageCount < layoutResult.Pages.Length
                            ? layoutResult.Pages[outputPageCount] : null;

                        for (int i = 0; i < pageColCount; i++)
                        {
                            double colWidth = ResolveColumnWidthMm(i, config, pageColCount);
                            double colLeftX = contentLeftX + xOffset;
                            double textLeftX = colLeftX + innerPad;
                            double colTopOffsetMm = pageLayout?.ColumnTopOffsetsMm != null && i < pageLayout.ColumnTopOffsetsMm.Length
                                ? Math.Max(0, pageLayout.ColumnTopOffsetsMm[i]) : 0;
                            double fullInnerHeightMm = config.PageHeightMm - config.MarginTopMm - config.MarginBottomMm;
                            double colHeightMm = pageLayout?.ColumnHeightsMm != null && i < pageLayout.ColumnHeightsMm.Length && pageLayout.ColumnHeightsMm[i] > 0
                                ? Math.Min(pageLayout.ColumnHeightsMm[i], fullInnerHeightMm - colTopOffsetMm) : fullInnerHeightMm - colTopOffsetMm;
                            double textTopY = contentTopY - colTopOffsetMm * config.Scale - innerPad;
                            double colContentBottomY = contentTopY - (colTopOffsetMm + colHeightMm) * config.Scale;
                            double textWidth = Math.Max(config.ActualTextHeight, colWidth - innerPad * 2.0);

                            var overflowSegments = InsertColumnEntities(
                                tr, btr2, db, config,
                                outputPageCount, i, pendingMarkdown,
                                textLeftX, textTopY, insertPt.Z,
                                textWidth, colContentBottomY, newGroupId, markdownSource,
                                textStyleId,
                                ref metadataWritten, ref newAnchorEntityId,
                                ref mtextCount, ref tableCount);

                            pendingMarkdown = BuildMarkdownFromSegments(overflowSegments);
                            xOffset += colWidth + area.ColumnGutter;
                        }

                        outputPageCount++;
                    }

                    tr.Commit();
                    string widthInfo = string.Join("+", area.ColumnWidths.Select(w => w.ToString("0.0")));
                    ed.WriteMessage($"\n已更新设计说明：MText={mtextCount}, Table={tableCount}（{outputPageCount}页，{area.ColumnCount}栏，栏宽={widthInfo}mm，总宽={area.TotalWidth:0.0}mm）");
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

        private static double ResolveColumnWidthMm(
            int colIndex,
            DesignSpecConfig config,
            int pageColCount)
        {
            _ = colIndex;

            int cols = Math.Max(1, pageColCount);
            double availableWidth = (config.PageWidthMm - config.MarginLeftMm - config.MarginRightMm) * config.Scale;
            double gutter = config.ActualColumnGutter;
            double width = (availableWidth - gutter * Math.Max(0, cols - 1)) / cols;
            if (width > 0)
                return Math.Max(config.ActualTextHeight, width);

            return config.GetPageDrivenColumnWidth();
        }

        private List<ColumnMarkdownSegment> InsertColumnEntities(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            int pageIdx,
            int colIdx,
            string columnMarkdown,
            double textLeftX,
            double textTopY,
            double z,
            double textWidth,
            double contentBottomY,
            string groupId,
            string markdownSource,
            ObjectId textStyleId,
            ref bool metadataWritten,
            ref ObjectId anchorEntityId,
            ref int mtextCount,
            ref int tableCount)
        {
            _ = pageIdx;
            _ = colIdx;

            var segments = SplitColumnMarkdownByBlocks(columnMarkdown);
            var renderer = new MarkdownToMTextRenderer(config);
            var pendingTextBlocks = new List<string>();
            double entityGap = Math.Max(config.ActualTextHeight * 0.5, config.ActualTextHeight * 0.25);

            bool metadataLocal = metadataWritten;
            ObjectId anchorLocal = anchorEntityId;
            int mtextLocal = mtextCount;
            int tableLocal = tableCount;
            bool createdAnyEntity = false;
            double cursorTopY = textTopY;

            List<ColumnMarkdownSegment> BuildOverflowFrom(int index, bool includePendingText)
            {
                var overflow = new List<ColumnMarkdownSegment>();
                if (includePendingText)
                {
                    string md = string.Join("\n\n", pendingTextBlocks.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(md))
                    {
                        overflow.Add(new ColumnMarkdownSegment
                        {
                            IsTable = false,
                            Markdown = md
                        });
                    }
                }

                for (int i = index; i < segments.Count; i++)
                {
                    if (segments[i] != null)
                        overflow.Add(segments[i]);
                }
                return overflow;
            }

            bool TryFlushTextSegment(int overflowStartIndex, bool forcePlaceholder, out List<ColumnMarkdownSegment> overflow)
            {
                overflow = null;
                if (pendingTextBlocks.Count == 0 && !forcePlaceholder)
                    return true;

                var validBlocks = pendingTextBlocks.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                string segmentMarkdown = validBlocks.Count == 0
                    ? string.Empty
                    : string.Join("\n\n", validBlocks);

                string content = string.Empty;
                if (!string.IsNullOrWhiteSpace(segmentMarkdown))
                {
                    content = renderer.Convert(segmentMarkdown) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(content) && !forcePlaceholder)
                    { pendingTextBlocks.Clear(); return true; }
                }
                else if (!forcePlaceholder)
                {
                    pendingTextBlocks.Clear();
                    return true;
                }

                pendingTextBlocks.Clear();

                var mtext = CreateMTextEntity(config, textStyleId, textLeftX, cursorTopY, z, textWidth, content);
                SetLayer(db, tr, mtext, LayerText);
                btr.AppendEntity(mtext);
                tr.AddNewlyCreatedDBObject(mtext, true);

                double estimatedHeight = string.IsNullOrWhiteSpace(content)
                    ? config.ActualTextHeight
                    : EstimateMarkdownHeightFallback(segmentMarkdown, config, textWidth);
                double actualBottomY = GetEntityBottomY(mtext, cursorTopY, estimatedHeight);

                if (actualBottomY >= contentBottomY || forcePlaceholder)
                {
                    if (anchorLocal.IsNull)
                        anchorLocal = mtext.ObjectId;
                    WriteMetadataIfNeeded(tr, mtext, markdownSource, config, ref metadataLocal);
                    ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                    mtextLocal++;
                    createdAnyEntity = true;
                    if (!string.IsNullOrWhiteSpace(content))
                        cursorTopY = actualBottomY;
                    return true;
                }

                mtext.Erase();

                if (validBlocks.Count <= 1)
                {
                    string singleBlock = validBlocks.FirstOrDefault() ?? string.Empty;
                    var lines = singleBlock
                        .Replace("\r\n", "\n")
                        .Replace('\r', '\n')
                        .Split('\n')
                        .ToList();
                    if (lines.Count == 0)
                        lines.Add(string.Empty);

                    int fitLineCount = 0;
                    for (int tryCount = lines.Count - 1; tryCount >= 1; tryCount--)
                    {
                        string partialMd = string.Join("\n", lines.Take(tryCount));
                        string partialContent = renderer.Convert(partialMd) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(partialContent)) continue;

                        var testMtext = CreateMTextEntity(config, textStyleId, textLeftX, cursorTopY, z, textWidth, partialContent);
                        SetLayer(db, tr, testMtext, LayerText);
                        btr.AppendEntity(testMtext);
                        tr.AddNewlyCreatedDBObject(testMtext, true);

                        double testEstimate = EstimateMarkdownHeightFallback(partialMd, config, textWidth);
                        double testBottom = GetEntityBottomY(testMtext, cursorTopY, testEstimate);

                        if (testBottom >= contentBottomY)
                        {
                            if (anchorLocal.IsNull)
                                anchorLocal = testMtext.ObjectId;
                            WriteMetadataIfNeeded(tr, testMtext, markdownSource, config, ref metadataLocal);
                            ExtensionDictionaryService.WriteLongString(tr, testMtext, groupId, XREC_KEY_GROUP);
                            mtextLocal++;
                            createdAnyEntity = true;
                            cursorTopY = testBottom;
                            fitLineCount = tryCount;
                            break;
                        }
                        testMtext.Erase();
                    }

                    if (fitLineCount == 0 && !createdAnyEntity)
                    {
                        string firstLineMd = lines[0];
                        string firstLineContent = renderer.Convert(firstLineMd) ?? firstLineMd;
                        var firstLineMText = CreateMTextEntity(config, textStyleId, textLeftX, cursorTopY, z, textWidth, firstLineContent);
                        SetLayer(db, tr, firstLineMText, LayerText);
                        btr.AppendEntity(firstLineMText);
                        tr.AddNewlyCreatedDBObject(firstLineMText, true);
                        double firstEstimate = EstimateMarkdownHeightFallback(firstLineMd, config, textWidth);
                        double firstBottom = GetEntityBottomY(firstLineMText, cursorTopY, firstEstimate);
                        if (anchorLocal.IsNull)
                            anchorLocal = firstLineMText.ObjectId;
                        WriteMetadataIfNeeded(tr, firstLineMText, markdownSource, config, ref metadataLocal);
                        ExtensionDictionaryService.WriteLongString(tr, firstLineMText, groupId, XREC_KEY_GROUP);
                        mtextLocal++;
                        createdAnyEntity = true;
                        cursorTopY = firstBottom;
                        fitLineCount = 1;
                    }

                    if (fitLineCount == 0)
                    {
                        pendingTextBlocks.AddRange(validBlocks);
                    }
                    else
                    {
                        string remainingMd = string.Join("\n", lines.Skip(fitLineCount)).Trim();
                        if (!string.IsNullOrWhiteSpace(remainingMd))
                            pendingTextBlocks.Add(remainingMd);
                    }

                    overflow = BuildOverflowFrom(overflowStartIndex, includePendingText: true);
                    return fitLineCount > 0 && pendingTextBlocks.Count == 0 && overflow.Count == 0;
                }

                int fitCount = 0;
                for (int tryCount = validBlocks.Count - 1; tryCount >= 1; tryCount--)
                {
                    string partialMd = string.Join("\n\n", validBlocks.Take(tryCount));
                    string partialContent = renderer.Convert(partialMd) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(partialContent)) continue;

                    var testMtext = CreateMTextEntity(config, textStyleId, textLeftX, cursorTopY, z, textWidth, partialContent);
                    SetLayer(db, tr, testMtext, LayerText);
                    btr.AppendEntity(testMtext);
                    tr.AddNewlyCreatedDBObject(testMtext, true);

                    double testEstimate = EstimateMarkdownHeightFallback(partialMd, config, textWidth);
                    double testBottom = GetEntityBottomY(testMtext, cursorTopY, testEstimate);

                    if (testBottom >= contentBottomY)
                    {
                        if (anchorLocal.IsNull)
                            anchorLocal = testMtext.ObjectId;
                        WriteMetadataIfNeeded(tr, testMtext, markdownSource, config, ref metadataLocal);
                        ExtensionDictionaryService.WriteLongString(tr, testMtext, groupId, XREC_KEY_GROUP);
                        mtextLocal++;
                        createdAnyEntity = true;
                        cursorTopY = testBottom;
                        fitCount = tryCount;
                        break;
                    }
                    testMtext.Erase();
                }

                if (fitCount == 0 && !createdAnyEntity)
                {
                    string firstBlockMd = validBlocks[0];
                    string firstBlockContent = renderer.Convert(firstBlockMd) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(firstBlockContent))
                    {
                        var firstBlockMText = CreateMTextEntity(config, textStyleId, textLeftX, cursorTopY, z, textWidth, firstBlockContent);
                        SetLayer(db, tr, firstBlockMText, LayerText);
                        btr.AppendEntity(firstBlockMText);
                        tr.AddNewlyCreatedDBObject(firstBlockMText, true);
                        double firstEstimate = EstimateMarkdownHeightFallback(firstBlockMd, config, textWidth);
                        double firstBottom = GetEntityBottomY(firstBlockMText, cursorTopY, firstEstimate);
                        if (anchorLocal.IsNull)
                            anchorLocal = firstBlockMText.ObjectId;
                        WriteMetadataIfNeeded(tr, firstBlockMText, markdownSource, config, ref metadataLocal);
                        ExtensionDictionaryService.WriteLongString(tr, firstBlockMText, groupId, XREC_KEY_GROUP);
                        mtextLocal++;
                        createdAnyEntity = true;
                        cursorTopY = firstBottom;
                        fitCount = 1;
                    }
                }

                var remainingBlocks = validBlocks.Skip(fitCount).ToList();
                foreach (var rb in remainingBlocks)
                    pendingTextBlocks.Add(rb);

                overflow = BuildOverflowFrom(overflowStartIndex, includePendingText: true);
                return false;
            }

            for (int segIndex = 0; segIndex < segments.Count; segIndex++)
            {
                var segment = segments[segIndex];
                if (segment == null)
                    continue;

                if (!segment.IsTable)
                {
                    if (!string.IsNullOrWhiteSpace(segment.Markdown))
                        pendingTextBlocks.Add(segment.Markdown);
                    continue;
                }

                if (!TryFlushTextSegment(segIndex, forcePlaceholder: false, out var textOverflow))
                {
                    metadataWritten = metadataLocal;
                    anchorEntityId = anchorLocal;
                    mtextCount = mtextLocal;
                    tableCount = tableLocal;
                    return textOverflow;
                }

                if (createdAnyEntity)
                {
                    if (cursorTopY - entityGap < contentBottomY)
                    {
                        metadataWritten = metadataLocal;
                        anchorEntityId = anchorLocal;
                        mtextCount = mtextLocal;
                        tableCount = tableLocal;
                        return BuildOverflowFrom(segIndex, includePendingText: false);
                    }
                    cursorTopY -= entityGap;
                }

                double estimatedTableHeight = EstimateTableHeightFallback(segment.TableData, textWidth, config);

                bool savedMeta = metadataLocal;
                ObjectId savedAnchor = anchorLocal;
                if (TryCreateTable(
                    tr,
                    btr,
                    db,
                    config,
                    segment.TableData,
                    textLeftX,
                    cursorTopY,
                    z,
                    textWidth,
                    groupId,
                    markdownSource,
                    textStyleId,
                    ref metadataLocal,
                    ref anchorLocal,
                    out var createdTable))
                {
                    double tableBottomY = GetEntityBottomY(createdTable, cursorTopY, estimatedTableHeight);
                    bool tableOverflow = tableBottomY < contentBottomY;
                    bool splitTable = string.Equals(config.TableBreakMode, "Split", StringComparison.OrdinalIgnoreCase);

                    if (tableOverflow && splitTable && segment.TableData != null && segment.TableData.Rows.Count > 1)
                    {
                        createdTable.Erase();
                        metadataLocal = savedMeta;
                        anchorLocal = savedAnchor;

                        int totalRows = segment.TableData.Rows.Count;
                        int fitRows = 0;
                        double fitBottomY = cursorTopY;
                        for (int rowsToTry = totalRows - 1; rowsToTry >= 1; rowsToTry--)
                        {
                            var partialTableData = SliceTableRows(segment.TableData, 0, rowsToTry);
                            if (partialTableData == null || partialTableData.Rows.Count == 0 || partialTableData.ColumnCount <= 0)
                                continue;

                            bool testMeta = metadataLocal;
                            ObjectId testAnchor = anchorLocal;
                            if (!TryCreateTable(
                                tr, btr, db, config,
                                partialTableData,
                                textLeftX, cursorTopY, z,
                                textWidth, groupId, markdownSource, textStyleId,
                                ref metadataLocal, ref anchorLocal,
                                out var partialTable))
                            {
                                metadataLocal = testMeta;
                                anchorLocal = testAnchor;
                                continue;
                            }

                            double partialEstimate = EstimateTableHeightFallback(partialTableData, textWidth, config);
                            double partialBottomY = GetEntityBottomY(partialTable, cursorTopY, partialEstimate);
                            if (partialBottomY >= contentBottomY)
                            {
                                fitRows = rowsToTry;
                                fitBottomY = partialBottomY;
                                break;
                            }

                            partialTable.Erase();
                            metadataLocal = testMeta;
                            anchorLocal = testAnchor;
                        }

                        if (fitRows > 0)
                        {
                            tableLocal++;
                            createdAnyEntity = true;
                            cursorTopY = fitBottomY;

                            var remainingTableData = SliceTableRows(segment.TableData, fitRows, totalRows - fitRows);
                            var overflow = new List<ColumnMarkdownSegment>();
                            if (remainingTableData != null && remainingTableData.Rows.Count > 0 && remainingTableData.ColumnCount > 0)
                            {
                                overflow.Add(new ColumnMarkdownSegment
                                {
                                    IsTable = true,
                                    TableData = remainingTableData,
                                    Markdown = BuildMarkdownFromTableData(remainingTableData)
                                });
                            }

                            for (int k = segIndex + 1; k < segments.Count; k++)
                            {
                                if (segments[k] != null)
                                    overflow.Add(segments[k]);
                            }

                            metadataWritten = metadataLocal;
                            anchorEntityId = anchorLocal;
                            mtextCount = mtextLocal;
                            tableCount = tableLocal;
                            return overflow;
                        }

                        if (!TryCreateTable(
                            tr, btr, db, config,
                            segment.TableData,
                            textLeftX, cursorTopY, z,
                            textWidth, groupId, markdownSource, textStyleId,
                            ref metadataLocal, ref anchorLocal,
                            out createdTable))
                        {
                            metadataWritten = metadataLocal;
                            anchorEntityId = anchorLocal;
                            mtextCount = mtextLocal;
                            tableCount = tableLocal;
                            return BuildOverflowFrom(segIndex, includePendingText: false);
                        }

                        tableBottomY = GetEntityBottomY(createdTable, cursorTopY, estimatedTableHeight);
                    }

                    if (tableBottomY < contentBottomY && createdAnyEntity)
                    {
                        createdTable.Erase();
                        metadataLocal = savedMeta;
                        anchorLocal = savedAnchor;
                        metadataWritten = metadataLocal;
                        anchorEntityId = anchorLocal;
                        mtextCount = mtextLocal;
                        tableCount = tableLocal;
                        return BuildOverflowFrom(segIndex, includePendingText: false);
                    }

                    tableLocal++;
                    createdAnyEntity = true;
                    cursorTopY = tableBottomY;

                    bool hasRemaining = segIndex < segments.Count - 1;
                    if (hasRemaining)
                    {
                        if (cursorTopY - entityGap < contentBottomY)
                        {
                            metadataWritten = metadataLocal;
                            anchorEntityId = anchorLocal;
                            mtextCount = mtextLocal;
                            tableCount = tableLocal;
                            return BuildOverflowFrom(segIndex + 1, includePendingText: false);
                        }
                        cursorTopY -= entityGap;
                    }
                }
            }

            if (!TryFlushTextSegment(segments.Count, forcePlaceholder: false, out var finalOverflow))
            {
                metadataWritten = metadataLocal;
                anchorEntityId = anchorLocal;
                mtextCount = mtextLocal;
                tableCount = tableLocal;
                return finalOverflow;
            }

            if (!createdAnyEntity)
            {
                // 该栏完全为空时，仍放置空 MText 占位，保证页栏计数稳定。
                if (!TryFlushTextSegment(segments.Count, forcePlaceholder: true, out _))
                {
                    metadataWritten = metadataLocal;
                    anchorEntityId = anchorLocal;
                    mtextCount = mtextLocal;
                    tableCount = tableLocal;
                    return new List<ColumnMarkdownSegment>();
                }
            }

            metadataWritten = metadataLocal;
            anchorEntityId = anchorLocal;
            mtextCount = mtextLocal;
            tableCount = tableLocal;
            return new List<ColumnMarkdownSegment>();
        }

        private static string BuildMarkdownFromSegments(List<ColumnMarkdownSegment> segments)
        {
            if (segments == null || segments.Count == 0)
                return string.Empty;

            var blocks = segments
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Markdown))
                .Select(s => s.Markdown.Trim())
                .ToArray();
            if (blocks.Length == 0)
                return string.Empty;
            return string.Join("\n\n", blocks);
        }

        private static MarkdownTableData SliceTableRows(MarkdownTableData source, int startIndex, int count)
        {
            if (source?.Rows == null || source.Rows.Count == 0 || count <= 0)
                return null;

            int start = Math.Max(0, startIndex);
            if (start >= source.Rows.Count)
                return null;

            int end = Math.Min(source.Rows.Count, start + count);
            if (end <= start)
                return null;

            var data = new MarkdownTableData();
            for (int i = start; i < end; i++)
            {
                var row = source.Rows[i] ?? new List<string>();
                data.Rows.Add(row.Select(cell => cell ?? string.Empty).ToList());
            }

            return data;
        }

        private static string BuildMarkdownFromTableData(MarkdownTableData tableData)
        {
            if (tableData?.Rows == null || tableData.Rows.Count == 0 || tableData.ColumnCount <= 0)
                return string.Empty;

            int cols = tableData.ColumnCount;
            string BuildRow(List<string> row)
            {
                var cells = Enumerable.Range(0, cols)
                    .Select(i => i < (row?.Count ?? 0) ? (row[i] ?? string.Empty) : string.Empty)
                    .Select(cell => cell.Replace("|", "\\|"));
                return "| " + string.Join(" | ", cells) + " |";
            }

            var lines = new List<string>
            {
                BuildRow(tableData.Rows[0]),
                "| " + string.Join(" | ", Enumerable.Repeat("---", cols)) + " |"
            };

            for (int i = 1; i < tableData.Rows.Count; i++)
                lines.Add(BuildRow(tableData.Rows[i]));

            return string.Join("\n", lines);
        }

        private static List<ColumnMarkdownSegment> SplitColumnMarkdownByBlocks(string columnMarkdown)
        {
            var result = new List<ColumnMarkdownSegment>();
            if (string.IsNullOrWhiteSpace(columnMarkdown))
                return result;

            var blocks = SharedMarkdownBlockParser.ParseTopLevelBlocks(columnMarkdown ?? string.Empty);
            if (blocks == null || blocks.Count == 0)
            {
                result.Add(new ColumnMarkdownSegment
                {
                    IsTable = false,
                    Markdown = columnMarkdown
                });
                return result;
            }

            foreach (var block in blocks)
            {
                string blockMarkdown = block?.SourceText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(blockMarkdown))
                    continue;

                if (block.Type == SharedDocumentBlockType.Table)
                {
                    var tableData = MarkdownTableExtractor.ExtractTopLevelTables(blockMarkdown).FirstOrDefault();
                    if (tableData != null && tableData.Rows.Count > 0 && tableData.ColumnCount > 0)
                    {
                        result.Add(new ColumnMarkdownSegment
                        {
                            IsTable = true,
                            Markdown = blockMarkdown,
                            TableData = tableData
                        });
                        continue;
                    }
                }

                result.Add(new ColumnMarkdownSegment
                {
                    IsTable = false,
                    Markdown = blockMarkdown
                });
            }

            return result;
        }

        private static MText CreateMTextEntity(
            DesignSpecConfig config,
            ObjectId textStyleId,
            double leftX,
            double topY,
            double z,
            double width,
            string content)
        {
            var mtext = new MText();
            mtext.SetDatabaseDefaults();
            mtext.Location = new Point3d(leftX, topY, z);
            mtext.Attachment = ParseAttachmentPoint(config.MTextAttachment);
            mtext.TextStyleId = textStyleId;
            mtext.TextHeight = config.ActualTextHeight;
            mtext.LineSpacingStyle = ParseLineSpacingStyle(config.MTextLineSpacingStyle);
            mtext.LineSpacingFactor = config.LineSpacingFactor;
            mtext.Width = Math.Max(config.ActualTextHeight, width);
            mtext.Contents = ApplyMTextInlineFormatting(content ?? string.Empty, config);
            return mtext;
        }

        private static string ApplyMTextInlineFormatting(string content, DesignSpecConfig config)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content ?? string.Empty;

            var inlineCodes = new StringBuilder();
            bool hasTracking = content.IndexOf("\\T", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasTracking && config.MTextCharSpacing >= 0.75 && config.MTextCharSpacing <= 4.0)
            {
                inlineCodes.Append("\\T").Append(config.MTextCharSpacing.ToString("0.###", CultureInfo.InvariantCulture)).Append(";");
            }

            bool hasParagraphAlign = content.IndexOf("\\pxq", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasParagraphAlign)
            {
                inlineCodes.Append(GetParagraphAlignCode(config.MTextParagraphAlign));
            }

            if (inlineCodes.Length == 0)
                return content;

            return "{"
                + inlineCodes.ToString()
                + content
                + "}";
        }

        private static string GetParagraphAlignCode(string value)
        {
            string v = (value ?? "Left").Trim();
            if (string.Equals(v, "Center", StringComparison.OrdinalIgnoreCase))
                return "\\pxqc;";
            if (string.Equals(v, "Right", StringComparison.OrdinalIgnoreCase))
                return "\\pxqr;";
            if (string.Equals(v, "Justify", StringComparison.OrdinalIgnoreCase))
                return "\\pxqj;";
            return "\\pxql;";
        }

        private static double EstimateMarkdownHeightFallback(string markdown, DesignSpecConfig config, double columnWidth)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return config.ActualTextHeight;

            var blocks = SharedMarkdownBlockParser.ParseTopLevelBlocks(markdown);
            if (blocks == null || blocks.Count == 0)
                return config.ActualTextHeight * config.LineSpacingFactor;

            double total = 0;
            foreach (var block in blocks)
                total += Math.Max(config.ActualTextHeight, EstimateBlockHeightFallback(block, config, columnWidth));
            return Math.Max(config.ActualTextHeight, total);
        }

        private static double EstimateTableHeightFallback(MarkdownTableData tableData, double columnWidth, DesignSpecConfig config)
        {
            int rows = tableData?.Rows?.Count ?? 0;
            if (rows <= 0)
                return config.ActualTextHeight;

            double[] colWidths = BuildTableColumnWidths(tableData, columnWidth, config);
            int[] rowLines = BuildTableRowLineCounts(tableData, colWidths, config);
            double cellPadding = Math.Max(config.ActualTextHeight * 0.08, config.ActualTextHeight * 0.12);
            double baseRowHeight = Math.Max(
                config.ActualTextHeight,
                config.ActualTextHeight * config.LineSpacingFactor + cellPadding * 2.0);

            double total = 0;
            for (int r = 0; r < rows; r++)
            {
                int lineCount = (rowLines != null && r < rowLines.Length) ? Math.Max(1, rowLines[r]) : 1;
                total += Math.Max(baseRowHeight, baseRowHeight * lineCount);
            }

            return Math.Max(config.ActualTextHeight, total);
        }

        private static double GetEntityBottomY(Entity entity, double currentTopY, double fallbackHeight)
        {
            double safeHeight = Math.Max(0.1, fallbackHeight);
            try
            {
                var ext = entity.GeometricExtents;
                if (!double.IsNaN(ext.MinPoint.Y)
                    && !double.IsInfinity(ext.MinPoint.Y)
                    && ext.MinPoint.Y < currentTopY - 0.01)
                {
                    return ext.MinPoint.Y;
                }
            }
            catch
            {
                // 几何范围偶发不可得，回退估算高度。
            }

            return currentTopY - safeHeight;
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
                    double cellPadding = Math.Max(config.ActualTextHeight * 0.08, config.ActualTextHeight * 0.12);
                    var rowHeight = Math.Max(
                        config.ActualTextHeight,
                        config.ActualTextHeight * config.LineSpacingFactor + cellPadding * 2.0);
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
            return TryCreateTable(
                tr,
                btr,
                db,
                config,
                tableData,
                columnLeftX,
                tableTopY,
                z,
                columnWidth,
                groupId,
                markdownSource,
                textStyleId,
                ref metadataWritten,
                ref anchorEntityId,
                out _);
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
            ref ObjectId anchorEntityId,
            out Table createdTable)
        {
            createdTable = null;
            int rows = tableData?.Rows?.Count ?? 0;
            int cols = tableData?.ColumnCount ?? 0;
            if (rows <= 0 || cols <= 0)
                return false;

            var table = new Table();
            table.SetDatabaseDefaults();
            table.TableStyle = db.Tablestyle;
            table.Position = new Point3d(columnLeftX, tableTopY, z);
            table.SetSize(rows, cols);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    try
                    {
                        var range = table.Cells[r, c].GetMergeRange();
                        if (range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn)
                            table.UnmergeCells(range);
                    }
                    catch { }
                }
            }

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
            double cellPadding = Math.Max(config.ActualTextHeight * 0.08, config.ActualTextHeight * 0.12);
            double baseRowHeight = Math.Max(
                config.ActualTextHeight,
                config.ActualTextHeight * config.LineSpacingFactor + cellPadding * 2.0);

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
            double totalWidth = SumTableColumnWidths(table, cols);
            if (columnWidth > 0 && totalWidth > columnWidth + 0.01)
            {
                double scale = columnWidth / totalWidth;
                for (int c = 0; c < cols; c++)
                {
                    table.Columns[c].Width = Math.Max(config.ActualTextHeight, table.Columns[c].Width * scale);
                }
                table.GenerateLayout();
            }

            for (int r = 0; r < rows; r++)
            {
                int lc = (rowLines != null && r < rowLines.Length) ? Math.Max(1, rowLines[r]) : 1;
                table.Rows[r].Height = baseRowHeight * lc;
            }

            SetLayer(db, tr, table, LayerText);
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            if (anchorEntityId.IsNull)
                anchorEntityId = table.ObjectId;

            WriteMetadataIfNeeded(tr, table, markdownSource, config, ref metadataWritten);
            ExtensionDictionaryService.WriteLongString(tr, table, groupId, XREC_KEY_GROUP);
            createdTable = table;
            return true;
        }

        private static double SumTableColumnWidths(Table table, int cols)
        {
            if (table == null || cols <= 0)
                return 0;

            double total = 0;
            for (int c = 0; c < cols; c++)
                total += table.Columns[c].Width;
            return total;
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
                charsPerLine[c] = Math.Max(1, (int)Math.Floor(width / Math.Max(0.01, config.ActualTextHeight * 0.5)));
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
                // SHX 路径：清掉残留的 TT FontDescriptor
                try { rec.Font = new FontDescriptor(string.Empty, false, false, 0, 0); } catch { }
                rec.FileName = rawFont;
                rec.BigFontFileName = config?.BigFontFileName ?? string.Empty;
                return;
            }

            // TrueType 路径：用 FontDescriptor 设置 typeface（家族名），
            // AutoCAD 会自动反推 .ttf/.ttc 文件，避免把"微软雅黑"误存为 SHX 文件名。
            string typeface = ResolveTrueTypeFontFile(rawFont);
            rec.Font = new FontDescriptor(typeface, false, false, 134, 34);
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

        /// <summary>
        /// 将各种字体输入（显示名 / 文件名 / 英文名）统一规范成 FontDescriptor 用的 typeface（字体家族名）。
        /// </summary>
        private static string ResolveTrueTypeFontFile(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                return "微软雅黑";

            string value = fontName.Trim();
            string lower = value.ToLowerInvariant();

            switch (lower)
            {
                case "msyh.ttc":
                case "msyh.ttf":
                case "msyhbd.ttc":
                case "microsoft yahei":
                case "microsoft yahei ui":
                    return "微软雅黑";
                case "simsun.ttc":
                case "simsun.ttf":
                    return "宋体";
                case "simhei.ttf":
                    return "黑体";
                case "simkai.ttf":
                    return "楷体";
                case "simfang.ttf":
                    return "仿宋";
            }

            if (string.Equals(value, "微软雅黑", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "微软雅黑体", StringComparison.OrdinalIgnoreCase))
                return "微软雅黑";

            if (lower.EndsWith(".ttf") || lower.EndsWith(".ttc"))
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
            SetLayer(db, tr, outerPoly, LayerFrame);
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
            SetLayer(db, tr, innerPoly, LayerFrame);
            btr.AppendEntity(innerPoly);
            tr.AddNewlyCreatedDBObject(innerPoly, true);
            ExtensionDictionaryService.WriteLongString(tr, innerPoly, groupId, XREC_KEY_GROUP);
        }

        private static TextAreaCalculator.TextAreaResult CalculateArea(DesignSpecConfig config, LayoutResultModel layoutResult)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            config.Normalize();

            if (layoutResult?.ColumnWidthsMm != null && layoutResult.ColumnWidthsMm.Length > 0)
            {
                int cols = Math.Max(1, config.ColumnCount);
                var widths = new double[cols];
                for (int i = 0; i < cols; i++)
                {
                    double fromLayout = i < layoutResult.ColumnWidthsMm.Length ? layoutResult.ColumnWidthsMm[i] : 0;
                    widths[i] = fromLayout > 0 ? fromLayout : config.GetPageDrivenColumnWidth();
                }

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

            return TextAreaCalculator.Calculate(config);
        }
    }
}
