using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using Exception = Autodesk.AutoCAD.BoundaryRepresentation.Exception;

namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        public static class MLeaderStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}_Mleader";
            public static string TextStyleName => $"0_Hy_{BaseConfig.Scale}";
        }
        /// <summary>
        /// 创建或更新引线样式并设置为当前。
        /// </summary>
        /// <param name="name">引线样式名称。</param>
        /// <returns>新建或更新的引线样式 ObjectId。</returns>
        public static ObjectId CreateMLeaderStyle(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("引线样式名称不能为空。");

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            string styleName = $"{name}_{BaseConfig.Scale}";

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var dbDic = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForWrite);
                    MLeaderStyle mle;
                    ObjectId mleId;

                    if (dbDic.Contains(styleName))
                    {
                        mleId = dbDic.GetAt(styleName);
                        mle = (MLeaderStyle)tr.GetObject(mleId, OpenMode.ForWrite);
                        ed.WriteMessage($"\n引线样式 '{styleName}' 已存在，正在更新。");
                    }
                    else
                    {
                        mle = new MLeaderStyle();
                        mleId = dbDic.SetAt(styleName, mle);
                        tr.AddNewlyCreatedDBObject(mle, true);
                        ed.WriteMessage($"\n正在创建引线样式 '{styleName}'。");
                    }

                    SetMleaderStyle(mle, Tools.TextStyleConfig.Name, BaseConfig.Scale);
                    db.MLeaderstyle = mleId;
                    ed.WriteMessage($"\n引线样式 '{styleName}' 已设置为当前。");
                    tr.Commit();
                    return mleId;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n创建或更新引线样式失败: {ex.Message}\n");
                    tr.Abort();
                    return ObjectId.Null;
                }
            }
        }

        /// <summary>
        /// 配置引线样式的属性。
        /// </summary>
        /// <param name="mLeaderStyle">引线样式对象。</param>
        /// <param name="textStyleName">文本样式名称。</param>
        /// <param name="scale">比例因子。</param>
        /// <returns>配置好的引线样式对象。</returns>
        public static MLeaderStyle SetMleaderStyle(MLeaderStyle mLeaderStyle, string textStyleName, double scale)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;

            Application.SetSystemVariable("Dimldrblk", "_DotSmall");
            var arrowSymbolId = GetArrowObjectId("DIMLDRBLK", "_DotSmall");

            var textStyleRecord = GetSymbolRecordFromDbByName<TextStyleTable, TextStyleTableRecord>(db.TextStyleTableId, textStyleName);

            mLeaderStyle.TextStyleId = textStyleRecord.Id;
            mLeaderStyle.TextHeight = Tools.TextStyleConfig.TextSize * scale;
            mLeaderStyle.TextColor = Color.FromColorIndex(ColorMethod.ByColor, 7);
            mLeaderStyle.TextAttachmentType = TextAttachmentType.AttachmentBottomLine;
            mLeaderStyle.ArrowSize = 2 * scale;
            mLeaderStyle.ArrowSymbolId = arrowSymbolId;
            mLeaderStyle.MaxLeaderSegmentsPoints = 2;
            mLeaderStyle.LandingGap = 0.5 * scale;
            mLeaderStyle.LeaderLineWeight = LineWeight.ByLayer;

            return mLeaderStyle;
        }
    }
}
