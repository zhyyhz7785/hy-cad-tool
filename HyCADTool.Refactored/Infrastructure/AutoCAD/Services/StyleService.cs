using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 样式服务实现
    /// </summary>
    public class StyleService : IStyleService
    {
        public void CreateOrUpdateTextStyle(TextStyleConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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

                    TextStyleTableRecord textStyle;
                    bool isNew = false;

                    if (textStyleTable.Has(config.Name))
                    {
                        // 更新现有样式
                        var styleId = textStyleTable[config.Name];
                        textStyle = (TextStyleTableRecord)tr.GetObject(styleId, OpenMode.ForWrite);
                    }
                    else
                    {
                        // 创建新样式
                        textStyleTable.UpgradeOpen();
                        textStyle = new TextStyleTableRecord();
                        textStyle.Name = config.Name;
                        textStyleTable.Add(textStyle);
                        tr.AddNewlyCreatedDBObject(textStyle, true);
                        isNew = true;
                    }

                    // 设置样式属性
                    textStyle.FileName = config.FontFileName ?? "txt.shx";
                    textStyle.BigFontFileName = config.BigFontFileName ?? string.Empty;
                    textStyle.TextSize = config.TextSize;
                    textStyle.XScale = config.XScale;
                    textStyle.ObliquingAngle = config.ObliqueAngle;

                    tr.Commit();

                    var action = isNew ? "创建" : "更新";
                    doc.Editor.WriteMessage($"\n✓ {action}文本样式: {config.Name}");
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 文本样式操作失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public void CreateOrUpdateDimensionStyle(DimensionStyleConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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

                    DimStyleTableRecord dimStyle;
                    bool isNew = false;

                    if (dimStyleTable.Has(config.Name))
                    {
                        // 更新现有样式
                        var styleId = dimStyleTable[config.Name];
                        dimStyle = (DimStyleTableRecord)tr.GetObject(styleId, OpenMode.ForWrite);
                    }
                    else
                    {
                        // 创建新样式
                        dimStyleTable.UpgradeOpen();
                        dimStyle = new DimStyleTableRecord();
                        dimStyle.Name = config.Name;
                        dimStyleTable.Add(dimStyle);
                        tr.AddNewlyCreatedDBObject(dimStyle, true);
                        isNew = true;
                    }

                    // 设置样式属性
                    dimStyle.Dimtxt = config.TextHeight;
                    dimStyle.Dimexo = config.ExtensionLineOffset;
                    dimStyle.Dimexe = config.ExtensionLineExtend;
                    dimStyle.Dimasz = config.ArrowSize;
                    dimStyle.Dimgap = config.TextGap;
                    dimStyle.Dimdec = config.DecimalPlaces;
                    dimStyle.Dimtdec = config.TextDecimalPlaces;

                    // 设置文本样式（如果存在）
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    if (textStyleTable.Has(config.TextStyleName))
                    {
                        dimStyle.Dimtxsty = textStyleTable[config.TextStyleName];
                    }

                    tr.Commit();

                    var action = isNew ? "创建" : "更新";
                    doc.Editor.WriteMessage($"\n✓ {action}标注样式: {config.Name}");
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 标注样式操作失败: {ex.Message}");
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
                    }
                    else
                    {
                        tr.Abort();
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
                    }
                    else
                    {
                        tr.Abort();
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

        public bool TextStyleExists(string styleName)
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
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    bool exists = textStyleTable.Has(styleName);
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

        public bool DimensionStyleExists(string styleName)
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
                    var dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    bool exists = dimStyleTable.Has(styleName);
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

        public void CreateOrUpdateMLeaderStyle(MLeaderStyleConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

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
                    bool isNew = false;

                    if (mleaderStyleDict.Contains(config.Name))
                    {
                        // 更新现有样式
                        var styleId = mleaderStyleDict.GetAt(config.Name);
                        mleaderStyle = (MLeaderStyle)tr.GetObject(styleId, OpenMode.ForWrite);
                    }
                    else
                    {
                        // 创建新样式
                        mleaderStyleDict.UpgradeOpen();
                        mleaderStyle = new MLeaderStyle();
                        mleaderStyleDict.SetAt(config.Name, mleaderStyle);
                        tr.AddNewlyCreatedDBObject(mleaderStyle, true);
                        isNew = true;
                    }

                    // 设置文本样式（如果存在）
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    if (textStyleTable.Has(config.TextStyleName))
                    {
                        mleaderStyle.TextStyleId = textStyleTable[config.TextStyleName];
                    }

                    tr.Commit();

                    var action = isNew ? "创建" : "更新";
                    doc.Editor.WriteMessage($"\n✓ {action}多重引线样式: {config.Name}");
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\n✗ 多重引线样式操作失败: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        public bool StyleExists(string styleName)
        {
            // 通用方法：检查文本样式或标注样式是否存在
            return TextStyleExists(styleName) || DimensionStyleExists(styleName);
        }
    }
}

