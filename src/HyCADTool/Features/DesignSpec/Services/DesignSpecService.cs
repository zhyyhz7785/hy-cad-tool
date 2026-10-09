using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using FontDescriptor = Autodesk.AutoCAD.GraphicsInterface.FontDescriptor;
using HyCADTool.Features.DesignSpec.Domain.Models;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shell.ViewModels;
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
        private const string HyMdTableStyleFallbackName = "HyMd";
        private const double CadLineSpacingCell = 5.0 / 3.0;

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
        /// 按 HyMd 导出的毫米几何插入：每块一个 MText，GFM 表走 CAD Table，多页按纸高下排。只 Insert。
        /// </summary>
        public ObjectId InsertFromHyMdDump(
            CadLayoutDump dump,
            Point3d insertionPoint,
            double scale,
            string styleName)
        {
            if (dump == null)
                throw new ArgumentNullException(nameof(dump));
            if (dump.Paper == null)
                throw new InvalidOperationException("HyMd 导出缺少纸面几何");

            var config = ConfigFromDump(dump, scale, styleName);
            config.Normalize();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var ed = doc.Editor;
            string markdownSource = dump.Markdown ?? string.Empty;
            double s = config.Scale;

            ObjectId anchorEntityId = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);
                    string groupId = Guid.NewGuid().ToString("N");
                    bool metadataWritten = false;
                    int mtextCount = 0;
                    int tableCount = 0;
                    var renderer = new MarkdownToMTextRenderer(config);
                    var pages = dump.Pages ?? new List<CadDumpPage>();
                    bool useV3 = dump.SchemaVersion >= 3;
                    if (pages.Count == 0)
                    {
                        DrawTitleBlock(tr, btr, db, config, insertionPoint.X, insertionPoint.Y, insertionPoint.Z, groupId);
                    }

                    foreach (var page in pages)
                    {
                        int pageIndex = page?.PageIndex ?? 0;
                        double pageTopY = insertionPoint.Y - pageIndex * config.PageHeightMm * s;
                        DrawTitleBlock(tr, btr, db, config, insertionPoint.X, pageTopY, insertionPoint.Z, groupId);

                        if (page?.Blocks != null)
                        {
                            foreach (var block in page.Blocks)
                            {
                                if (block == null)
                                    continue;
                                var mtexts = new List<MText>();
                                if (useV3 && block.Lines != null && block.Lines.Count > 0)
                                {
                                    mtexts.AddRange(CreateLockedMTexts(config, textStyleId, insertionPoint, pageTopY, s, block));
                                }
                                else if (!string.IsNullOrWhiteSpace(block.Markdown))
                                {
                                    string contents = renderer.Convert(block.Markdown);
                                    if (string.IsNullOrWhiteSpace(contents))
                                        continue;
                                    double x = insertionPoint.X + block.XMm * s;
                                    double y = pageTopY - block.YMm * s;
                                    double w = Math.Max(config.ActualTextHeight, block.WidthMm * s);
                                    mtexts.Add(CreateMTextEntity(config, textStyleId, x, y, insertionPoint.Z, w, contents));
                                }
                                foreach (var mtext in mtexts)
                                {
                                    if (mtext == null)
                                        continue;
                                    SetLayer(db, tr, mtext, LayerText);
                                    btr.AppendEntity(mtext);
                                    tr.AddNewlyCreatedDBObject(mtext, true);
                                    if (anchorEntityId.IsNull)
                                        anchorEntityId = mtext.ObjectId;
                                    WriteMetadataIfNeeded(tr, mtext, markdownSource, config, ref metadataWritten);
                                    ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                                    mtextCount++;
                                }
                            }
                        }

                        if (page?.Tables != null)
                        {
                            foreach (var tableDump in page.Tables)
                            {
                                if (tableDump == null)
                                    continue;
                                if (useV3 && tableDump.Cells != null && tableDump.ColumnWidthsMm != null)
                                {
                                    if (TryCreateTableFromDumpV3(
                                        tr, btr, db, config, tableDump,
                                        insertionPoint, pageTopY, s,
                                        groupId, markdownSource, textStyleId,
                                        ref metadataWritten, ref anchorEntityId))
                                    {
                                        tableCount++;
                                    }
                                    continue;
                                }
                                if (tableDump.Rows == null || tableDump.Rows.Count == 0)
                                    continue;
                                var tableData = new MarkdownTableData();
                                foreach (var row in tableDump.Rows)
                                {
                                    tableData.Rows.Add(row ?? new List<string>());
                                }
                                double x = insertionPoint.X + tableDump.XMm * s;
                                double y = pageTopY - tableDump.YMm * s;
                                double w = Math.Max(config.ActualTextHeight, tableDump.WidthMm * s);
                                if (TryCreateTable(
                                    tr, btr, db, config, tableData,
                                    x, y, insertionPoint.Z, w,
                                    groupId, markdownSource, textStyleId,
                                    ref metadataWritten, ref anchorEntityId))
                                {
                                    tableCount++;
                                }
                            }
                        }
                    }

                    if (!metadataWritten && !anchorEntityId.IsNull)
                    {
                        var ent = tr.GetObject(anchorEntityId, OpenMode.ForWrite) as Entity;
                        WriteMetadataIfNeeded(tr, ent, markdownSource, config, ref metadataWritten);
                    }

                    tr.Commit();
                    int skip = dump.SkippedBlocks?.Count ?? 0;
                    ed.WriteMessage(
                        $"\n已从 HyMd 插入设计说明：MText={mtextCount}, Table={tableCount}（{pages.Count}页，跳过={skip}）");
                    if (skip > 0)
                    {
                        ed.WriteMessage("\n已跳过 sheet/slide/calc/layout 或 mermaid/math 块，未整篇失败。");
                    }
                    return anchorEntityId;
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\nHyMd 贴图失败: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 按 HyMd 编好的 CadStampPlan 落笔：不再从 dump 算锁行 Contents / 表内容 / 图框。
        /// </summary>
        public ObjectId AppendFromCadStampPlan(CadStampPlan plan, Point3d insertionPoint)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var config = ConfigFromPlan(plan);
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var ed = doc.Editor;
            string markdownSource = plan.Markdown ?? string.Empty;
            double s = config.Scale;
            var pages = plan.Pages ?? new List<CadStampPagePlan>();

            ObjectId anchorEntityId = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);
                    string groupId = Guid.NewGuid().ToString("N");
                    bool metadataWritten = false;
                    int mtextCount = 0;
                    int tableCount = 0;
                    int frameCount = 0;

                    if (pages.Count == 0)
                    {
                        DrawTitleBlock(tr, btr, db, config, insertionPoint.X, insertionPoint.Y, insertionPoint.Z, groupId);
                    }

                    foreach (var page in pages)
                    {
                        if (page == null)
                            continue;
                        double pageTopY = insertionPoint.Y - page.PageTopMm * s;
                        if (page.Frames != null && page.Frames.Count > 0)
                        {
                            foreach (var frame in page.Frames)
                            {
                                if (DrawFrameFromPlan(tr, btr, db, insertionPoint, pageTopY, s, frame, groupId))
                                    frameCount++;
                            }
                        }
                        else
                        {
                            DrawTitleBlock(tr, btr, db, config, insertionPoint.X, pageTopY, insertionPoint.Z, groupId);
                        }

                        if (page.Mtexts != null)
                        {
                            foreach (var mtPlan in page.Mtexts)
                            {
                                if (mtPlan == null || string.IsNullOrEmpty(mtPlan.Contents))
                                    continue;
                                var mtext = CreateMTextFromPlan(textStyleId, insertionPoint, pageTopY, s, mtPlan);
                                SetLayer(db, tr, mtext, LayerText);
                                btr.AppendEntity(mtext);
                                tr.AddNewlyCreatedDBObject(mtext, true);
                                if (anchorEntityId.IsNull)
                                    anchorEntityId = mtext.ObjectId;
                                WriteMetadataIfNeeded(tr, mtext, markdownSource, config, ref metadataWritten);
                                ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                                mtextCount++;
                            }
                        }

                        if (page.Tables != null)
                        {
                            foreach (var tablePlan in page.Tables)
                            {
                                if (tablePlan == null)
                                    continue;
                                if (TryCreateTableFromPlan(
                                    tr, btr, db, config, tablePlan,
                                    insertionPoint, pageTopY, s,
                                    groupId, markdownSource, textStyleId,
                                    ref metadataWritten, ref anchorEntityId))
                                {
                                    tableCount++;
                                }
                            }
                        }
                    }

                    if (!metadataWritten && !anchorEntityId.IsNull)
                    {
                        var ent = tr.GetObject(anchorEntityId, OpenMode.ForWrite) as Entity;
                        WriteMetadataIfNeeded(tr, ent, markdownSource, config, ref metadataWritten);
                    }

                    tr.Commit();
                    int skip = plan.SkippedCount;
                    ed.WriteMessage(
                        $"\n已从 HyMd 插入设计说明：MText={mtextCount}, Table={tableCount}, Frame={frameCount}（{pages.Count}页，跳过={skip}）");
                    if (skip > 0)
                    {
                        ed.WriteMessage("\n已跳过 sheet/slide/calc/layout 或 mermaid/math 块，未整篇失败。");
                    }
                    return anchorEntityId;
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\nHyMd 贴图失败: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }

        private static DesignSpecConfig ConfigFromPlan(CadStampPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            var margin = plan.MarginMm ?? new double[0];
            double mt = margin.Length > 0 ? margin[0] : 10;
            double mr = margin.Length > 1 ? margin[1] : 10;
            double mb = margin.Length > 2 ? margin[2] : 10;
            double ml = margin.Length > 3 ? margin[3] : 25;
            var config = new DesignSpecConfig
            {
                Scale = plan.Scale > 0 ? plan.Scale : 1.0,
                TextSize = 3.5,
                TextStyleName = !string.IsNullOrWhiteSpace(plan.StyleName)
                    ? plan.StyleName.Trim()
                    : "0-hy-说明-T",
                PageWidthMm = plan.PageWidthMm > 0 ? plan.PageWidthMm : 210,
                PageHeightMm = plan.PageHeightMm > 0 ? plan.PageHeightMm : 297,
                MarginLeftMm = ml,
                MarginRightMm = mr,
                MarginTopMm = mt,
                MarginBottomMm = mb,
                ColumnCount = plan.Columns > 0 ? plan.Columns : 1,
                ColumnGutter = plan.GutterMm,
                SourcePath = plan.SourcePath,
                FontFileName = string.IsNullOrWhiteSpace(plan.FontFileName)
                    ? "Microsoft YaHei"
                    : plan.FontFileName.Trim(),
                BigFontFileName = plan.BigFontFileName ?? string.Empty,
                TextXScale = plan.TextXScale > 0 ? plan.TextXScale : 1.0,
                HyMdLayoutSchemaVersion = plan.SchemaVersion
            };
            if (plan.LineSpacingFactor > 0)
                config.LineSpacingFactor = plan.LineSpacingFactor;
            if (plan.HeadingScales != null && plan.HeadingScales.Length >= 3)
            {
                if (plan.HeadingScales[0] > 0) config.H1Scale = plan.HeadingScales[0];
                if (plan.HeadingScales[1] > 0) config.H2Scale = plan.HeadingScales[1];
                if (plan.HeadingScales[2] > 0) config.H3Scale = plan.HeadingScales[2];
            }
            string face = config.FontFileName ?? string.Empty;
            bool shx = face.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);
            if (shx)
            {
                config.MTextBoldMode = "Color";
                config.MTextCodeMode = "SameFont";
                config.BoldFontName = string.Empty;
                config.MTextCharSpacing = 1.0;
            }
            else
            {
                config.MTextBoldMode = "FontSwitch";
                config.MTextCodeMode = "SameFont";
                config.BoldFontName = ResolveTrueTypeFontFile(face);
            }
            config.Normalize();
            return config;
        }

        private static DesignSpecConfig ConfigFromDump(CadLayoutDump dump, double scale, string styleName)
        {
            var paper = dump.Paper;
            var margin = paper.MarginMm ?? new double[0];
            // HyMd dump.marginMm = [上, 右, 下, 左]
            double mt = margin.Length > 0 ? margin[0] : 10;
            double mr = margin.Length > 1 ? margin[1] : 10;
            double mb = margin.Length > 2 ? margin[2] : 10;
            double ml = margin.Length > 3 ? margin[3] : 25;
            string dumpStyle = dump.Font != null ? dump.Font.StyleName : null;
            var config = new DesignSpecConfig
            {
                Scale = scale > 0 ? scale : 1.0,
                TextSize = dump.TextSizeMm > 0 ? dump.TextSizeMm : 3.5,
                TextStyleName = !string.IsNullOrWhiteSpace(dumpStyle)
                    ? dumpStyle.Trim()
                    : (string.IsNullOrWhiteSpace(styleName) ? "0-hy-说明-T" : styleName),
                PageWidthMm = paper.WidthMm > 0 ? paper.WidthMm : 210,
                PageHeightMm = paper.HeightMm > 0 ? paper.HeightMm : 297,
                MarginLeftMm = ml,
                MarginRightMm = mr,
                MarginTopMm = mt,
                MarginBottomMm = mb,
                ColumnCount = paper.Columns > 0 ? paper.Columns : 1,
                ColumnGutter = paper.GutterMm,
                SourcePath = dump.SourcePath,
                HyMdLayoutSchemaVersion = dump.SchemaVersion
            };
            ApplyDumpFont(config, dump);
            return config;
        }

        /// <summary>
        /// 用 InsertFromHyMdDump 同一套公式编出 CadStampPlan，供 HYMDPLANCHECK 与 HyMd TS 产出对照。
        /// 不写图，不取点。
        /// </summary>
        public static CadStampPlan BuildStampPlanFromDump(CadLayoutDump dump, double scale)
        {
            if (dump == null)
                throw new ArgumentNullException(nameof(dump));
            if (dump.Paper == null)
                throw new InvalidOperationException("HyMd 导出缺少纸面几何");
            var config = ConfigFromDump(dump, scale, null);
            config.Normalize();
            double s = config.Scale;
            var pagesIn = dump.Pages ?? new List<CadDumpPage>();
            if (pagesIn.Count == 0)
                pagesIn = new List<CadDumpPage> { new CadDumpPage { PageIndex = 0 } };

            var pages = new List<CadStampPagePlan>();
            for (int i = 0; i < pagesIn.Count; i++)
            {
                var page = pagesIn[i] ?? new CadDumpPage();
                int pageIndex = page.PageIndex;
                double pageTopMm = pageIndex * config.PageHeightMm;
                var pagePlan = new CadStampPagePlan
                {
                    PageIndex = pageIndex,
                    PageTopMm = pageTopMm,
                    Frames = PageFramesMm(config),
                    Mtexts = new List<CadStampMText>(),
                    Tables = new List<CadStampTablePlan>()
                };
                if (page.Blocks != null)
                {
                    foreach (var block in page.Blocks)
                    {
                        var mt = LockedMTextPlanFromBlock(config, block);
                        if (mt != null)
                            pagePlan.Mtexts.Add(mt);
                    }
                }
                if (page.Tables != null)
                {
                    foreach (var table in page.Tables)
                    {
                        var t = TablePlanFromDump(config, table, s);
                        if (t != null)
                            pagePlan.Tables.Add(t);
                    }
                }
                pages.Add(pagePlan);
            }

            return new CadStampPlan
            {
                SchemaVersion = 1,
                Scale = s,
                StyleName = config.TextStyleName,
                FontFileName = config.FontFileName ?? string.Empty,
                BigFontFileName = config.BigFontFileName ?? string.Empty,
                TextXScale = config.TextXScale,
                PageWidthMm = config.PageWidthMm,
                PageHeightMm = config.PageHeightMm,
                MarginMm = new[] { config.MarginTopMm, config.MarginRightMm, config.MarginBottomMm, config.MarginLeftMm },
                Columns = config.ColumnCount,
                GutterMm = config.ColumnGutter,
                LineSpacingFactor = dump.LineSpacingFactor ?? 0,
                HeadingScales = dump.HeadingScales,
                Markdown = dump.Markdown ?? string.Empty,
                SourcePath = dump.SourcePath,
                SkippedCount = dump.SkippedBlocks != null ? dump.SkippedBlocks.Count : 0,
                Pages = pages
            };
        }

        private static List<CadStampFrame> PageFramesMm(DesignSpecConfig config)
        {
            double w = config.PageWidthMm;
            double h = config.PageHeightMm;
            double ml = config.MarginLeftMm;
            double mr = config.MarginRightMm;
            double mt = config.MarginTopMm;
            double mb = config.MarginBottomMm;
            return new List<CadStampFrame>
            {
                new CadStampFrame
                {
                    Kind = "outer",
                    PointsMm = new List<double[]>
                    {
                        new[] { 0.0, 0.0 },
                        new[] { w, 0.0 },
                        new[] { w, h },
                        new[] { 0.0, h }
                    }
                },
                new CadStampFrame
                {
                    Kind = "inner",
                    PointsMm = new List<double[]>
                    {
                        new[] { ml, mt },
                        new[] { w - mr, mt },
                        new[] { w - mr, h - mb },
                        new[] { ml, h - mb }
                    }
                }
            };
        }

        private static CadStampMText LockedMTextPlanFromBlock(DesignSpecConfig config, CadDumpBlock block)
        {
            var lines = block?.Lines;
            if (lines == null || lines.Count == 0)
                return null;
            double fontSizeMm = block.FontSizeMm > 0 ? block.FontSizeMm : config.TextSize;
            double originXMm = DumpBlockOriginXMm(block);
            double topMm = lines[0].BaselineMm - fontSizeMm;
            return new CadStampMText
            {
                XMm = originXMm,
                YMm = topMm,
                HeightMm = fontSizeMm,
                Width = 0,
                Attachment = "topLeft",
                LineSpacingExactly = LockedLineSpacingFactor(block.LinePitchMm, fontSizeMm),
                Contents = BuildLockedMTextContents(lines, originXMm, config)
            };
        }

        private static CadStampTablePlan TablePlanFromDump(DesignSpecConfig config, CadDumpTable table, double s)
        {
            var cells = table?.Cells;
            int rows = cells != null ? cells.Count : 0;
            int cols = table?.ColumnWidthsMm != null ? table.ColumnWidthsMm.Length : 0;
            if (cells != null)
            {
                foreach (var row in cells)
                {
                    if (row != null && row.Count > cols)
                        cols = row.Count;
                }
            }
            if (rows <= 0 || cols <= 0)
                return null;
            double fontSizeMm = table.FontSizeMm > 0 ? table.FontSizeMm : config.TextSize;
            double glyphHeight = Math.Max(0.01, fontSizeMm * s);
            var outCells = new List<List<string>>();
            var aligns = new List<List<string>>();
            for (int r = 0; r < rows; r++)
            {
                var row = cells != null && r < cells.Count ? cells[r] : null;
                var cellRow = new List<string>();
                var alignRow = new List<string>();
                for (int c = 0; c < cols; c++)
                {
                    var cell = row != null && c < row.Count ? row[c] : null;
                    cellRow.Add(CellLinesToContents(cell, config, glyphHeight));
                    string a = ParseCellAlignName(cell?.Align);
                    alignRow.Add(a);
                }
                outCells.Add(cellRow);
                aligns.Add(alignRow);
            }
            var colW = new double[cols];
            for (int c = 0; c < cols; c++)
            {
                colW[c] = (table.ColumnWidthsMm != null && c < table.ColumnWidthsMm.Length)
                    ? table.ColumnWidthsMm[c]
                    : table.WidthMm / Math.Max(1, cols);
            }
            var rowH = new double[rows];
            for (int r = 0; r < rows; r++)
            {
                rowH[r] = (table.RowHeightsMm != null && r < table.RowHeightsMm.Length)
                    ? table.RowHeightsMm[r]
                    : fontSizeMm;
            }
            return new CadStampTablePlan
            {
                XMm = table.XMm,
                YMm = table.YMm,
                FontSizeMm = fontSizeMm,
                CellPadXMm = Math.Max(0, table.CellPadXMm),
                CellPadYMm = Math.Max(0, table.CellPadYMm),
                ColumnWidthsMm = colW,
                RowHeightsMm = rowH,
                Aligns = aligns,
                Cells = outCells
            };
        }

        private static string ParseCellAlignName(string align)
        {
            string a = (align ?? "center").Trim().ToLowerInvariant();
            if (a == "left" || a == "start") return "left";
            if (a == "right" || a == "end") return "right";
            return "center";
        }

        /// <summary>
        /// dump 内字体口径优先：有则覆盖 FontFileName / 字宽 / 行距 / 标题倍率。
        /// SHX 口径不再 \f 切雅黑粗体或代码字体。
        /// </summary>
        private static void ApplyDumpFont(DesignSpecConfig config, CadLayoutDump dump)
        {
            if (dump == null || config == null) return;
            var font = dump.Font;
            if (font != null)
            {
                if (!string.IsNullOrWhiteSpace(font.FontFileName))
                    config.FontFileName = font.FontFileName.Trim();
                config.BigFontFileName = font.BigFontFileName ?? string.Empty;
                if (font.XScale > 0)
                    config.TextXScale = font.XScale;
            }
            if (dump.LineSpacingFactor.HasValue && dump.LineSpacingFactor.Value > 0)
                config.LineSpacingFactor = dump.LineSpacingFactor.Value;
            if (dump.HeadingScales != null && dump.HeadingScales.Length >= 3)
            {
                if (dump.HeadingScales[0] > 0) config.H1Scale = dump.HeadingScales[0];
                if (dump.HeadingScales[1] > 0) config.H2Scale = dump.HeadingScales[1];
                if (dump.HeadingScales[2] > 0) config.H3Scale = dump.HeadingScales[2];
            }

            string face = config.FontFileName ?? string.Empty;
            string id = font?.Id ?? string.Empty;
            bool shx = face.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "tssd", StringComparison.OrdinalIgnoreCase);
            if (shx)
            {
                config.MTextBoldMode = "Color";
                config.MTextCodeMode = "SameFont";
                config.BoldFontName = string.Empty;
                config.MTextCharSpacing = 1.0;
                return;
            }
            config.MTextBoldMode = "FontSwitch";
            config.MTextCodeMode = "SameFont";
            config.BoldFontName = ResolveTrueTypeFontFile(face);
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
        /// 从选中图元或其同组图元读取 HyMd Markdown 源与配置。
        /// </summary>
        public static bool TryReadHymdSource(
            Transaction tr,
            Database db,
            Entity entity,
            out string markdown,
            out DesignSpecConfig config)
        {
            markdown = null;
            config = null;
            if (tr == null || entity == null)
                return false;

            markdown = ReadMarkdownSource(tr, entity);
            config = ReadConfig(tr, entity);
            if (!string.IsNullOrWhiteSpace(markdown) || (config != null && !string.IsNullOrWhiteSpace(config.SourcePath)))
                return true;

            string groupId = ExtensionDictionaryService.ReadLongString(tr, entity, XREC_KEY_GROUP);
            if (string.IsNullOrEmpty(groupId) || db == null)
                return false;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    continue;
                string gid = ExtensionDictionaryService.ReadLongString(tr, ent, XREC_KEY_GROUP);
                if (gid != groupId)
                    continue;
                string md = ReadMarkdownSource(tr, ent);
                var cfg = ReadConfig(tr, ent);
                if (cfg != null)
                    config = cfg;
                if (!string.IsNullOrWhiteSpace(md))
                {
                    markdown = md;
                    return true;
                }
            }

            return config != null && !string.IsNullOrWhiteSpace(config.SourcePath);
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

        private static string FormatCadNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeMTextLiteral(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            text = text.Replace("\\t", " ").Replace("\t", " ");
            return text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");
        }

        private static double LockedLineSpacingFactor(double linePitchMm, double fontSizeMm)
        {
            if (!(fontSizeMm > 0) || !(linePitchMm > 0))
                return 1.2;
            double factor = linePitchMm / (CadLineSpacingCell * fontSizeMm);
            if (factor < 0.8) return 0.8;
            if (factor > 4.0) return 4.0;
            return factor;
        }

        private static string FormatDumpRun(CadDumpRun run, DesignSpecConfig config)
        {
            string text = EscapeMTextLiteral(run?.Text ?? string.Empty);
            if (run == null)
                return text;
            var prefix = new StringBuilder();
            var suffix = new StringBuilder();
            if (run.SizeMm.HasValue && run.SizeMm.Value > 0)
            {
                prefix.Append("{\\H").Append(FormatCadNumber(run.SizeMm.Value * config.Scale)).Append(";");
                suffix.Insert(0, "}");
            }
            if (run.Bold)
            {
                if (IsShxFont(config.FontFileName))
                {
                    prefix.Append("{\\C1;");
                    suffix.Insert(0, "}");
                }
                else
                {
                    string face = ResolveTrueTypeFontFile(config.FontFileName);
                    prefix.Append("{\\f").Append(face).Append("|b1|i0|c134|p34;");
                    suffix.Insert(0, "}");
                }
            }
            if (run.Italic)
            {
                double angle = config.MTextItalicAngle > 0.01 ? config.MTextItalicAngle : 15;
                prefix.Append("{\\Q").Append(FormatCadNumber(angle)).Append(";");
                suffix.Insert(0, "}");
            }
            return prefix.ToString() + text + suffix.ToString();
        }

        private static void AppendDumpLineContents(StringBuilder sb, CadDumpLine line, double originXMm, DesignSpecConfig config)
        {
            if (line == null)
                return;
            bool hasMarker = line.Marker != null && !string.IsNullOrEmpty(line.Marker.Text);
            double lineLeft = hasMarker ? line.Marker.XMm : line.XMm;
            double dx = lineLeft - originXMm;
            if (Math.Abs(dx) > 0.05)
                sb.Append("\\pxl").Append(FormatCadNumber(dx / 25.4)).Append(";");
            if (hasMarker)
            {
                sb.Append(EscapeMTextLiteral(line.Marker.Text));
                if (!line.Marker.Text.EndsWith(" "))
                    sb.Append(' ');
            }
            if (line.Runs != null)
            {
                foreach (var run in line.Runs)
                    sb.Append(FormatDumpRun(run, config));
            }
        }

        private static string BuildLockedMTextContents(IList<CadDumpLine> lines, double originXMm, DesignSpecConfig config)
        {
            var sb = new StringBuilder();
            sb.Append("{\\W").Append(FormatCadNumber(config.TextXScale)).Append(";");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    sb.Append("\\P");
                AppendDumpLineContents(sb, lines[i], originXMm, config);
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static double DumpBlockOriginXMm(CadDumpBlock block)
        {
            double origin = block.XMm;
            if (block.Lines == null)
                return origin;
            foreach (var line in block.Lines)
            {
                if (line == null)
                    continue;
                origin = Math.Min(origin, line.XMm);
                if (line.Marker != null)
                    origin = Math.Min(origin, line.Marker.XMm);
            }
            return origin;
        }

        private static List<MText> CreateLockedMTexts(
            DesignSpecConfig config,
            ObjectId textStyleId,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            CadDumpBlock block)
        {
            var result = new List<MText>();
            result.Add(CreateLockedMText(config, textStyleId, insertionPoint, pageTopY, s, block));
            return result;
        }

        private static MText CreateLockedMText(
            DesignSpecConfig config,
            ObjectId textStyleId,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            CadDumpBlock block)
        {
            var lines = block.Lines;
            double fontSizeMm = block.FontSizeMm > 0 ? block.FontSizeMm : config.TextSize;
            double textHeight = Math.Max(0.01, fontSizeMm * s);
            double firstBaseline = lines[0].BaselineMm;
            double topMm = firstBaseline - fontSizeMm;
            double originXMm = DumpBlockOriginXMm(block);
            double x = insertionPoint.X + originXMm * s;
            double y = pageTopY - topMm * s;
            var mtext = new MText();
            mtext.SetDatabaseDefaults();
            mtext.Location = new Point3d(x, y, insertionPoint.Z);
            mtext.Attachment = AttachmentPoint.TopLeft;
            mtext.TextStyleId = textStyleId;
            mtext.TextHeight = textHeight;
            mtext.LineSpacingStyle = LineSpacingStyle.Exactly;
            mtext.LineSpacingFactor = LockedLineSpacingFactor(block.LinePitchMm, fontSizeMm);
            mtext.Width = 0;
            mtext.Contents = BuildLockedMTextContents(lines, originXMm, config);
            return mtext;
        }

        private static MText CreateMTextFromPlan(
            ObjectId textStyleId,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            CadStampMText mt)
        {
            double textHeight = Math.Max(0.01, mt.HeightMm * s);
            double x = insertionPoint.X + mt.XMm * s;
            double y = pageTopY - mt.YMm * s;
            double factor = mt.LineSpacingExactly;
            if (factor <= 0) factor = 1.2;
            if (factor < 0.8) factor = 0.8;
            if (factor > 4) factor = 4;
            var mtext = new MText();
            mtext.SetDatabaseDefaults();
            mtext.Location = new Point3d(x, y, insertionPoint.Z);
            mtext.Attachment = AttachmentPoint.TopLeft;
            mtext.TextStyleId = textStyleId;
            mtext.TextHeight = textHeight;
            mtext.LineSpacingStyle = LineSpacingStyle.Exactly;
            mtext.LineSpacingFactor = factor;
            mtext.Width = 0;
            mtext.Contents = mt.Contents ?? string.Empty;
            return mtext;
        }

        private static CellAlignment ParseCellAlign(string align)
        {
            string a = (align ?? "center").Trim();
            if (string.Equals(a, "left", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "start", StringComparison.OrdinalIgnoreCase))
                return CellAlignment.MiddleLeft;
            if (string.Equals(a, "right", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "end", StringComparison.OrdinalIgnoreCase))
                return CellAlignment.MiddleRight;
            return CellAlignment.MiddleCenter;
        }

        private static string HyMdTableStyleNameFor(string textStyleName)
        {
            return string.IsNullOrWhiteSpace(textStyleName)
                ? HyMdTableStyleFallbackName
                : textStyleName.Trim();
        }

        private static ObjectId EnsureHyMdTableStyle(
            Database db,
            Transaction tr,
            ObjectId textStyleId,
            double padX,
            double padY,
            string textStyleName)
        {
            string styleName = HyMdTableStyleNameFor(textStyleName);
            var dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
            TableStyle ts;
            if (dict.Contains(styleName))
            {
                ts = (TableStyle)tr.GetObject(dict.GetAt(styleName), OpenMode.ForWrite);
            }
            else
            {
                dict.UpgradeOpen();
                ts = new TableStyle();
                dict.SetAt(styleName, ts);
                tr.AddNewlyCreatedDBObject(ts, true);
            }

            int allRows = (int)(RowType.DataRow | RowType.HeaderRow | RowType.TitleRow);
            ts.SetBackgroundColorNone(true, allRows);
            ts.IsTitleSuppressed = true;
            ts.IsHeaderSuppressed = true;
            try
            {
                ts.HorizontalCellMargin = Math.Max(0, padX);
                ts.VerticalCellMargin = Math.Max(0, padY);
            }
            catch { }
            if (!textStyleId.IsNull)
            {
                try { ts.SetTextStyle(textStyleId, allRows); } catch { }
            }
            // 选中框厚度跟表样式字高走；0 与手建表（样式高度 0）一致。
            try { ts.SetTextHeight(0, allRows); } catch { }
            return ts.ObjectId;
        }

        private static void SetTableUiTextHeight(Cell cell)
        {
            try { cell.TextHeight = 0; }
            catch { cell.TextHeight = 0.01; }
        }

        private static string WrapGlyphHeight(string contents, double glyphHeight)
        {
            if (string.IsNullOrEmpty(contents))
                return contents ?? string.Empty;
            return "{\\H" + FormatCadNumber(Math.Max(0.01, glyphHeight)) + ";" + contents + "}";
        }

        /// <summary>
        /// 表格单元格不用 \f / \H / \W：字高走 Cell.TextHeight，粗体只标颜色 1。
        /// </summary>
        private static string FormatTableRun(CadDumpRun run)
        {
            string text = EscapeMTextLiteral(run?.Text ?? string.Empty);
            if (run != null && run.Bold)
                return "{\\C1;" + text + "}";
            return text;
        }

        private static string CellLinesToContents(CadDumpTableCell cell, DesignSpecConfig config, double glyphHeight)
        {
            if (cell?.Lines == null || cell.Lines.Count == 0)
                return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < cell.Lines.Count; i++)
            {
                if (i > 0)
                    sb.Append("\\P");
                var line = cell.Lines[i];
                if (line?.Runs == null)
                    continue;
                foreach (var run in line.Runs)
                    sb.Append(FormatTableRun(run));
            }
            return sb.ToString();
        }

        private static void SetTableCellGlyphHeight(Cell cell, double textHeight)
        {
            try { cell.TextHeight = Math.Max(0.01, textHeight); }
            catch { }
        }

        private bool TryCreateTableFromDumpV3(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            CadDumpTable tableDump,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            string groupId,
            string markdownSource,
            ObjectId textStyleId,
            ref bool metadataWritten,
            ref ObjectId anchorEntityId)
        {
            int rows = tableDump.Cells != null ? tableDump.Cells.Count : 0;
            int cols = 0;
            if (tableDump.ColumnWidthsMm != null)
                cols = tableDump.ColumnWidthsMm.Length;
            if (tableDump.Cells != null)
            {
                foreach (var row in tableDump.Cells)
                {
                    if (row != null && row.Count > cols)
                        cols = row.Count;
                }
            }
            if (rows <= 0 || cols <= 0)
                return false;

            double padX = Math.Max(0, tableDump.CellPadXMm) * s;
            double padY = Math.Max(0, tableDump.CellPadYMm) * s;
            ObjectId styleId = EnsureHyMdTableStyle(db, tr, textStyleId, padX, padY, config.TextStyleName);

            var table = new Table();
            table.SetDatabaseDefaults();
            table.TableStyle = styleId;
            table.Position = new Point3d(
                insertionPoint.X + tableDump.XMm * s,
                pageTopY - tableDump.YMm * s,
                insertionPoint.Z);
            table.SetSize(rows, cols);
#pragma warning disable CS0618
            table.IsTitleSuppressed = true;
            table.IsHeaderSuppressed = true;
#pragma warning restore CS0618

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

            double fontSizeMm = tableDump.FontSizeMm > 0 ? tableDump.FontSizeMm : config.TextSize;
            double textHeight = Math.Max(0.01, fontSizeMm * s);

            for (int c = 0; c < cols; c++)
            {
                double w = (tableDump.ColumnWidthsMm != null && c < tableDump.ColumnWidthsMm.Length)
                    ? tableDump.ColumnWidthsMm[c] * s
                    : Math.Max(textHeight, tableDump.WidthMm * s / Math.Max(1, cols));
                table.Columns[c].Width = Math.Max(textHeight, w);
            }

            for (int r = 0; r < rows; r++)
            {
                double h = (tableDump.RowHeightsMm != null && r < tableDump.RowHeightsMm.Length)
                    ? tableDump.RowHeightsMm[r] * s
                    : textHeight;
                table.Rows[r].Height = Math.Max(textHeight, h);
                try { table.Rows[r].TextHeight = textHeight; }
                catch { }
            }

            for (int r = 0; r < rows; r++)
            {
                var row = tableDump.Cells[r];
                for (int c = 0; c < cols; c++)
                {
                    var cellDump = (row != null && c < row.Count) ? row[c] : null;
                    var cell = table.Cells[r, c];
                    if (!textStyleId.IsNull)
                        cell.TextStyleId = textStyleId;
                    SetTableCellGlyphHeight(cell, textHeight);
                    cell.Alignment = ParseCellAlign(cellDump?.Align);
                    cell.TextString = CellLinesToContents(cellDump, config, textHeight);
                }
            }

            table.GenerateLayout();
            for (int r = 0; r < rows; r++)
            {
                double h = (tableDump.RowHeightsMm != null && r < tableDump.RowHeightsMm.Length)
                    ? tableDump.RowHeightsMm[r] * s
                    : textHeight;
                table.Rows[r].Height = Math.Max(textHeight, h);
                try { table.Rows[r].TextHeight = textHeight; }
                catch { }
                for (int c = 0; c < cols; c++)
                    SetTableCellGlyphHeight(table.Cells[r, c], textHeight);
            }

            SetLayer(db, tr, table, LayerText);
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            if (anchorEntityId.IsNull)
                anchorEntityId = table.ObjectId;
            WriteMetadataIfNeeded(tr, table, markdownSource, config, ref metadataWritten);
            ExtensionDictionaryService.WriteLongString(tr, table, groupId, XREC_KEY_GROUP);
            return true;
        }

        private bool TryCreateTableFromPlan(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            DesignSpecConfig config,
            CadStampTablePlan tablePlan,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            string groupId,
            string markdownSource,
            ObjectId textStyleId,
            ref bool metadataWritten,
            ref ObjectId anchorEntityId)
        {
            int rows = tablePlan.Cells != null ? tablePlan.Cells.Count : 0;
            int cols = 0;
            if (tablePlan.ColumnWidthsMm != null)
                cols = tablePlan.ColumnWidthsMm.Length;
            if (tablePlan.Cells != null)
            {
                foreach (var row in tablePlan.Cells)
                {
                    if (row != null && row.Count > cols)
                        cols = row.Count;
                }
            }
            if (rows <= 0 || cols <= 0)
                return false;

            double padX = Math.Max(0, tablePlan.CellPadXMm) * s;
            double padY = Math.Max(0, tablePlan.CellPadYMm) * s;
            ObjectId styleId = EnsureHyMdTableStyle(db, tr, textStyleId, padX, padY, config.TextStyleName);

            var table = new Table();
            table.SetDatabaseDefaults();
            table.TableStyle = styleId;
            table.Position = new Point3d(
                insertionPoint.X + tablePlan.XMm * s,
                pageTopY - tablePlan.YMm * s,
                insertionPoint.Z);
            table.SetSize(rows, cols);
#pragma warning disable CS0618
            table.IsTitleSuppressed = true;
            table.IsHeaderSuppressed = true;
#pragma warning restore CS0618

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

            double fontSizeMm = tablePlan.FontSizeMm > 0 ? tablePlan.FontSizeMm : config.TextSize;
            double textHeight = Math.Max(0.01, fontSizeMm * s);

            for (int c = 0; c < cols; c++)
            {
                double w = (tablePlan.ColumnWidthsMm != null && c < tablePlan.ColumnWidthsMm.Length)
                    ? tablePlan.ColumnWidthsMm[c] * s
                    : textHeight;
                table.Columns[c].Width = Math.Max(textHeight, w);
            }

            for (int r = 0; r < rows; r++)
            {
                double h = (tablePlan.RowHeightsMm != null && r < tablePlan.RowHeightsMm.Length)
                    ? tablePlan.RowHeightsMm[r] * s
                    : textHeight;
                table.Rows[r].Height = Math.Max(textHeight, h);
                try { table.Rows[r].TextHeight = 0; }
                catch { table.Rows[r].TextHeight = 0.01; }
            }

            for (int r = 0; r < rows; r++)
            {
                var row = tablePlan.Cells[r];
                var alignRow = (tablePlan.Aligns != null && r < tablePlan.Aligns.Count) ? tablePlan.Aligns[r] : null;
                for (int c = 0; c < cols; c++)
                {
                    var cell = table.Cells[r, c];
                    if (!textStyleId.IsNull)
                        cell.TextStyleId = textStyleId;
                    SetTableUiTextHeight(cell);
                    string align = (alignRow != null && c < alignRow.Count) ? alignRow[c] : "center";
                    cell.Alignment = ParseCellAlign(align);
                    cell.TextString = (row != null && c < row.Count) ? (row[c] ?? string.Empty) : string.Empty;
                }
            }

            table.GenerateLayout();
            for (int r = 0; r < rows; r++)
            {
                double h = (tablePlan.RowHeightsMm != null && r < tablePlan.RowHeightsMm.Length)
                    ? tablePlan.RowHeightsMm[r] * s
                    : textHeight;
                table.Rows[r].Height = Math.Max(textHeight, h);
                try { table.Rows[r].TextHeight = 0; }
                catch { table.Rows[r].TextHeight = 0.01; }
                for (int c = 0; c < cols; c++)
                    SetTableUiTextHeight(table.Cells[r, c]);
            }

            SetLayer(db, tr, table, LayerText);
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            if (anchorEntityId.IsNull)
                anchorEntityId = table.ObjectId;
            WriteMetadataIfNeeded(tr, table, markdownSource, config, ref metadataWritten);
            ExtensionDictionaryService.WriteLongString(tr, table, groupId, XREC_KEY_GROUP);
            return true;
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
            bool hasTracking = content.IndexOf("\\T", StringComparison.Ordinal) >= 0;
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
                try { table.Rows[r].TextHeight = 0; }
                catch { table.Rows[r].TextHeight = 0.01; }
            }

            for (int c = 0; c < cols; c++)
            {
                double width = (colWidths != null && c < colWidths.Length) ? colWidths[c] : (columnWidth / Math.Max(1, cols));
                table.Columns[c].Width = Math.Max(config.ActualTextHeight * config.TextXScale * 2.0, width);
            }

            double glyphH = config.ActualTextHeight;
            for (int r = 0; r < rows; r++)
            {
                var row = tableData.Rows[r];
                for (int c = 0; c < cols; c++)
                {
                    string raw = c < row.Count ? row[c] ?? string.Empty : string.Empty;
                    var cell = table.Cells[r, c];
                    SetTableUiTextHeight(cell);
                    cell.TextString = WrapGlyphHeight(raw, glyphH);
                }
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
                try { table.Rows[r].TextHeight = 0; }
                catch { table.Rows[r].TextHeight = 0.01; }
                for (int c = 0; c < cols; c++)
                    SetTableUiTextHeight(table.Cells[r, c]);
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
                case "yahei":
                    return "微软雅黑";
                case "simsun.ttc":
                case "simsun.ttf":
                case "simsun":
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

            if (string.Equals(value, "宋体", StringComparison.OrdinalIgnoreCase))
                return "宋体";

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
        /// 按 plan 顶点画一圈闭合多段线（纸面 y 向下）。
        /// </summary>
        private bool DrawFrameFromPlan(
            Transaction tr,
            BlockTableRecord btr,
            Database db,
            Point3d insertionPoint,
            double pageTopY,
            double s,
            CadStampFrame frame,
            string groupId)
        {
            if (frame?.PointsMm == null || frame.PointsMm.Count < 2)
                return false;
            var poly = new Polyline(frame.PointsMm.Count);
            int i = 0;
            foreach (var pt in frame.PointsMm)
            {
                if (pt == null || pt.Length < 2)
                    continue;
                double x = insertionPoint.X + pt[0] * s;
                double y = pageTopY - pt[1] * s;
                poly.AddVertexAt(i, new Point2d(x, y), 0, 0, 0);
                i++;
            }
            if (i < 2)
                return false;
            poly.Closed = true;
            poly.Elevation = insertionPoint.Z;
            SetLayer(db, tr, poly, LayerFrame);
            btr.AppendEntity(poly);
            tr.AddNewlyCreatedDBObject(poly, true);
            ExtensionDictionaryService.WriteLongString(tr, poly, groupId, XREC_KEY_GROUP);
            return true;
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
