using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Drawing.ValueObjects;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Features.TitleBlock.Services;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Features.Road.CrossSection.Services.FillRendering;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Features.Road.CrossSection.Services
{
    /// <summary>
    /// 横断面绘图模式（M7.4）。
    /// <list type="bullet">
    ///   <item><see cref="WithStructureThickness"/>：原 v1 行为——完整绘制轮廓 + 条带 + 尺寸 + 填充 + 标注（"出带结构厚度轮廓"）。</item>
    ///   <item><see cref="SingleLine"/>：仅绘制上表面竖直投影轮廓（顶面 polyline + 中心线），用于"平面图单线"用途。</item>
    /// </list>
    /// </summary>
    public enum CrossSectionDrawMode
    {
        WithStructureThickness = 0,
        SingleLine = 1,
        TopSurfaceWithAnnotation = 2,
    }

    /// <summary>
    /// M3 标准横断面图出图服务。
    ///
    /// 职责：把 <see cref="CrossSectionFigure"/> 绘制到 AutoCAD ModelSpace：
    /// <list type="bullet">
    ///   <item>主轮廓 LWPolyline（闭合）+ 顶面 polyline。</item>
    ///   <item>按 <see cref="FigurePanel.Kind"/> 分色的分段 Hatch（可选；v1 仅 LWPoly 切片）。</item>
    ///   <item>中心虚线 Line。</item>
    ///   <item>道路下仅一道总宽尺寸（Tier=0）；顶排总宽与分条尺寸已取消；<see cref="AlignedDimension"/>，不依赖 DIMSTYLE 文字替代。</item>
    ///   <item>横坡（DBText + 竖向指坡箭头，箭头端与路顶面留 2mm 纸面间距）；分隔带不生成横坡；顶部条带名（DBText）。</item>
    ///   <item>方位箭头、图题。</item>
    ///   <item>所有生成的实体挂 HY_ROAD Xdata：<c>ID=template.Id</c>，<c>KIND=CrossSectionStandard</c>。</item>
    /// </list>
    ///
    /// 幂等策略：在同一 <see cref="Template.Id"/> 再次调用 <see cref="Draw"/> 前，
    /// 先 <see cref="Clear"/> 删掉旧实体；<see cref="RoadAlignmentService"/> 的"拾取-重建"模式。
    ///
    /// 坐标系：Figure 已是 "m" 单位。服务按 <paramref name="origin"/> 平移，
    /// 并按 <paramref name="modelUnitPerMeter"/> 缩放（通常等于 1.0，DWG 建模以 m 为单位）。
    /// </summary>
    public sealed class RoadStandardSectionDrawService
    {
        /// <summary>XData KIND 常量：标准横断面图的全部实体统一用这个 KIND 回收。</summary>
        public const string DrawingKind = "CrossSectionStandard";

        /// <summary>
        /// 把 Figure 绘制到 ModelSpace 指定位置。
        /// </summary>
        /// <param name="transaction">调用方事务（不自主 Commit）。</param>
        /// <param name="database">活动 Database。</param>
        /// <param name="figure">来自 <see cref="Services.Road.CrossSectionLayoutBuilder.ToFigure"/> 的绘图指令。</param>
        /// <param name="template">关联的持久化 Template（用于 Xdata.ID 挂钩）。</param>
        /// <param name="origin">图面插入点（WCS 二维）。</param>
        /// <param name="modelUnitPerMeter">模型空间单位 / 米，默认 1.0（DWG 单位=m）。</param>
        /// <returns>生成的实体数量。</returns>
        public int Draw(
            Transaction transaction,
            Database database,
            CrossSectionFigure figure,
            Template template,
            Point2d origin,
            double modelUnitPerMeter = 1.0)
            => Draw(transaction, database, figure, template, origin, modelUnitPerMeter, CrossSectionDrawMode.WithStructureThickness, null, null, null, planStripVerticalOffsetMeters: 5.0, drawSectionStructureFills: true);

        /// <summary>
        /// M7.4 重载：指定绘图模式。
        /// <para><see cref="CrossSectionDrawMode.SingleLine"/> 时跳过板块闭合填充 / 尺寸链 / 标注 / 标题，只保留：
        /// 顶面轮廓 Polyline + 中心虚线 + 方位箭头，专供平面图单线投影使用。</para>
        /// </summary>
        /// <param name="planStripVerticalOffsetMeters">平面带底边在图面中相对「路顶 ymax+1.2m」的附加上移量（m），与设置「道路 · 平面带上移」一致，默认 5。</param>
        public int Draw(
            Transaction transaction,
            Database database,
            CrossSectionFigure figure,
            Template template,
            Point2d origin,
            double modelUnitPerMeter,
            CrossSectionDrawMode mode,
            CrossSectionLayout layout = null,
            CrossSectionAnnotationStyle annotationStyle = null,
            DrawingSheetTitleSpec sheetTitleSpec = null,
            double planStripVerticalOffsetMeters = 5.0,
            bool drawSectionStructureFills = true)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (figure == null) throw new ArgumentNullException(nameof(figure));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (modelUnitPerMeter <= 0) throw new ArgumentOutOfRangeException(nameof(modelUnitPerMeter));
            if (double.IsNaN(planStripVerticalOffsetMeters) || double.IsInfinity(planStripVerticalOffsetMeters))
                throw new ArgumentOutOfRangeException(nameof(planStripVerticalOffsetMeters));

            HyRoadXdata.EnsureRegApp(transaction, database);
            EnsureLayersVisible(transaction, database);

            var titleSpec = sheetTitleSpec ?? DrawingSheetTitleSpec.RoadCrossSectionDefault;
            EnsureStyleLayersVisible(transaction, database, annotationStyle, titleSpec);

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            double s = modelUnitPerMeter;
            Point3d Map(double x, double y) => new Point3d(origin.X + x * s, origin.Y + y * s, 0);

            int added = 0;

            // ---------------- 1. 轮廓 polyline（顶面折线） ----------------
            if (figure.Vertices.Count >= 2)
            {
                var outline = new Polyline(figure.Vertices.Count);
                for (int i = 0; i < figure.Vertices.Count; i++)
                {
                    var v = figure.Vertices[i];
                    outline.AddVertexAt(i, new Point2d(origin.X + v.X * s, origin.Y + v.Y * s), 0, 0, 0);
                }
                outline.Layer = HyRoadLayers.CrossSectionOutlineLayer;
                outline.ColorIndex = 256; // ByLayer
                ms.AppendEntity(outline);
                transaction.AddNewlyCreatedDBObject(outline, true);
                TagEntity(transaction, database, outline, template.Id);
                added++;
            }

            // ---------------- 2. 基线 + 左右半宽面板(作为封闭 polyline) ----------------
            // SingleLine 模式下仅画顶面投影，不画板块闭合 → 跳过 steps 2 / 4 / 5 / 6 / 7 / 8 / 10。
            if (mode == CrossSectionDrawMode.SingleLine)
            {
                // 先画中心线（step 3），再画方位箭头（step 9），直接 return。
                double ymaxS = 0;
                foreach (var v in figure.Vertices) if (v.Y > ymaxS) ymaxS = v.Y;
                const double singleLineCenterXOffsetM = 0.3;
                var centerS = new Line(
                    new Point3d(origin.X + singleLineCenterXOffsetM * s, origin.Y - 0.5 * s, 0),
                    new Point3d(origin.X + singleLineCenterXOffsetM * s, origin.Y + (ymaxS + 0.6) * s, 0))
                {
                    Layer = HyRoadLayers.CrossSectionCenterlineLayer,
                    ColorIndex = 256,
                };
                TrySetLinetype(transaction, database, centerS, "HIDDEN");
                ms.AppendEntity(centerS);
                transaction.AddNewlyCreatedDBObject(centerS, true);
                TagEntity(transaction, database, centerS, template.Id);
                added++;

                if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel) ||
                    !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
                {
                    added += DrawOrientation(transaction, ms, database, template.Id,
                        origin, figure.Orientation, s, annotationStyle);
                }
                return added;
            }

            // 路面/绿化/分隔带等"路面板块"用"顶 + 底"闭合（构成立柱）；
            // 路牙（Kerb）的 vertices 本身已构成完整的 L 型凸起多边形，直接自闭合（不加底边）。
            // TopSurfaceWithAnnotation 只需要顶面轮廓 + 标注，不绘制板块闭合。
            if (mode != CrossSectionDrawMode.TopSurfaceWithAnnotation)
            {
                foreach (var panel in figure.Panels)
                {
                    int i0 = Math.Max(0, panel.StartVertexIndex);
                    int i1 = Math.Min(figure.Vertices.Count - 1, panel.EndVertexIndex);
                    if (i1 - i0 < 1) continue;

                    bool isKerbPanel = panel.Kind == TemplateComponentKind.Kerb;
                    int count = isKerbPanel ? (i1 - i0 + 1) : (i1 - i0 + 1) + 2;
                    var pl = new Polyline(count);
                    int idx = 0;
                    for (int i = i0; i <= i1; i++)
                    {
                        var v = figure.Vertices[i];
                        pl.AddVertexAt(idx++, new Point2d(origin.X + v.X * s, origin.Y + v.Y * s), 0, 0, 0);
                    }
                    if (!isKerbPanel)
                    {
                        // 底边两点：从最右回到最左，与顶面构成"立柱"
                        pl.AddVertexAt(idx++, new Point2d(origin.X + figure.Vertices[i1].X * s, origin.Y), 0, 0, 0);
                        pl.AddVertexAt(idx, new Point2d(origin.X + figure.Vertices[i0].X * s, origin.Y), 0, 0, 0);
                    }
                    pl.Closed = true;
                    pl.Layer = PickPanelLayer(panel.Kind);
                    pl.ColorIndex = 256;

                    ms.AppendEntity(pl);
                    transaction.AddNewlyCreatedDBObject(pl, true);
                    TagEntity(transaction, database, pl, template.Id);
                    added++;
                }
            }

            if (mode == CrossSectionDrawMode.WithStructureThickness && layout != null)
            {
                added += DrawStructureLayerFills(
                    transaction, ms, database, template.Id, figure, layout, origin, s, drawSectionStructureFills);
            }

            // ---------------- 3. 字高 / 标准图区域标高 ----------------
            double ymax = 0;
            foreach (var v in figure.Vertices) if (v.Y > ymax) ymax = v.Y;
            double txtH = Math.Max(0.15, 0.3 * s);
            if (annotationStyle != null && annotationStyle.TextHeightModel > 1e-6)
            {
                txtH = annotationStyle.TextHeightModel;
            }
            double planStripBottomY = ymax + 1.2 + planStripVerticalOffsetMeters;
            double planStripTopY = planStripBottomY + Math.Max(0.5, figure.PlanStripLength);
            double axisBottomY = (figure.Vertices.Count > 0 ? figure.Vertices.Min(v => v.Y) : 0) - 2.8;
            // 左/中/右竖向线画至「平面带上侧 +5m」行，与北南向紫线、轴线字同高。
            const double rcsTopAxisAndOrientationM = 5.0;
            double axisTopY = planStripTopY + rcsTopAxisAndOrientationM;

            // ---------------- 4. 顶部平面带 ----------------
            if (layout != null)
            {
                added += DrawPlanStrip(transaction, ms, database, template.Id, layout, origin, s, planStripBottomY, planStripTopY);
            }

            // ---------------- 5. 左/中/右轴线（竖线自断面底至平面带+5m；字在整段竖线竖向中点，左右红线另 ±0.3m 水平微移） ----------------
            added += DrawAxisMarkers(
                transaction, ms, database, template.Id, figure, origin, s, axisBottomY, axisTopY, annotationStyle, txtH);

            // ---------------- 6. 上下尺寸链（显式绿色）；整链图面 y 统一下移 0.5m，避免与路面线拥挤 ----------------
            const double rcsDimensionChainDropFigureM = 0.5;
            double dimRowGap = Math.Max(0.45, txtH * 1.9);
            foreach (var seg in figure.DimensionSegments)
            {
                double featureY;
                double dimOffset;
                if (seg.Track == FigureDimensionTrack.Top)
                {
                    featureY = planStripTopY - rcsDimensionChainDropFigureM;
                    dimOffset = seg.Tier == 0 ? dimRowGap * 2.2 : dimRowGap;
                }
                else
                {
                    featureY = 0.0 - rcsDimensionChainDropFigureM;
                    dimOffset = seg.Tier == 0 ? -dimRowGap * 2.2 : -dimRowGap;
                }

                var a = new Point3d(origin.X + seg.StartX * s, origin.Y + featureY * s, 0);
                var b = new Point3d(origin.X + seg.EndX * s, origin.Y + featureY * s, 0);
                added += DrawDimensionSegment(transaction, ms, database, template.Id, a, b, annotationStyle, txtH, dimOffset * s);
            }

            // ---------------- 7. 横坡：文字 + 下方水平箭头（箭头方向随坡向，从高侧指向低侧） ----------------
            var slopeLayer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            ObjectId slopeAnnotTextStyle = TryGetTextStyleId(transaction, database, annotationStyle?.TextStyleName);
            foreach (var slope in figure.SlopeLabels)
            {
                added += DrawCrossSectionSlopeTextAndHorizArrow(
                    transaction, ms, database, template.Id, slope, Map, s, txtH, slopeLayer,
                    slopeAnnotTextStyle);
            }

            // ---------------- 8. 标高：MLeader 避让盒
            // 同 x 的多个标高 → 并成一条 MText（\P 叠行）+ 锚 y 用竖向中点（图2）
            // ----------------
            var mleaderTextBoxes = new List<MLeaderTextAabb>();
            foreach (var g in GroupHeightLabelsByColocatedX(figure.HeightLabels))
            {
                added += AddHeightMLeader(
                    transaction, ms, database, template.Id, g, Map, s, txtH, annotationStyle, mleaderTextBoxes);
            }

            // ---------------- 9. 顶部竖排板块名（距平面带上侧 2.5m；样式/字高经 RcsTop 收口） ----------------
            string topLabelLayer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            ResolveRcsTopStripStyle(transaction, database, annotationStyle, slopeAnnotTextStyle, txtH,
                out ObjectId topLabelTextStyle, out double topLabelTxtH);
            const double rcsTopBandLabelOffsetM = 2.5;
            double topLabelY = planStripTopY + rcsTopBandLabelOffsetM;
            foreach (var top in figure.TopLabels)
            {
                added += AddText(
                    transaction, ms, database, template.Id,
                    new Point3d(origin.X + top.CenterX * s, origin.Y + topLabelY * s, 0),
                    top.Text,
                    topLabelLayer,
                    topLabelTxtH,
                    rotationDeg: 90,
                    alignCenter: true,
                    textStyleId: topLabelTextStyle);
            }

            // ---------------- 10. 方位箭头（北南向线距平面带上侧 5m，与轴线字同高；字高/样式 RcsTop） ----------------
            if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel) ||
                !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
            {
                double orientationYFig = planStripTopY + rcsTopAxisAndOrientationM;
                added += DrawOrientation(transaction, ms, database, template.Id,
                    origin, figure.Orientation, s, annotationStyle, orientationYFig, useRcsTopStripText: true);
            }

            // ---------------- 11. 图题（规格化：双下划线 + 比例 + 可选左十字/方格） ----------------
            if (!string.IsNullOrWhiteSpace(figure.Title.Text))
            {
                // 图名相对 Figure 再下移 1cm（纸面，同 ActualTextHeight 换算）
                var scaleCtxForTitle = ActiveScaleContextProvider.Current;
                double oneCmPaperMm = 10.0;
                double titleDropFigure =
                    (oneCmPaperMm * scaleCtxForTitle.UnitFactor * scaleCtxForTitle.MainScale) / s;
                var p = Map(figure.Title.CenterX, figure.Title.Y - titleDropFigure);
                string titleText = DrawingSheetTitleText.RemoveTrailingScaleInTitle(figure.Title.Text);
                if (string.IsNullOrWhiteSpace(titleText)) titleText = figure.Title.Text.Trim();
                // 图题主/副字高：与设置里「文字高度」一致，存纸面 mm；→ 模型单位 = paperMm×UnitFactor×MainScale（同 ActualTextHeight）
                var scaleCtx = ActiveScaleContextProvider.Current;
                double titleH;
                if (titleSpec.MainTextHeightModel > 1e-9)
                    titleH = titleSpec.MainTextHeightModel * scaleCtx.UnitFactor * scaleCtx.MainScale;
                else if (annotationStyle != null && annotationStyle.TextHeightModel > 1e-6)
                    titleH = annotationStyle.TextHeightModel;
                else
                    titleH = Math.Max(0.3, 0.6 * s);
                ObjectId mainStyleId = TryGetTextStyleId(transaction, database, titleSpec.MainTextStyleName);
                if (mainStyleId.IsNull) mainStyleId = slopeAnnotTextStyle;
                ObjectId scaleStyleId = TryGetTextStyleId(transaction, database, titleSpec.ScaleTextStyleName);
                if (scaleStyleId.IsNull) scaleStyleId = mainStyleId;
                double scaleH = titleSpec.ScaleTextHeightModel > 1e-9
                    ? titleSpec.ScaleTextHeightModel * scaleCtx.UnitFactor * scaleCtx.MainScale
                    : titleSpec.ScaleTextHeightFactor * titleH;
                string titleLayerOverride = annotationStyle?.TitleLayerName;
                void AppendTitle(Entity e)
                {
                    ms.AppendEntity(e);
                    transaction.AddNewlyCreatedDBObject(e, true);
                    TagEntity(transaction, database, e, template.Id);
                }
                added += DrawingSheetTitleDrawer.Draw(
                    database,
                    AppendTitle,
                    titleSpec,
                    p,
                    titleText,
                    figure.ScaleDenominator,
                    titleH,
                    mainStyleId,
                    scaleStyleId,
                    scaleH,
                    titleLayerOverride,
                    useWhiteColor: true);
            }

            return added;
        }

        /// <summary>同横坐标（竖向台阶/缝）的标高并成一条引线，MText 用 \P 叠行、锚点 y 取中点。</summary>
        private static List<FigureHeightLabel[]> GroupHeightLabelsByColocatedX(
            IReadOnlyList<FigureHeightLabel> labels, double xTolMeters = 1e-3)
        {
            var result = new List<List<FigureHeightLabel>>();
            if (labels == null || labels.Count == 0) return new List<FigureHeightLabel[]>();
            var ordered = labels.OrderBy(h => h.PositionX).ThenByDescending(h => h.PositionY);
            foreach (var h in ordered)
            {
                List<FigureHeightLabel> target = null;
                foreach (var g in result)
                {
                    if (Math.Abs(g[0].PositionX - h.PositionX) < xTolMeters)
                    {
                        target = g;
                        break;
                    }
                }
                if (target == null)
                {
                    target = new List<FigureHeightLabel>();
                    result.Add(target);
                }
                target.Add(h);
            }
            foreach (var g in result)
            {
                g.Sort((a, b) => b.PositionY.CompareTo(a.PositionY));
            }
            return result.Select(x => x.ToArray()).ToList();
        }

        private int AddHeightMLeader(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            IReadOnlyList<FigureHeightLabel> group,
            Func<double, double, Point3d> map,
            double s,
            double textH,
            CrossSectionAnnotationStyle annotationStyle,
            List<MLeaderTextAabb> placed)
        {
            if (group == null || group.Count == 0) return 0;
            var parts = new List<string>(group.Count);
            foreach (var h in group)
            {
                var t = h.Text?.Trim() ?? string.Empty;
                if (t.Length > 0) parts.Add(t);
            }
            if (parts.Count == 0) return 0;
            string content = parts.Count == 1
                ? parts[0]
                : string.Join("\\P", parts);
            Point3d anchor;
            if (group.Count == 1)
            {
                var h0 = group[0];
                anchor = map(h0.PositionX, h0.PositionY);
            }
            else
            {
                // 引线起点取较低处（y 小）；文字仍上行较高、下行较低（组内已按 Y 降序拼入 parts）
                double yMin = group.Min(h => h.PositionY);
                double x0 = group[0].PositionX;
                anchor = map(x0, yMin);
            }
            var endPoint = ResolveMLeaderTextPlacement(
                anchor, content, textH, s, placed,
                initialOffset: (dx: 0.28 * s, dy: 0.10 * s));
            var ml = MLeaderExtensions.CreateMLeaderSinglePoint(
                anchor, endPoint, content, TryGetMLeaderStyleId(tr, db, annotationStyle));
            ml.Layer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            SetEntityMLeaderLightGreen(ml);
            ms.AppendEntity(ml);
            tr.AddNewlyCreatedDBObject(ml, true);
            TagEntity(tr, db, ml, templateId);
            return 1;
        }

        /// <summary>估算 MLeader 文字占用矩形（WCS，用于相交避让；略保守略大于真实 MText 边界）。</summary>
        private static MLeaderTextAabb EstimateMLeaderTextBounds(Point3d lowerLeft, string content, double textH)
        {
            if (textH < 1e-9) textH = 0.15;
            var raw = content ?? string.Empty;
            // MText 行分隔 \P
            var lines = raw.Split(new[] { "\\P" }, StringSplitOptions.None);
            int lineCount = Math.Max(1, lines.Length);
            int maxLen = 1;
            foreach (var line in lines)
            {
                int L = (line ?? string.Empty).Length;
                if (L > maxLen) maxLen = L;
            }
            double w = Math.Max(textH * 1.4, maxLen * textH * 0.45);
            double h = textH * 1.25 * lineCount;
            return MLeaderTextAabb.FromLowerLeft(lowerLeft, w, h);
        }

        /// <summary>
        /// 自初始偏移起沿 +Y 阶梯抬升，直到与已放置的引线文字盒不再相交（外扩 margin）。
        /// </summary>
        private static Point3d ResolveMLeaderTextPlacement(
            Point3d anchor,
            string content,
            double textH,
            double s,
            List<MLeaderTextAabb> placed,
            (double dx, double dy) initialOffset)
        {
            double step = Math.Max(0.08 * s, textH * 0.4);
            double margin = Math.Max(0.04 * s, textH * 0.2);
            var p = new Point3d(anchor.X + initialOffset.dx, anchor.Y + initialOffset.dy, 0);
            for (int i = 0; i < 56; i++)
            {
                var box = EstimateMLeaderTextBounds(p, content, textH);
                if (!placed.Any(b => b.InflatedIntersects(box, margin)))
                {
                    placed.Add(box);
                    return p;
                }
                p = new Point3d(p.X, p.Y + step, 0);
            }
            var fallback = EstimateMLeaderTextBounds(p, content, textH);
            placed.Add(fallback);
            return p;
        }

        /// <summary>与 <see cref="MLeaderExtensions.CreateMLeaderSinglePoint"/> 配套的文字外包矩形（2D，米）。</summary>
        private struct MLeaderTextAabb
        {
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;

            public static MLeaderTextAabb FromLowerLeft(Point3d lowerLeft, double w, double h)
            {
                return new MLeaderTextAabb
                {
                    MinX = lowerLeft.X,
                    MinY = lowerLeft.Y,
                    MaxX = lowerLeft.X + w,
                    MaxY = lowerLeft.Y + h,
                };
            }

            public bool InflatedIntersects(MLeaderTextAabb o, double margin)
            {
                return !(MaxX + margin < o.MinX - margin
                         || o.MaxX + margin < MinX - margin
                         || MaxY + margin < o.MinY - margin
                         || o.MaxY + margin < MinY - margin);
            }
        }

        /// <summary>
        /// 清除与 <paramref name="templateId"/> 绑定的全部横断面实体，保证幂等重画。
        /// </summary>
        public int Clear(Transaction transaction, Database database, Guid templateId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (templateId == Guid.Empty) return 0;

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, DrawingKind, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid != templateId) continue;
                toErase.Add(id);
            }

            foreach (var id in toErase)
            {
                var ent = (Entity)transaction.GetObject(id, OpenMode.ForWrite);
                ent.Erase();
            }
            return toErase.Count;
        }

        // =========================================================================
        //  Internals
        // =========================================================================

        private static ObjectId TryGetTextStyleId(Transaction tr, Database db, string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName)) return ObjectId.Null;
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            return tst.Has(styleName) ? tst[styleName] : ObjectId.Null;
        }

        private static ObjectId TryGetMLeaderStyleId(Transaction tr, Database db, CrossSectionAnnotationStyle a)
        {
            if (a == null || string.IsNullOrWhiteSpace(a.MLeaderStyleName)) return ObjectId.Null;
            var st = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
            if (!st.Contains(a.MLeaderStyleName)) return ObjectId.Null;
            return st.GetAt(a.MLeaderStyleName);
        }

        /// <summary>顶部板块/轴线/北南字：优先 Rcs 顶部样式名解析结果与 Rcs 字高，否则用回退值。</summary>
        private static void ResolveRcsTopStripStyle(
            Transaction tr,
            Database db,
            CrossSectionAnnotationStyle annotationStyle,
            ObjectId fallbackStyleId,
            double fallbackHeightModel,
            out ObjectId styleId,
            out double heightModel)
        {
            if (annotationStyle != null
                && !string.IsNullOrWhiteSpace(annotationStyle.RcsTopStripTextStyleName)
                && annotationStyle.RcsTopStripTextHeightModel > 1e-6)
            {
                var id = TryGetTextStyleId(tr, db, annotationStyle.RcsTopStripTextStyleName);
                if (!id.IsNull)
                {
                    styleId = id;
                    heightModel = annotationStyle.RcsTopStripTextHeightModel;
                    return;
                }
            }

            styleId = fallbackStyleId;
            heightModel = fallbackHeightModel;
        }

        private static double GetAxisLabelXOffsetMeters(FigureAxisMarker axis)
        {
            var label = axis.Label ?? string.Empty;
            if (label.IndexOf("左红线", StringComparison.Ordinal) >= 0) return -0.3;
            if (label.IndexOf("右红线", StringComparison.Ordinal) >= 0) return 0.3;
            if (label.IndexOf("中心", StringComparison.Ordinal) >= 0) return 0.0;
            if (axis.X < -1e-6) return -0.3;
            if (axis.X > 1e-6) return 0.3;
            return 0.0;
        }

        private static void SetEntityAciWhite(Entity e) =>
            e.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);

        /// <summary>标高引线 MLeader 使用淡绿（与尺寸纯绿、其它白字区分）。</summary>
        private static void SetEntityMLeaderLightGreen(Entity e) =>
            e.Color = Color.FromRgb(170, 220, 170);

        private int DrawStructureLayerFills(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            CrossSectionFigure figure,
            CrossSectionLayout layout,
            Point2d origin,
            double s,
            bool drawSectionFillHatch = true)
        {
            int n = 0;
            if (layout == null || figure == null) return 0;
            foreach (var (panel, band) in EnumerateRoadPanelsWithBands(layout, figure))
            {
                if (band.StructureScheme?.Layers == null) continue;
                int i0 = Math.Max(0, panel.StartVertexIndex);
                int i1 = Math.Min(figure.Vertices.Count - 1, panel.EndVertexIndex);
                if (i1 - i0 < 1) continue;
                var vx0 = figure.Vertices[i0];
                var vx1 = figure.Vertices[i1];
                double x0 = vx0.X;
                double y0s = vx0.Y;
                double x1 = vx1.X;
                double y1s = vx1.Y;
                double depth = 0;
                string layerName = PickPanelLayer(panel.Kind);
                foreach (var sl in band.StructureScheme.Layers)
                {
                    if (sl == null) continue;
                    sl.MigrateLegacyPatternName();
                    double th = Math.Max(0, sl.ThicknessCm) / 100.0;
                    if (th < 1e-7) continue;
                    var sf = sl.SectionFill ?? new LayerFillSettings();
                    double y0a = y0s - depth;
                    double y1a = y1s - depth;
                    double y0b = y0s - depth - th;
                    double y1b = y1s - depth - th;
                    var pl = new Polyline(4);
                    pl.AddVertexAt(0, new Point2d(origin.X + x0 * s, origin.Y + y0a * s), 0, 0, 0);
                    pl.AddVertexAt(1, new Point2d(origin.X + x1 * s, origin.Y + y1a * s), 0, 0, 0);
                    pl.AddVertexAt(2, new Point2d(origin.X + x1 * s, origin.Y + y1b * s), 0, 0, 0);
                    pl.AddVertexAt(3, new Point2d(origin.X + x0 * s, origin.Y + y0b * s), 0, 0, 0);
                    pl.Closed = true;
                    pl.Layer = layerName;
                    pl.ColorIndex = 256;
                    ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    TagEntity(tr, db, pl, templateId);
                    n++;
                    if (drawSectionFillHatch)
                    {
                        n += LayerFillApplier.ApplyHatch(tr, ms, db, templateId, pl, sf, layerName);
                    }
                    double cxM = 0.5 * (x0 + x1);
                    double yTop = 0.5 * (y0a + y1a);
                    double yBot = 0.5 * (y0b + y1b);
                    double cY = 0.5 * (yTop + yBot);
                    var c = new Point3d(origin.X + cxM * s, origin.Y + cY * s, 0);
                    n += LayerFillApplier.ApplyBlock(tr, ms, db, templateId, c, sf, layerName);
                    depth += th;
                }
            }
            return n;
        }

        private static IEnumerable<(FigurePanel panel, CrossSectionBand band)> EnumerateRoadPanelsWithBands(
            CrossSectionLayout layout, CrossSectionFigure figure)
        {
            if (layout == null || figure?.Panels == null) yield break;
            var q = new Queue<CrossSectionBand>();
            for (int s = layout.LeftBands.Count - 1; s >= 0; s--) q.Enqueue(layout.LeftBands[s]);
            for (int s = 0; s < layout.RightBands.Count; s++) q.Enqueue(layout.RightBands[s]);

            foreach (var p in figure.Panels)
            {
                if (p.Kind == TemplateComponentKind.MedianStrip) continue;
                if (p.Kind == TemplateComponentKind.Kerb) continue;
                if (q.Count == 0) yield break;
                yield return (p, q.Dequeue());
            }
        }

        private static LayerFillSettings ResolvePlanViewFillForBand(CrossSectionBand? band)
        {
            if (band is null)
                return new LayerFillSettings { PatternEnabled = true, PatternName = "SOLID" };
            var b = band.Value;
            var scheme = b.StructureScheme;
            if (scheme?.Layers == null)
                return new LayerFillSettings { PatternEnabled = true, PatternName = "SOLID" };
            foreach (var L in scheme.Layers)
            {
                if (L == null) continue;
                L.MigrateLegacyPatternName();
                if (L.LayerKind != StructureLayerKind.Surface) continue;
                if (L.PlanFill == null) continue;
                var p = L.PlanFill.Clone();
                if (!p.PatternEnabled || string.IsNullOrWhiteSpace(p.PatternName))
                {
                    p.PatternEnabled = true;
                    p.PatternName = "SOLID";
                }
                return p;
            }
            return new LayerFillSettings { PatternEnabled = true, PatternName = "SOLID" };
        }

        private int DrawPlanStrip(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            CrossSectionLayout layout,
            Point2d origin,
            double s,
            double bottomY,
            double topY)
        {
            if (layout == null) return 0;
            int added = 0;
            foreach (var span in EnumeratePlanStripSpans(layout))
            {
                if (span.Kind == TemplateComponentKind.MedianStrip) continue;
                var pl = new Polyline(4);
                pl.AddVertexAt(0, new Point2d(origin.X + span.StartX * s, origin.Y + bottomY * s), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(origin.X + span.EndX * s, origin.Y + bottomY * s), 0, 0, 0);
                pl.AddVertexAt(2, new Point2d(origin.X + span.EndX * s, origin.Y + topY * s), 0, 0, 0);
                pl.AddVertexAt(3, new Point2d(origin.X + span.StartX * s, origin.Y + topY * s), 0, 0, 0);
                pl.Closed = true;
                string plLayer = PickPanelLayer(span.Kind);
                pl.Layer = plLayer;
                pl.ColorIndex = 256;
                ms.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                added++;
                TagEntity(tr, db, pl, templateId);

                var planView = ResolvePlanViewFillForBand(span.RoadBand);
                var hatchFill = planView.Clone();
                if (!hatchFill.PatternEnabled || string.IsNullOrWhiteSpace(hatchFill.PatternName))
                {
                    hatchFill.PatternEnabled = true;
                    hatchFill.PatternName = "SOLID";
                }
                added += LayerFillApplier.ApplyHatch(tr, ms, db, templateId, pl, hatchFill, plLayer);
                double cx = 0.5 * (span.StartX + span.EndX) * s + origin.X;
                double cy = 0.5 * (bottomY + topY) * s + origin.Y;
                added += LayerFillApplier.ApplyBlock(tr, ms, db, templateId, new Point3d(cx, cy, 0), planView, plLayer);
            }

            return added;
        }

        private int DrawAxisMarkers(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            CrossSectionFigure figure,
            Point2d origin,
            double s,
            double bottomY,
            double lineTopY,
            CrossSectionAnnotationStyle annotationStyle,
            double fallbackTextHeight)
        {
            if (figure == null || figure.AxisMarkers == null || figure.AxisMarkers.Count == 0) return 0;
            int added = 0;
            string textLayer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            double baseH = annotationStyle != null && annotationStyle.TextHeightModel > 1e-6
                ? annotationStyle.TextHeightModel
                : Math.Max(0.3, fallbackTextHeight);
            ObjectId axisFallbackText = TryGetTextStyleId(tr, db, annotationStyle?.TextStyleName);
            ResolveRcsTopStripStyle(tr, db, annotationStyle, axisFallbackText, baseH,
                out ObjectId textStyleId, out double textH);

            // 竖线 x = 图面轴坐标（中心线 x=0 与分隔带几何中心一致，不再 +0.3m 偏移）。
            // 注记 y：均在「bottomY～lineTopY」整段竖线的竖向中点，与左/右红线同列；左右红线 x 再 ±0.3m 见 GetAxisLabelXOffsetMeters。
            double yTextFig = (bottomY + lineTopY) * 0.5;
            foreach (var axis in figure.AxisMarkers)
            {
                bool isCenter = Math.Abs(axis.X) < 1e-9;
                double xLineFig = axis.X;
                string lineLayer = isCenter ? HyRoadLayers.CrossSectionCenterlineLayer : HyRoadLayers.PlanRedLineLayer;
                var line = new Line(
                    new Point3d(origin.X + xLineFig * s, origin.Y + bottomY * s, 0),
                    new Point3d(origin.X + xLineFig * s, origin.Y + lineTopY * s, 0))
                {
                    Layer = lineLayer,
                    ColorIndex = 256,
                };
                if (isCenter)
                    TrySetLinetype(tr, db, line, "HIDDEN");
                ms.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                TagEntity(tr, db, line, templateId);
                added++;

                double xOffM = GetAxisLabelXOffsetMeters(axis);
                double xTextFig = axis.X + xOffM;
                added += AddText(
                    tr, ms, db, templateId,
                    new Point3d(origin.X + xTextFig * s, origin.Y + yTextFig * s, 0),
                    axis.Label,
                    textLayer,
                    textH,
                    rotationDeg: 90,
                    alignCenter: true,
                    textStyleId: textStyleId);
            }

            return added;
        }

        private static IEnumerable<PlanStripSpan> EnumeratePlanStripSpans(CrossSectionLayout layout)
        {
            double cursor = -layout.TotalWidth / 2.0;
            for (int i = layout.LeftBands.Count - 1; i >= 0; i--)
            {
                var band = layout.LeftBands[i];
                double end = cursor + band.Width;
                yield return new PlanStripSpan(cursor, end, band.Kind, band);
                cursor = end;
            }

            if (layout.CenterMedianWidth > 1e-9)
            {
                double end = cursor + layout.CenterMedianWidth;
                yield return new PlanStripSpan(cursor, end, TemplateComponentKind.MedianStrip, null);
                cursor = end;
            }

            for (int i = 0; i < layout.RightBands.Count; i++)
            {
                var band = layout.RightBands[i];
                double end = cursor + band.Width;
                yield return new PlanStripSpan(cursor, end, band.Kind, band);
                cursor = end;
            }
        }

        private readonly struct PlanStripSpan
        {
            public double StartX { get; }
            public double EndX { get; }
            public TemplateComponentKind Kind { get; }
            public CrossSectionBand? RoadBand { get; }

            public PlanStripSpan(double startX, double endX, TemplateComponentKind kind, CrossSectionBand? roadBand)
            {
                StartX = startX;
                EndX = endX;
                Kind = kind;
                RoadBand = roadBand;
            }
        }

        private int DrawDimensionSegment(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point3d a,
            Point3d b,
            CrossSectionAnnotationStyle annotationStyle,
            double fallbackTextHeight,
            double dimLineOffset)
        {
            var dimLinePoint = new Point3d(
                (a.X + b.X) * 0.5,
                a.Y + dimLineOffset,
                0);
            // 不写入「文字替代」：第四参用空，由两定义点得测量值，再经标注样式（如线性比例/测量单位）出字；特性中「文字替代」保持空
            var dim = new AlignedDimension(a, b, dimLinePoint, string.Empty, ObjectId.Null)
            {
                Layer = annotationStyle?.DimensionLayerName ?? HyRoadLayers.CrossSectionDimensionLayer,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 3),
            };
            if (annotationStyle != null && annotationStyle.ApplyCurrentDocumentDimStyle)
            {
                dim.DimensionStyle = db.Dimstyle;
            }
            ms.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            TagEntity(tr, db, dim, templateId);
            return 1;
        }

        /// <summary>
        /// 横坡标注：文字居中位于 slope 位置下移 0.2m 处；在文字正下方再绘一个水平箭头，
        /// 箭头方向由 <see cref="FigureSlopeLabel.DirectionSign"/> 给出（+1 向右 / -1 向左）。
        /// </summary>
        private int DrawCrossSectionSlopeTextAndHorizArrow(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            FigureSlopeLabel slope,
            Func<double, double, Point3d> map,
            double s,
            double textH,
            string layer,
            ObjectId textStyleId)
        {
            int n = 0;
            // textH 是模型米；同一"图面米"尺度需除以 s。文字图面高约 textH/s。
            double textHFig = textH / s;

            // 原文字底 y（图面米）= slope.Y + 0.15；下移 0.2m → 新底 y = slope.Y - 0.05
            // 改用居中对齐以方便与下方箭头同心：中心 y（图面）= 底 y + 0.5 × 图面字高
            double textCenterYFig = -0.05 + 0.5 * textHFig;

            n += AddText(
                tr, ms, db, templateId,
                map(slope.PositionX, slope.PositionY + textCenterYFig),
                slope.Text, layer, textH, 0, alignCenter: true,
                textStyleId: textStyleId, useWhiteText: true);

            // 水平箭头：宽度 ≈ 4×textH（图面 m），方向由 DirectionSign 决定
            int dir = slope.DirectionSign;
            if (dir == 0) dir = 1; // 无坡向信息时兜底向右
            double arrowHalfLenFig = 2.0 * textHFig;
            double headLenFig = 0.8 * textHFig;
            // 箭尖横向总宽 = 0.35×字高（原 0.7× 的一半）
            double headHalfWModel = 0.175 * textH;

            // 箭头中心 y（图面）= 文字底再下移 0.4 × 图面字高
            double arrowCenterYFig = (-0.05) - 0.4 * textHFig;

            var pTailHoriz = map(slope.PositionX - dir * arrowHalfLenFig, slope.PositionY + arrowCenterYFig);
            var pHeadBase = map(slope.PositionX + dir * (arrowHalfLenFig - headLenFig), slope.PositionY + arrowCenterYFig);
            var pTipHoriz = map(slope.PositionX + dir * arrowHalfLenFig, slope.PositionY + arrowCenterYFig);

            // 尾线：从尾端到箭头根部（不画到尖端，避免与三角形叠加产生"双线"）
            var shaft = new Line(pTailHoriz, pHeadBase) { Layer = layer, ColorIndex = 256 };
            SetEntityAciWhite(shaft);
            ms.AppendEntity(shaft);
            tr.AddNewlyCreatedDBObject(shaft, true);
            TagEntity(tr, db, shaft, templateId);
            n++;

            // 三角形箭头：以 pHeadBase 为底边中点，pTipHoriz 为尖端
            n += AddSlopeArrowHead(tr, ms, db, templateId, pTipHoriz, pHeadBase, layer, headHalfWModel);
            return n;
        }

        /// <summary>横坡箭头尖：<paramref name="head"/> 为尖端，<paramref name="tail"/> 为底边中点；<paramref name="wingMeters"/> 为底边半幅（模型米）。2D <see cref="Solid"/> 实体填充、ACI7 白。</summary>
        private int AddSlopeArrowHead(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point3d head,
            Point3d tail,
            string layer,
            double wingMeters)
        {
            var ddx = tail.X - head.X;
            var ddy = tail.Y - head.Y;
            var len = Math.Sqrt(ddx * ddx + ddy * ddy);
            if (len < 1e-12) return 0;
            var nx = -ddy / len * wingMeters;
            var ny = ddx / len * wingMeters;
            double z = head.Z;
            var pa = new Point3d(tail.X + nx, tail.Y + ny, z);
            var pb = new Point3d(tail.X - nx, tail.Y - ny, z);
            var h = new Point3d(head.X, head.Y, z);
            // Solid 为四边形的三角退化：p3 与 p2 同点 → 画实心三角
            var fill = new Solid(h, pa, pb, pb)
            {
                Layer = layer,
                ColorIndex = 256,
            };
            SetEntityAciWhite(fill);
            ms.AppendEntity(fill);
            tr.AddNewlyCreatedDBObject(fill, true);
            TagEntity(tr, db, fill, templateId);
            return 1;
        }

        private int AddText(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point3d position,
            string text,
            string layer,
            double height,
            double rotationDeg = 0,
            bool alignCenter = false,
            ObjectId textStyleId = default(ObjectId),
            bool useWhiteText = true)
        {
            var dbt = new DBText
            {
                Position = position,
                Height = height,
                TextString = text ?? string.Empty,
                Layer = layer,
                ColorIndex = 256,
                Rotation = rotationDeg * Math.PI / 180.0,
            };
            if (useWhiteText)
                SetEntityAciWhite(dbt);
            if (!textStyleId.IsNull)
            {
                dbt.TextStyleId = textStyleId;
            }
            if (alignCenter)
            {
                dbt.HorizontalMode = TextHorizontalMode.TextCenter;
                dbt.VerticalMode = TextVerticalMode.TextVerticalMid;
                dbt.AlignmentPoint = position;
            }
            ms.AppendEntity(dbt);
            tr.AddNewlyCreatedDBObject(dbt, true);
            TagEntity(tr, db, dbt, templateId);
            return 1;
        }

        private int DrawOrientation(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point2d origin,
            FigureOrientation o,
            double s,
            CrossSectionAnnotationStyle annotationStyle,
            double? orientationYFigureMeters = null,
            bool useRcsTopStripText = false)
        {
            int added = 0;
            double fallbackH = Math.Max(0.3, 0.5 * s);
            ObjectId textStyleId = ObjectId.Null;
            double h = fallbackH;
            if (annotationStyle != null)
            {
                if (useRcsTopStripText)
                {
                    var fb = TryGetTextStyleId(tr, db, annotationStyle.TextStyleName);
                    ResolveRcsTopStripStyle(tr, db, annotationStyle, fb, fallbackH, out textStyleId, out h);
                }
                else
                {
                    textStyleId = TryGetTextStyleId(tr, db, annotationStyle.TextStyleName);
                    if (annotationStyle.TextHeightModel > 1e-6)
                    {
                        h = annotationStyle.TextHeightModel;
                    }
                }
            }
            string textLayer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;

            double yFig = orientationYFigureMeters ?? o.Y;
            double y = yFig * s;
            double leftX = o.LeftX * s;
            double rightX = o.RightX * s;

            // 左箭头
            var left = new Line(
                new Point3d(origin.X + rightX, origin.Y + y, 0),
                new Point3d(origin.X + leftX, origin.Y + y, 0))
            {
                Layer = HyRoadLayers.CrossSectionOrientationLayer,
                ColorIndex = 256,
            };
            SetEntityAciWhite(left);
            ms.AppendEntity(left);
            tr.AddNewlyCreatedDBObject(left, true);
            TagEntity(tr, db, left, templateId);
            added++;

            // 左箭头头（简单三角 Polyline）
            added += AddArrow(tr, ms, db, templateId,
                new Point3d(origin.X + leftX, origin.Y + y, 0),
                new Point3d(origin.X + leftX + 0.8 * s, origin.Y + y, 0));

            // 右箭头头（与左向共用一条水平向线，不再重复加 Line）
            added += AddArrow(tr, ms, db, templateId,
                new Point3d(origin.X + rightX, origin.Y + y, 0),
                new Point3d(origin.X + rightX - 0.8 * s, origin.Y + y, 0));

            // 左右文字
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + leftX - 1.4 * s, origin.Y + y, 0),
                o.LeftLabel, textLayer, h, textStyleId: textStyleId, useWhiteText: true);
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + rightX + 0.4 * s, origin.Y + y, 0),
                o.RightLabel, textLayer, h, textStyleId: textStyleId, useWhiteText: true);

            return added;
        }

        /// <summary>从 <paramref name="head"/> 画一个指向它的小三角形（<paramref name="tail"/> → <paramref name="head"/>）。</summary>
        private int AddArrow(Transaction tr, BlockTableRecord ms, Database db, Guid templateId, Point3d head, Point3d tail)
        {
            var pl = new Polyline(3);
            var dir = tail - head;
            var perp = new Vector3d(-dir.Y * 0.1, dir.X * 0.1, 0);
            var pa = new Point2d(tail.X + perp.X, tail.Y + perp.Y);
            var pb = new Point2d(tail.X - perp.X, tail.Y - perp.Y);
            pl.AddVertexAt(0, new Point2d(head.X, head.Y), 0, 0, 0);
            pl.AddVertexAt(1, pa, 0, 0, 0);
            pl.AddVertexAt(2, pb, 0, 0, 0);
            pl.Closed = true;
            pl.Layer = HyRoadLayers.CrossSectionOrientationLayer;
            pl.ColorIndex = 256;
            SetEntityAciWhite(pl);
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            TagEntity(tr, db, pl, templateId);
            return 1;
        }

        private static string PickPanelLayer(TemplateComponentKind kind)
        {
            switch (kind)
            {
                case TemplateComponentKind.Pavement:
                case TemplateComponentKind.NonMotorized:
                    return HyRoadLayers.CrossSectionPavementLayer;
                case TemplateComponentKind.Sidewalk:
                case TemplateComponentKind.Shoulder:
                    return HyRoadLayers.CrossSectionSidewalkLayer;
                case TemplateComponentKind.Kerb:
                    // 路牙独立图层（v2 新增）：与人行道分离，便于按图层批量改色 / 冻结。
                    return HyRoadLayers.CrossSectionKerbLayer;
                case TemplateComponentKind.GreenStrip:
                case TemplateComponentKind.MedianStrip:
                    return HyRoadLayers.CrossSectionGreenLayer;
                default:
                    return HyRoadLayers.CrossSectionOutlineLayer;
            }
        }

        private static void TagEntity(Transaction tr, Database db, Entity ent, Guid templateId)
        {
            HyRoadXdata.Write(tr, db, ent, templateId, DrawingKind, SchemaVersion.Current);
        }

        private static void EnsureLayersVisible(Transaction tr, Database db)
        {
            // 放到 LayerManager 更干净，但此服务作为独立模块，最小副作用：不改变现有图层状态，只确保存在。
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var (name, color) in HyRoadLayers.GetAllResolved())
            {
                if (lt.Has(name)) continue;
                if (!lt.IsWriteEnabled) lt.UpgradeOpen();
                using (var ltr = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, color) })
                {
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
            }
        }

        private static void EnsureStyleLayersVisible(
            Transaction tr,
            Database db,
            CrossSectionAnnotationStyle annotationStyle,
            DrawingSheetTitleSpec titleSpec)
        {
            EnsureLayerExists(tr, db, annotationStyle?.TextLayerName, 7);
            EnsureLayerExists(tr, db, annotationStyle?.DimensionLayerName, 3);
            EnsureLayerExists(tr, db, annotationStyle?.TitleLayerName, 7);
            EnsureLayerExists(tr, db, titleSpec?.TitleTextLayerName, 7);
            EnsureLayerExists(tr, db, titleSpec?.TitleDecorationLayerName, 7);
        }

        private static void EnsureLayerExists(Transaction tr, Database db, string layerName, short aciColor)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return;
            if (!lt.IsWriteEnabled) lt.UpgradeOpen();
            using (var ltr = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, aciColor),
            })
            {
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        /// <summary>
        /// 尝试给 <paramref name="ent"/> 设置线型（若数据库中有该线型则设置，否则静默忽略）。
        /// 避免"HIDDEN 线型不存在"触发异常打断出图。
        /// </summary>
        private static void TrySetLinetype(Transaction tr, Database db, Entity ent, string linetypeName)
        {
            if (string.IsNullOrWhiteSpace(linetypeName)) return;
            try
            {
                var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(linetypeName))
                {
                    ent.LinetypeId = lt[linetypeName];
                }
            }
            catch
            {
                // 静默：线型加载失败不阻断出图
            }
        }
    }
}
