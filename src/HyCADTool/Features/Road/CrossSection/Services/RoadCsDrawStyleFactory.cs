#nullable enable

using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.Drawing.Models;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Features.Road.CrossSection.Services
{
    /// <summary>
    /// 将「设置」面板中当前名（与「置为当前」一致）解析为 rCs 出图用 <see cref="CrossSectionAnnotationStyle"/>，不另建 hy-rcs-* 样式。
    /// </summary>
    public sealed class RoadCsDrawStyleFactory
    {
        /// <summary>rCs 顶部板块字与道路左/中/右轴线字、北南字统一使用的 TrueType 样式名。</summary>
        private const string RcsTopStripTextStyleName = "0-hy-说明-T";
        /// <summary>rCs 出图除图题外注记与顶部专项文字的纸面字高（mm）。</summary>
        private const double RcsAnnotationPaperHeightMm = 2.5;
        /// <summary>顶部 TrueType 样式与 <see cref="RcsAnnotationPaperHeightMm"/> 一致（纸面 2.5mm）。</summary>
        private const double RcsTopStripPaperHeightMm = RcsAnnotationPaperHeightMm;

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
                var ctx = ActiveScaleContextProvider.Current;
                double bodyH = PaperMmToModelHeight(ctx, RcsAnnotationPaperHeightMm);
                double rcsTopH = bodyH;

                return new CrossSectionAnnotationStyle(
                    textStyleName: null,
                    applyCurrentDocumentDimStyle: false,
                    mLeaderStyleName: null,
                    textHeightModel: bodyH,
                    mleaderLandingGapModel: 0.05,
                    mleaderArrowSizeModel: 0.2,
                    textLayerName: ResolveCommonTextLayerName(),
                    dimensionLayerName: ResolveCommonDimensionLayerName(),
                    titleLayerName: ResolveCommonTextLayerName(),
                    precision: 3,
                    rcsTopStripTextStyleName: RcsTopStripTextStyleName,
                    rcsTopStripTextHeightModel: rcsTopH);
            }

            settings.CommitTextAndMLeaderStylesToActiveDocument();
            return ResolveFromSettings(settings);
        }

        private static CrossSectionAnnotationStyle ResolveFromSettings(SettingsPanelViewModel settings)
        {
            var textName = string.IsNullOrWhiteSpace(settings.TextStyleName) ? "0-hy-说明-S" : settings.TextStyleName.Trim();
            // 不解析设置里的 DimStyleName：不写入标注样式表，尺寸文字以几何为准；外观用当前图 Database.Dimstyle（只读）
            var mleaderName = string.IsNullOrWhiteSpace(settings.MLeaderStyleName)
                ? settings.BuildScaleContext().BuildMLeaderStyleName()
                : settings.MLeaderStyleName.Trim();

            // rCs：尺寸/引线/横坡等「其它注记」固定纸面 2.5mm（不跟设置面板的 TextSize 走）
            double textH = RcsAnnotationPaperHeightMm * settings.BuildScaleContext().UnitFactor * settings.Scale;
            var gap = GetActualMLeaderLandingOr(settings, 0.05);
            var arr = GetActualMLeaderArrowOr(settings, 0.2);
            // 与 BuildDimensionSegments / FormatWidthMeters 小数习惯一致，不随设置里的单位/精度切换
            const int prec = 3;

            double rcsTopH = RcsTopStripPaperHeightMm * settings.BuildScaleContext().UnitFactor * settings.Scale; // 同 textH

            return new CrossSectionAnnotationStyle(
                textStyleName: textName,
                applyCurrentDocumentDimStyle: true,
                mLeaderStyleName: mleaderName,
                textHeightModel: textH,
                mleaderLandingGapModel: gap,
                mleaderArrowSizeModel: arr,
                textLayerName: ResolveCommonTextLayerName(settings),
                dimensionLayerName: ResolveCommonDimensionLayerName(settings),
                titleLayerName: ResolveCommonTextLayerName(settings),
                precision: prec,
                rcsTopStripTextStyleName: RcsTopStripTextStyleName,
                rcsTopStripTextHeightModel: rcsTopH);
        }

        private static double PaperMmToModelHeight(ScaleContext ctx, double paperMm) =>
            paperMm * ctx.UnitFactor * ctx.MainScale;

        private static string ResolveCommonTextLayerName(SettingsPanelViewModel? settings = null)
        {
            if (settings != null)
                return settings.TryResolveLayerName(LayerSemanticIds.CommonMLeader, LayerBuiltinDefaults.CommonMLeader);
            return UserLayerNameResolver.Get(LayerSemanticIds.CommonMLeader, LayerBuiltinDefaults.CommonMLeader);
        }

        private static string ResolveCommonDimensionLayerName(SettingsPanelViewModel? settings = null)
        {
            if (settings != null)
                return settings.TryResolveLayerName(LayerSemanticIds.CommonDimOuter, LayerBuiltinDefaults.CommonDimOuter);
            return UserLayerNameResolver.Get(LayerSemanticIds.CommonDimOuter, LayerBuiltinDefaults.CommonDimOuter);
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

    }
}
