using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
using System;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        public static class TextStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}";
            public static string BigFontFileName { get; } = "hztxt.shx";
            public static string FontFileName { get; } = "tssdeng.shx";
            public static double TextSize { get; } = 2.5;
            public static double TextXScale { get; } = 0.7;
        }

        /// <summary>
        /// 创建新的文本样式并设置为当前样式。
        /// </summary>
        public static ObjectId CreateTextStyle(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("文本样式名称不能为空。");
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
                    if (textStyleTable.Has(name))
                    {
                        ObjectId id = textStyleTable[name];
                        db.Textstyle = id;
                        tr.Commit();
                        return id;
                    }
                    var newTextStyle = new TextStyleTableRecord
                    {
                        Name = name,
                        BigFontFileName = TextStyleConfig.BigFontFileName,
                        FileName = TextStyleConfig.FontFileName,
                        TextSize = TextStyleConfig.TextSize * BaseConfig.Scale,
                        XScale = TextStyleConfig.TextXScale
                    };
                    ObjectId textStyleId = textStyleTable.Add(newTextStyle);
                    tr.AddNewlyCreatedDBObject(newTextStyle, true);
                    db.Textstyle = textStyleId;
                    tr.Commit();
                    return textStyleId;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n创建文本样式失败: {ex.Message}");
                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }
        public static ObjectId CreateTextStyle(string name, string fontFile, string bigFontFile, double textSize, double xScale)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (textStyleTable.Has(name))
                {
                    ObjectId styleId = textStyleTable[name];
                    var styleRec = (TextStyleTableRecord)tr.GetObject(styleId, OpenMode.ForWrite);
                    styleRec.FileName = fontFile;
                    styleRec.BigFontFileName = bigFontFile;
                    styleRec.TextSize = textSize;
                    styleRec.XScale = xScale;
                    db.Textstyle = styleId;
                    tr.Commit();
                    return styleId;
                }
                else
                {
                    textStyleTable.UpgradeOpen();
                    var newStyle = new TextStyleTableRecord
                    {
                        Name = name,
                        FileName = fontFile,
                        BigFontFileName = bigFontFile,
                        TextSize = textSize,
                        XScale = xScale
                    };
                    ObjectId styleId = textStyleTable.Add(newStyle);
                    tr.AddNewlyCreatedDBObject(newStyle, true);
                    db.Textstyle = styleId;
                    tr.Commit();
                    return styleId;
                }
            }
        }

    }
}