using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.GraphicsInterface;
using HyCADTool.Refactored.Domain.Interfaces;
using System;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 样式服务实现
    /// </summary>
    public class StyleService : IStyleService
    {
        // === 文字样式 ===

        public string CreateTextStyle(string styleName, string fontName = "tssdeng.shx", string bigFontName = "hztxt.shx", double textHeight = 2.5, double widthFactor = 0.7)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                    if (textStyleTable.Has(styleName))
                    {
                        // 样式已存在 → 更新属性（与旧代码 CreateTextStyle 重载一致）
                        ObjectId styleId = textStyleTable[styleName];
                        var styleRec = (TextStyleTableRecord)tr.GetObject(styleId, OpenMode.ForWrite);
                        ApplyTextStyleFont(styleRec, fontName, bigFontName);
                        styleRec.TextSize = textHeight;
                        styleRec.XScale = widthFactor;
                        db.Textstyle = styleId;
                    }
                    else
                    {
                        textStyleTable.UpgradeOpen();

                        var textStyleRecord = new TextStyleTableRecord
                        {
                            Name = styleName,
                            TextSize = textHeight,
                            XScale = widthFactor
                        };
                        ApplyTextStyleFont(textStyleRecord, fontName, bigFontName);

                        textStyleTable.Add(textStyleRecord);
                        tr.AddNewlyCreatedDBObject(textStyleRecord, true);
                        db.Textstyle = textStyleTable[styleName];
                    }

                    tr.Commit();
                    return textStyleTable[styleName].ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n创建文字样式失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void SetCurrentTextStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    
                    if (textStyleTable.Has(styleName))
                    {
                        db.Textstyle = textStyleTable[styleName];
                        tr.Commit();
                    }
                    else
                    {
                        throw new ArgumentException($"Text style '{styleName}' does not exist");
                    }
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        // === 标注样式 ===

        public string CreateDimensionStyle(string styleName, string textStyleName = null, double scale = 1.0,
            double dimtxt = 2.5, double dimexo = 1.0, double dimexe = 1.0,
            double dimdle = 0.5, double dimgap = 1.0, double dimasz = 1.0,
            double dimlfac = 1.0, int dimdec = 0, double unitFactor = 1.0)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    DimStyleTableRecord dimStyleRecord;
                    bool isNew = false;

                    if (dimStyleTable.Has(styleName))
                    {
                        dimStyleRecord = (DimStyleTableRecord)tr.GetObject(dimStyleTable[styleName], OpenMode.ForWrite);
                    }
                    else
                    {
                        dimStyleTable.UpgradeOpen();
                        dimStyleRecord = new DimStyleTableRecord { Name = styleName };
                        isNew = true;
                    }

                    // paper-mm 基值 → 存入前乘 unitFactor（mm=1 / cm=0.1 / m=0.001）
                    // 最终 model-unit 尺寸 = 存入值 × DIMSCALE = paper_mm × unitFactor × scale
                    dimStyleRecord.Dimtdec = dimdec;
                    dimStyleRecord.Dimexo = dimexo * unitFactor;
                    dimStyleRecord.Dimexe = dimexe * unitFactor;
                    dimStyleRecord.Dimdle = dimdle * unitFactor;
                    dimStyleRecord.Dimtxt = dimtxt * unitFactor;
                    dimStyleRecord.Dimgap = dimgap * unitFactor;
                    dimStyleRecord.Dimasz = dimasz * unitFactor;
                    dimStyleRecord.Dimdec = dimdec;
                    dimStyleRecord.Dimscale = scale;
                    dimStyleRecord.Dimlfac = dimlfac;
                    dimStyleRecord.Dimtofl = true;        // 尺寸线强制
                    dimStyleRecord.Dimtad = 1;            // 文字位置垂直（上方）
                    dimStyleRecord.Dimtix = true;         // 文字在内
                    dimStyleRecord.Dimtih = false;        // 文字在内不水平对齐
                    dimStyleRecord.Dimtoh = false;        // 文字外部不水平对齐
                    dimStyleRecord.Dimclrt = Color.FromColorIndex(ColorMethod.ByColor, 7); // 文字颜色白色

                    // 设置文字样式
                    if (!string.IsNullOrEmpty(textStyleName))
                    {
                        var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                        if (textStyleTable.Has(textStyleName))
                        {
                            dimStyleRecord.Dimtxsty = textStyleTable[textStyleName];
                        }
                    }

                    if (isNew)
                    {
                        dimStyleTable.Add(dimStyleRecord);
                        tr.AddNewlyCreatedDBObject(dimStyleRecord, true);
                    }

                    // 设置尺寸样式数据并设为当前
                    db.SetDimstyleData(dimStyleRecord);
                    db.Dimstyle = dimStyleRecord.ObjectId;

                    // 设置箭头样式为 _ARCHTICK
                    SetDimStyleArrows(db, tr, dimStyleRecord);

                    tr.Commit();
                    return dimStyleRecord.ObjectId.ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n创建标注样式失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        /// <summary>
        /// 设置标注样式的箭头（_ARCHTICK）
        /// </summary>
        private void SetDimStyleArrows(Database db, Transaction tr, DimStyleTableRecord dimStyleRecord)
        {
            var arrowId = GetArrowObjectId(db, "_ARCHTICK");
            if (!arrowId.IsNull)
            {
                dimStyleRecord.Dimsah = true;
                dimStyleRecord.Dimblk1 = arrowId;
                dimStyleRecord.Dimblk2 = arrowId;
                db.SetDimstyleData(dimStyleRecord);
            }
        }

        /// <summary>
        /// 获取箭头块的 ObjectId（与旧代码 GetArrowObjectId 一致）
        /// </summary>
        private ObjectId GetArrowObjectId(Database db, string arrowName)
        {
            // 通过设置系统变量来注册箭头块定义
            string sysVar = "DIMBLK";
            try
            {
                string oldVal = AcApp.GetSystemVariable(sysVar) as string;
                AcApp.SetSystemVariable(sysVar, arrowName);
                if (!string.IsNullOrEmpty(oldVal))
                    AcApp.SetSystemVariable(sysVar, oldVal);
            }
            catch { }

            using (var tr2 = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr2.GetObject(db.BlockTableId, OpenMode.ForRead);
                ObjectId result = bt.Has(arrowName) ? bt[arrowName] : ObjectId.Null;
                tr2.Commit();
                return result;
            }
        }

        public void SetCurrentDimensionStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    
                    if (dimStyleTable.Has(styleName))
                    {
                        db.Dimstyle = dimStyleTable[styleName];
                        tr.Commit();
                    }
                    else
                    {
                        throw new ArgumentException($"Dimension style '{styleName}' does not exist");
                    }
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public string GetCurrentDimensionStyleName()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    if (db.Dimstyle.IsNull)
                    {
                        tr.Commit();
                        return string.Empty;
                    }

                    var dimStyleRecord = tr.GetObject(db.Dimstyle, OpenMode.ForRead) as DimStyleTableRecord;
                    string styleName = dimStyleRecord?.Name ?? string.Empty;
                    tr.Commit();
                    return styleName;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        private static void ApplyTextStyleFont(TextStyleTableRecord rec, string fontName, string bigFontName)
        {
            if (rec == null) return;

            string rawFont = (fontName ?? string.Empty).Trim();
            if (IsShxFont(rawFont))
            {
                // SHX 路径：清掉 TT 的 FontDescriptor，避免之前的 typeface 残留导致 AutoCAD 又自动给 .shx 名加 TT 前缀
                try { rec.Font = new FontDescriptor(string.Empty, false, false, 0, 0); } catch { }
                rec.FileName = string.IsNullOrWhiteSpace(rawFont) ? "tssdeng.shx" : rawFont;
                rec.BigFontFileName = (bigFontName ?? string.Empty).Trim();
                return;
            }

            // TrueType 路径：使用 FontDescriptor 设置 typeface（家族名），
            // AutoCAD 会自动反推真实 .ttf/.ttc 文件，避免把"微软雅黑"误当 SHX 文件名。
            // charSet=134 (GB2312)，pitchAndFamily=34 (VARIABLE_PITCH | FF_SWISS)，对中英文 TT 都通用。
            string typeface = ResolveTrueTypeTypeface(rawFont);
            rec.Font = new FontDescriptor(typeface, false, false, 134, 34);
            rec.BigFontFileName = string.Empty;
        }

        private static bool IsShxFont(string fontName)
        {
            return !string.IsNullOrWhiteSpace(fontName)
                && fontName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将各种字体输入（显示名 / 文件名 / 英文名）统一规范成 FontDescriptor 用的 typeface（字体家族名）。
        /// </summary>
        private static string ResolveTrueTypeTypeface(string fontName)
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

        // === 多重引线样式 ===

        public string CreateMLeaderStyle(string styleName, string textStyleName = null, double scale = 1.0,
            double arrowSize = 2.0, double landingGap = 0.5, double textHeight = 2.5,
            int textColorIndex = 7, double unitFactor = 1.0)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var mleaderStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    MLeaderStyle mleaderStyle;
                    ObjectId mleId;

                    if (mleaderStyleDict.Contains(styleName))
                    {
                        mleId = mleaderStyleDict.GetAt(styleName);
                        mleaderStyle = (MLeaderStyle)tr.GetObject(mleId, OpenMode.ForWrite);
                    }
                    else
                    {
                        mleaderStyleDict.UpgradeOpen();
                        mleaderStyle = new MLeaderStyle();
                        mleId = mleaderStyleDict.SetAt(styleName, mleaderStyle);
                        tr.AddNewlyCreatedDBObject(mleaderStyle, true);
                    }

                    // 基本属性
                    mleaderStyle.ContentType = ContentType.MTextContent;
                    mleaderStyle.LeaderLineType = LeaderType.StraightLeader;
                    mleaderStyle.MaxLeaderSegmentsPoints = 2;

                    // 文字样式
                    if (!string.IsNullOrEmpty(textStyleName))
                    {
                        var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                        if (textStyleTable.Has(textStyleName))
                        {
                            mleaderStyle.TextStyleId = textStyleTable[textStyleName];
                        }
                    }

                    // paper-mm 基值 → 乘 unitFactor × scale 得 model-unit 尺寸
                    // model = paper_mm × unitFactor × scale
                    mleaderStyle.TextHeight = textHeight * unitFactor * scale;
                    mleaderStyle.TextColor = Color.FromColorIndex(ColorMethod.ByColor, (short)textColorIndex);
                    mleaderStyle.TextAttachmentType = TextAttachmentType.AttachmentBottomLine;

                    // 箭头（_DotSmall）
                    var arrowId = GetArrowObjectId(db, "_DotSmall");
                    if (!arrowId.IsNull)
                    {
                        mleaderStyle.ArrowSymbolId = arrowId;
                    }
                    mleaderStyle.ArrowSize = arrowSize * unitFactor * scale;

                    // 着陆间隙（文字与基线间小间距，纸面 mm → 模型）
                    mleaderStyle.LandingGap = landingGap * unitFactor * scale;
                    // 狗腿/基线水平段长：未设时 CAD 默认易很大，数字左侧拉很长白线（特性「基线距离」/ DoglegLength）
                    double modelTextH = textHeight * unitFactor * scale;
                    mleaderStyle.DoglegLength = modelTextH * 0.45;
                    mleaderStyle.EnableDogleg = true;
                    mleaderStyle.LeaderLineWeight = LineWeight.ByLayer;

                    // 设为当前引线样式
                    db.MLeaderstyle = mleId;

                    tr.Commit();
                    return mleId.ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n创建多重引线样式失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void SetCurrentMLeaderStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var mleaderStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    
                    if (mleaderStyleDict.Contains(styleName))
                    {
                        db.MLeaderstyle = mleaderStyleDict.GetAt(styleName);
                        tr.Commit();
                    }
                    else
                    {
                        throw new ArgumentException($"MLeader style '{styleName}' does not exist");
                    }
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        // === 线型样式 ===

        public void LoadLinetype(string linetypeFilePath, string linetypeName = null)
        {
            if (string.IsNullOrWhiteSpace(linetypeFilePath))
                throw new ArgumentException("Linetype file path cannot be null or empty", nameof(linetypeFilePath));

            if (!File.Exists(linetypeFilePath))
                throw new FileNotFoundException($"Linetype file not found: {linetypeFilePath}");

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            {
                try
                {
                    if (string.IsNullOrEmpty(linetypeName))
                    {
                        // 加载所有线型
                        db.LoadLineTypeFile("*", linetypeFilePath);
                        doc.Editor.WriteMessage($"\n✓ 已从文件加载所有线型: {linetypeFilePath}");
                    }
                    else
                    {
                        // 加载指定线型
                        db.LoadLineTypeFile(linetypeName, linetypeFilePath);
                        doc.Editor.WriteMessage($"\n✓ 已从文件加载线型 '{linetypeName}': {linetypeFilePath}");
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 加载线型失败: {ex.Message}");
                    throw;
                }
            }
        }

        public void ExportLinetypesToFile(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    using (var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8))
                    {
                        var linetypeTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                        writer.WriteLine("; Exported from DWG");
                        writer.WriteLine("; Generated on " + DateTime.Now);
                        writer.WriteLine();

                        foreach (ObjectId ltId in linetypeTable)
                        {
                            var ltRecord = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);
                            if (!ltRecord.IsErased && ltRecord.Name != "ByLayer" && ltRecord.Name != "ByBlock")
                            {
                                writer.WriteLine($"*{ltRecord.Name},{ltRecord.Comments}");
                                // 这里可以添加更复杂的线型定义导出逻辑
                                writer.WriteLine("A,1.0");
                                writer.WriteLine();
                            }
                        }
                    }

                    tr.Commit();
                    doc.Editor.WriteMessage($"\n✓ 已导出线型到文件: {outputPath}");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    doc.Editor.WriteMessage($"\n✗ 导出线型失败: {ex.Message}");
                    throw;
                }
            }
        }

        // === 表格样式 ===

        public string CreateTableStyle(string styleName, string textStyleName = null)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var tableStyleDict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);

                    if (!tableStyleDict.Contains(styleName))
                    {
                        tableStyleDict.UpgradeOpen();

                        var tableStyle = new TableStyle();

                        // 设置基本属性 - 使用正确的参数
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.HorizontalBottom, (int)RowType.DataRow);
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.HorizontalInside, (int)RowType.DataRow);
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.HorizontalTop, (int)RowType.TitleRow);
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.VerticalInside, (int)RowType.DataRow);
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.VerticalLeft, (int)RowType.DataRow);
                        tableStyle.SetGridLineWeight(LineWeight.LineWeight000, (int)GridLineType.VerticalRight, (int)RowType.DataRow);

                        // 设置文字样式
                        if (!string.IsNullOrEmpty(textStyleName))
                        {
                            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            if (textStyleTable.Has(textStyleName))
                            {
                                tableStyle.SetTextStyle(textStyleTable[textStyleName], (int)RowType.TitleRow);
                                tableStyle.SetTextStyle(textStyleTable[textStyleName], (int)RowType.HeaderRow);
                                tableStyle.SetTextStyle(textStyleTable[textStyleName], (int)RowType.DataRow);
                            }
                        }

                        tableStyleDict.SetAt(styleName, tableStyle);
                        tr.AddNewlyCreatedDBObject(tableStyle, true);
                        
                        doc.Editor.WriteMessage($"\n✓ 已创建表格样式: {styleName}");
                    }

                    tr.Commit();
                    return tableStyleDict.GetAt(styleName).ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建表格样式失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void SetCurrentTableStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("No active document");

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var tableStyleDict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
                    
                    if (tableStyleDict.Contains(styleName))
                    {
                        db.Tablestyle = tableStyleDict.GetAt(styleName);
                        tr.Commit();
                    }
                    else
                    {
                        throw new ArgumentException($"Table style '{styleName}' does not exist");
                    }
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        // === 通用样式管理 ===

        public bool StyleExists(string styleName, StyleType styleType)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                return false;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    bool exists = false;

                    switch (styleType)
                    {
                        case StyleType.TextStyle:
                            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            exists = textStyleTable.Has(styleName);
                            break;

                        case StyleType.DimensionStyle:
                            var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                            exists = dimStyleTable.Has(styleName);
                            break;

                        case StyleType.MLeaderStyle:
                            var mleaderStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                            exists = mleaderStyleDict.Contains(styleName);
                            break;

                        case StyleType.TableStyle:
                            var tableStyleDict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
                            exists = tableStyleDict.Contains(styleName);
                            break;

                        case StyleType.Linetype:
                            var linetypeTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                            exists = linetypeTable.Has(styleName);
                            break;
                    }

                    tr.Commit();
                    return exists;
                }
                catch
                {
                    tr.Abort();
                    return false;
                }
            }
        }

        public bool DeleteStyle(string styleName, StyleType styleType)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                return false;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return false;

            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 注意：样式删除需要谨慎，通常只有未被使用的样式才能删除
                    // 这里提供基本实现，实际使用时可能需要更复杂的逻辑

                    switch (styleType)
                    {
                        case StyleType.TextStyle:
                            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
                            if (textStyleTable.Has(styleName) && styleName != "Standard")
                            {
                                var styleRecord = (TextStyleTableRecord)tr.GetObject(textStyleTable[styleName], OpenMode.ForWrite);
                                styleRecord.Erase();
                            }
                            break;

                        case StyleType.DimensionStyle:
                            var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForWrite);
                            if (dimStyleTable.Has(styleName) && styleName != "Standard")
                            {
                                var styleRecord = (DimStyleTableRecord)tr.GetObject(dimStyleTable[styleName], OpenMode.ForWrite);
                                styleRecord.Erase();
                            }
                            break;

                        // 其他样式类型的删除逻辑...
                    }

                    tr.Commit();
                    doc.Editor.WriteMessage($"\n✓ 已删除样式: {styleName}");
                    return true;
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    doc.Editor.WriteMessage($"\n✗ 删除样式失败: {ex.Message}");
                    return false;
                }
            }
        }
    }
}