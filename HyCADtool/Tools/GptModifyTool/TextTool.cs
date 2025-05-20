using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
using System;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        /// <summary>
        /// 创建新的文本样式并设置为当前样式。
        /// </summary>
        /// <param name="name">文本样式名称</param>
        /// <param name="bigTextName">大字体文件名称</param>
        /// <param name="textName">字体文件名称</param>
        /// <param name="scale">比例因子</param>
        /// <returns>新创建的文本样式的 ObjectId，失败时返回 ObjectId.Null</returns>
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
                        ed.WriteMessage($"\n文本样式 '{name}' 已存在。");
                        ObjectId id = textStyleTable[name];
                        db.Textstyle = id; // 设置为当前样式
                        tr.Commit(); // 提交事务，确保设置生效
                        ed.WriteMessage($"\n文本样式 '{name}' 已设为当前。");
                        return id; // 返回现有样式的 ObjectId，而不是 Null
                    }
                    var newTextStyle = new TextStyleTableRecord
                    {
                        Name = name,
                        BigFontFileName = BaseConfig.TextStyleConfig.BigFontFileName,
                        FileName = BaseConfig.TextStyleConfig.FontFileName,
                        TextSize = BaseConfig.TextStyleConfig.TextSize * BaseConfig.Scale,
                        XScale = BaseConfig.TextStyleConfig.TextXScale
                    };
                    ObjectId textStyleId = textStyleTable.Add(newTextStyle);
                    tr.AddNewlyCreatedDBObject(newTextStyle, true);
                    db.Textstyle = textStyleId; // 设置为当前样式
                    tr.Commit(); // 提交事务
                    ed.WriteMessage($"\n创建文本样式 '{name}' 成功并设为当前。");
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
        /// <summary>
        /// 更新现有文本样式并设置为当前样式。
        /// </summary>
        /// <param name="name">文本样式名称</param>
        /// <param name="bigTextName">大字体文件名称</param>
        /// <returns>更新后的文本样式的 ObjectId，失败时返回 ObjectId.Null</returns>
        public static ObjectId UpdateTextStyle(string name, string bigTextName)
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
                    var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    if (!textStyleTable.Has(name))
                    {
                        ed.WriteMessage($"\n文本样式 '{name}' 不存在，无法更新。");
                        return ObjectId.Null;
                    }
                    ObjectId textStyleId = textStyleTable[name];
                    var styleRec = (TextStyleTableRecord)tr.GetObject(textStyleId, OpenMode.ForWrite);
                    styleRec.BigFontFileName = bigTextName;
                    styleRec.TextSize = BaseConfig.TextStyleConfig.TextSize * BaseConfig.Scale;
                    styleRec.XScale = BaseConfig.TextStyleConfig.TextXScale;
                    db.Textstyle = textStyleId;
                    db.Textsize = Reinforcement.TextSize;
                    Application.SetSystemVariable("TEXTSTYLE", name);
                    tr.Commit();
                    ed.WriteMessage($"\n更新文本样式 '{name}' 成功。");
                    return textStyleId;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n更新文本样式失败: {ex.Message}");
                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }
        /// <summary>
        /// 创建或设置文本样式。
        /// </summary>
        /// <param name="name">文本样式名称。</param>
        /// <param name="bigTextName">大字体文件名称。</param>
        /// <param name="textName">字体文件名称。</param>
        /// <param name="scale">比例因子。</param>
        /// <returns>创建或更新的文本样式的 ObjectId。</returns>
        public static ObjectId CreateOrUpdateTextStyle(string name, string bigTextName, string textName, double scale)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\n开始创建或更新文本样式。");
            ObjectId textStyleId = ObjectId.Null; // 初始化 ObjectId
                                                  // 锁定文档以进行更改
            using (doc.LockDocument())
            {
                // 开始一个事务
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                        if (textStyleTable.Has(name))
                        {
                            // 如果文本样式已存在，则获取并设置为当前文本样式
                            textStyleId = textStyleTable[name];
                            db.Textstyle = textStyleId;
                            var styleRec = (TextStyleTableRecord)tr.GetObject(textStyleId, OpenMode.ForWrite);
                            styleRec.BigFontFileName = bigTextName;
                            Application.SetSystemVariable("TEXTSTYLE", name);
                            ed.WriteMessage($"\n文本样式 '{name}' 已存在，已设置为当前文本样式。");
                        }
                        else
                        {
                            // 如果文本样式不存在，则创建新的文本样式
                            textStyleTable.UpgradeOpen();
                            var newTextStyle = new TextStyleTableRecord
                            {
                                Name = name,
                                BigFontFileName = bigTextName,
                                FileName = textName,
                                TextSize = Reinforcement.TextSize * scale,
                                XScale = 0.7
                            };
                            textStyleId = textStyleTable.Add(newTextStyle);
                            tr.AddNewlyCreatedDBObject(newTextStyle, true);
                            // 设置新创建的文本样式为当前文本样式
                            db.Textstyle = textStyleId;
                            Application.SetSystemVariable("TEXTSTYLE", name);
                            ed.WriteMessage($"\n创建了新的文本样式 '{name}'，并设置为当前文本样式。");
                        }
                        // 设置当前系统的文本高度
                        db.Textsize = Reinforcement.TextSize;
                        ed.WriteMessage($"\n设置当前文本高度为：{Reinforcement.TextSize}。");
                        // 提交事务
                        tr.Commit();
                        ed.WriteMessage("\n事务提交成功。");
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        // 捕获并记录 AutoCAD 异常
                        ed.WriteMessage($"\n错误: {ex.Message}");
                    }
                }
            }
            ed.WriteMessage("\n文本样式创建或更新完成。");
            // 返回文本样式的 ObjectId
            return textStyleId;
        }
    }
}