#nullable enable

using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Factories
{
    /// <summary>
    /// 将「设置」面板中当前名（与「置为当前」一致）解析为 rCs 出图用 <see cref="CrossSectionAnnotationStyle"/>，不另建 hy-rcs-* 样式。
    /// </summary>
    public sealed class RoadCsDrawStyleFactory
    {
        public RoadCsDrawStyleFactory()
        {
        }

        public CrossSectionAnnotationStyle Build(
            Document doc,
            SettingsPanelViewModel? settings)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            if (settings == null)
            {
                return new CrossSectionAnnotationStyle(
                    textStyleId: ObjectId.Null,
                    dimensionStyleId: ObjectId.Null,
                    mleaderStyleId: ObjectId.Null,
                    textHeightModel: 0.3,
                    mleaderLandingGapModel: 0.05,
                    mleaderArrowSizeModel: 0.2,
                    precision: 3);
            }

            settings.CommitTextAndMLeaderStylesToActiveDocument();
            return ResolveFromSettings(doc, settings);
        }

        private static CrossSectionAnnotationStyle ResolveFromSettings(
            Document doc,
            SettingsPanelViewModel settings)
        {
            var db = doc.Database;
            var textName = string.IsNullOrWhiteSpace(settings.TextStyleName) ? "0-hy-说明-S" : settings.TextStyleName.Trim();
            // 不解析设置里的 DimStyleName：不写入标注样式表，尺寸文字以几何为准；外观用当前图 Database.Dimstyle（只读）
            var mleaderName = string.IsNullOrWhiteSpace(settings.MLeaderStyleName)
                ? settings.BuildScaleContext().BuildMLeaderStyleName()
                : settings.MLeaderStyleName.Trim();

            var textH = GetActualTextHeightOr(settings, 0.3);
            var gap = GetActualMLeaderLandingOr(settings, 0.05);
            var arr = GetActualMLeaderArrowOr(settings, 0.2);
            // 与 BuildDimensionSegments / FormatWidthMeters 小数习惯一致，不随设置里的单位/精度切换
            const int prec = 3;

            ObjectId textId;
            ObjectId dimId;
            ObjectId mleaderId;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                textId = FindTextStyleId(tr, db, textName) ?? ObjectId.Null;
                dimId = db.Dimstyle;
                mleaderId = (mleaderName == null) ? ObjectId.Null : (FindMLeaderStyleId(tr, db, mleaderName) ?? ObjectId.Null);
                tr.Commit();
            }

            return new CrossSectionAnnotationStyle(
                textStyleId: textId,
                dimensionStyleId: dimId,
                mleaderStyleId: mleaderId,
                textHeightModel: textH,
                mleaderLandingGapModel: gap,
                mleaderArrowSizeModel: arr,
                precision: prec);
        }

        private static double GetActualTextHeightOr(SettingsPanelViewModel s, double fallback)
        {
            try
            {
                if (s.ActualTextHeight > 0) return s.ActualTextHeight;
            }
            catch { }
            return fallback;
        }

        private static double GetActualMLeaderLandingOr(SettingsPanelViewModel s, double fallback)
        {
            try
            {
                if (s.ActualMLeaderLandingGap > 0) return s.ActualMLeaderLandingGap;
            }
            catch { }
            return fallback;
        }

        private static double GetActualMLeaderArrowOr(SettingsPanelViewModel s, double fallback)
        {
            try
            {
                if (s.ActualMLeaderArrowSize > 0) return s.ActualMLeaderArrowSize;
            }
            catch { }
            return fallback;
        }

        private static ObjectId? FindTextStyleId(Transaction tr, Database db, string name)
        {
            var st = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false, true);
            return st.Has(name) ? st[name] : (ObjectId?)null;
        }

        private static ObjectId? FindMLeaderStyleId(Transaction tr, Database db, string name)
        {
            var st = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead, false, true);
            if (!st.Contains(name)) return null;
            return st.GetAt(name);
        }
    }
}
