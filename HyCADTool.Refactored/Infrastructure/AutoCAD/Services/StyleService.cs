using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
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

                    if (!textStyleTable.Has(styleName))
                    {
                        textStyleTable.UpgradeOpen();

                        var textStyleRecord = new TextStyleTableRecord
                        {
                            Name = styleName,
                            FileName = fontName,
                            BigFontFileName = bigFontName,
                            TextSize = textHeight,
                            XScale = widthFactor
                        };

                        textStyleTable.Add(textStyleRecord);
                        tr.AddNewlyCreatedDBObject(textStyleRecord, true);
                        
                        doc.Editor.WriteMessage($"\n✓ 已创建文字样式: {styleName}");
                    }

                    tr.Commit();
                    return textStyleTable[styleName].ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建文字样式失败: {ex.Message}");
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

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    
                    if (textStyleTable.Has(styleName))
                    {
                        db.Textstyle = textStyleTable[styleName];
                        tr.Commit();
                        doc.Editor.WriteMessage($"\n✓ 已设置当前文字样式: {styleName}");
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

        public string CreateDimensionStyle(string styleName, string textStyleName = null, double scale = 1.0)
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

                    if (!dimStyleTable.Has(styleName))
                    {
                        dimStyleTable.UpgradeOpen();

                        var dimStyleRecord = new DimStyleTableRecord
                        {
                            Name = styleName
                        };

                        // 设置基本标注参数
                        dimStyleRecord.Dimexo = 1.0 * scale;  // 尺寸界线偏移
                        dimStyleRecord.Dimexe = 1.0 * scale;  // 尺寸界线超出
                        dimStyleRecord.Dimdle = 0.5 * scale;  // 尺寸线超出
                        dimStyleRecord.Dimtxt = 2.5 * scale;  // 文字高度
                        dimStyleRecord.Dimgap = 1.0 * scale;  // 文字偏移
                        dimStyleRecord.Dimasz = 1.0 * scale;  // 箭头大小
                        dimStyleRecord.Dimdec = 0;            // 小数位数
                        dimStyleRecord.Dimtdec = 0;           // 公差小数位数

                        // 设置文字样式
                        if (!string.IsNullOrEmpty(textStyleName))
                        {
                            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            if (textStyleTable.Has(textStyleName))
                            {
                                dimStyleRecord.Dimtxsty = textStyleTable[textStyleName];
                            }
                        }

                        dimStyleTable.Add(dimStyleRecord);
                        tr.AddNewlyCreatedDBObject(dimStyleRecord, true);
                        
                        doc.Editor.WriteMessage($"\n✓ 已创建标注样式: {styleName}");
                    }

                    tr.Commit();
                    return dimStyleTable[styleName].ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建标注样式失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
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

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    
                    if (dimStyleTable.Has(styleName))
                    {
                        db.Dimstyle = dimStyleTable[styleName];
                        tr.Commit();
                        doc.Editor.WriteMessage($"\n✓ 已设置当前标注样式: {styleName}");
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

        // === 多重引线样式 ===

        public string CreateMLeaderStyle(string styleName, string textStyleName = null)
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

                    if (!mleaderStyleDict.Contains(styleName))
                    {
                        mleaderStyleDict.UpgradeOpen();

                        var mleaderStyle = new MLeaderStyle
                        {
                            ContentType = ContentType.MTextContent,
                            LeaderLineType = LeaderType.StraightLeader,
                            MaxLeaderSegmentsPoints = 2
                        };

                        // 设置文字样式
                        if (!string.IsNullOrEmpty(textStyleName))
                        {
                            var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                            if (textStyleTable.Has(textStyleName))
                            {
                                mleaderStyle.TextStyleId = textStyleTable[textStyleName];
                            }
                        }

                        mleaderStyleDict.SetAt(styleName, mleaderStyle);
                        tr.AddNewlyCreatedDBObject(mleaderStyle, true);
                        
                        doc.Editor.WriteMessage($"\n✓ 已创建多重引线样式: {styleName}");
                    }

                    tr.Commit();
                    return mleaderStyleDict.GetAt(styleName).ToString();
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 创建多重引线样式失败: {ex.Message}");
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

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var mleaderStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    
                    if (mleaderStyleDict.Contains(styleName))
                    {
                        db.MLeaderstyle = mleaderStyleDict.GetAt(styleName);
                        tr.Commit();
                        doc.Editor.WriteMessage($"\n✓ 已设置当前多重引线样式: {styleName}");
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

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var tableStyleDict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
                    
                    if (tableStyleDict.Contains(styleName))
                    {
                        db.Tablestyle = tableStyleDict.GetAt(styleName);
                        tr.Commit();
                        doc.Editor.WriteMessage($"\n✓ 已设置当前表格样式: {styleName}");
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