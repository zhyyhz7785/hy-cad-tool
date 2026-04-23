using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
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
    ///   <item>横坡（DBText + 竖向指坡箭头，箭头端与路顶面留 2mm 纸面间距）；中分带不生成横坡；顶部条带名（DBText）。</item>
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
            => Draw(transaction, database, figure, template, origin, modelUnitPerMeter, CrossSectionDrawMode.WithStructureThickness, null, null, null);

        /// <summary>
        /// M7.4 重载：指定绘图模式。
        /// <para><see cref="CrossSectionDrawMode.SingleLine"/> 时跳过板块闭合填充 / 尺寸链 / 标注 / 标题，只保留：
        /// 顶面轮廓 Polyline + 中心虚线 + 方位箭头，专供平面图单线投影使用。</para>
        /// </summary>
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
            DrawingSheetTitleSpec sheetTitleSpec = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (figure == null) throw new ArgumentNullException(nameof(figure));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (modelUnitPerMeter <= 0) throw new ArgumentOutOfRangeException(nameof(modelUnitPerMeter));

            HyRoadXdata.EnsureRegApp(transaction, database);
            EnsureLayersVisible(transaction, database);

            var titleSpec = sheetTitleSpec ?? DrawingSheetTitleSpec.RoadCrossSectionDefault;

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
                var centerS = new Line(
                    new Point3d(origin.X, origin.Y - 0.5 * s, 0),
                    new Point3d(origin.X, origin.Y + (ymaxS + 0.6) * s, 0))
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

            // 路面/绿化/中分带等"路面板块"用"顶 + 底"闭合（构成立柱）；
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

            // ---------------- 3. 中心线（虚线） ----------------
            // 中心 X=0 画一条虚线段，从底(-0.5m)到顶(max(y)+0.6m)
            double ymax = 0;
            foreach (var v in figure.Vertices) if (v.Y > ymax) ymax = v.Y;
            var center = new Line(
                new Point3d(origin.X, origin.Y - 0.5 * s, 0),
                new Point3d(origin.X, origin.Y + (ymax + 0.6) * s, 0))
            {
                Layer = HyRoadLayers.CrossSectionCenterlineLayer,
                ColorIndex = 256,
            };
            TrySetLinetype(transaction, database, center, "HIDDEN");
            ms.AppendEntity(center);
            transaction.AddNewlyCreatedDBObject(center, true);
            TagEntity(transaction, database, center, template.Id);
            added++;

            // ---------------- 4. 线型字高（顶排总宽尺寸已取消，见下仅一道总宽） ----------------
            double txtH = Math.Max(0.15, 0.3 * s);
            if (annotationStyle != null && annotationStyle.TextHeightModel > 1e-6)
            {
                txtH = annotationStyle.TextHeightModel;
            }

            // ---------------- 5. 道路下方第二道尺寸线仅标红线总宽（无顶排、无分条与左/中/右分链） ----------------
            double dimTotalY = -2.0 * s;
            foreach (var seg in figure.DimensionSegments)
            {
                if (seg.Tier != 0) continue;
                var a = new Point3d(origin.X + seg.StartX * s, origin.Y + dimTotalY, 0);
                var b = new Point3d(origin.X + seg.EndX * s, origin.Y + dimTotalY, 0);
                added += DrawDimensionSegment(transaction, ms, database, template.Id, a, b, false, annotationStyle, txtH);
            }

            // ---------------- 6. 横坡：文字 + 指向下方路面（箭头端至路顶面 2mm 纸面，同 ActualTextHeight 换算） ----------------
            var slopeLayer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            var scaleC = ActiveScaleContextProvider.Current;
            const double paperMmToRoad = 2.0;
            double roadGapModel = paperMmToRoad * scaleC.UnitFactor * scaleC.MainScale;
            double roadGapFig = roadGapModel / s; // 图面米，顶面 y
            foreach (var slope in figure.SlopeLabels)
            {
                added += DrawCrossSectionSlopeTextAndDownArrow(
                    transaction, ms, database, template.Id, slope, Map, s, txtH, slopeLayer,
                    annotationStyle?.TextStyleId ?? ObjectId.Null, roadGapFig);
            }

            // ---------------- 7. 标高：MLeader 避让盒
            // 同 x 的多个标高 → 并成一条 MText（\P 叠行）+ 锚 y 用竖向中点（图2）
            // ----------------
            var mleaderTextBoxes = new List<MLeaderTextAabb>();
            foreach (var g in GroupHeightLabelsByColocatedX(figure.HeightLabels))
            {
                added += AddHeightMLeader(
                    transaction, ms, database, template.Id, g, Map, s, txtH, annotationStyle, mleaderTextBoxes);
            }

            // ---------------- 8. 顶栏条带名（绿化带等）不绘于道路上方，略（预览仍可在 WPF 显示） ----------------

            // ---------------- 9. 方位箭头 ----------------
            if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel) ||
                !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
            {
                added += DrawOrientation(transaction, ms, database, template.Id,
                    origin, figure.Orientation, s, annotationStyle);
            }

            // ---------------- 10. 图题（规格化：双下划线 + 比例 + 可选左十字/方格） ----------------
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
                if (mainStyleId.IsNull) mainStyleId = annotationStyle?.TextStyleId ?? ObjectId.Null;
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
                    titleLayerOverride);
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
                anchor, endPoint, content, annotationStyle?.MLeaderStyleId ?? ObjectId.Null);
            ml.Layer = annotationStyle?.TextLayerName ?? HyRoadLayers.CrossSectionAnnotationLayer;
            ml.ColorIndex = 256;
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

        private int DrawDimensionSegment(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point3d a,
            Point3d b,
            bool above,
            CrossSectionAnnotationStyle annotationStyle,
            double fallbackTextHeight)
        {
            var dimLineOffset = Math.Max(fallbackTextHeight * 1.6, 0.24);
            var dimLinePoint = new Point3d(
                (a.X + b.X) * 0.5,
                a.Y + (above ? dimLineOffset : -dimLineOffset),
                0);
            // 不写入「文字替代」：第四参用空，由两定义点得测量值，再经标注样式（如线性比例/测量单位）出字；特性中「文字替代」保持空
            var dim = new AlignedDimension(a, b, dimLinePoint, string.Empty, ObjectId.Null)
            {
                Layer = annotationStyle?.DimensionLayerName ?? HyRoadLayers.CrossSectionDimensionLayer,
                ColorIndex = 256,
            };
            if (annotationStyle != null && !annotationStyle.DimensionStyleId.IsNull)
            {
                dim.DimensionStyle = annotationStyle.DimensionStyleId;
            }
            ms.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            TagEntity(tr, db, dim, templateId);
            return 1;
        }

        /// <param name="roadGapFigure">路顶面至箭头端（尖端）的竖向图面间距（m），已含 2mm 纸面→模型换算 / s。</param>
        private int DrawCrossSectionSlopeTextAndDownArrow(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            FigureSlopeLabel slope,
            Func<double, double, Point3d> map,
            double s,
            double textH,
            string layer,
            ObjectId textStyleId,
            double roadGapFigure)
        {
            int n = 0;
            n += AddText(tr, ms, db, templateId, map(slope.PositionX, slope.PositionY + 0.15), slope.Text,
                layer, textH, 0, false, textStyleId);
            // 竖向自「略低于文字行」向下面路顶，尖端距路 2mm（纸面）
            double yTip = slope.PositionY + roadGapFigure;
            double yStart = slope.PositionY + 0.10;
            if (yTip > yStart - 1e-6)
            {
                yStart = yTip + 0.12;
            }
            var pHigh = map(slope.PositionX, yStart);
            var pTip = map(slope.PositionX, yTip);
            if (pHigh.Y - pTip.Y > 1e-9 * s)
            {
                var shaft = new Line(pHigh, pTip) { Layer = layer, ColorIndex = 256 };
                ms.AppendEntity(shaft);
                tr.AddNewlyCreatedDBObject(shaft, true);
                TagEntity(tr, db, shaft, templateId);
                n++;
            }
            var pTail = map(slope.PositionX, yTip + Math.Max(0.08, 0.2 * s));
            n += AddSlopeArrowHead(tr, ms, db, templateId, pTip, pTail, layer, Math.Max(0.08, 0.12 * s));
            return n;
        }

        /// <summary>横坡用竖向小箭头，尖端在 <paramref name="head"/>（路侧），<paramref name="tail"/> 在上方沿坡向。</summary>
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
            var pl = new Polyline(3);
            var ddx = tail.X - head.X;
            var ddy = tail.Y - head.Y;
            var len = Math.Sqrt(ddx * ddx + ddy * ddy);
            if (len < 1e-12) return 0;
            var nx = -ddy / len * wingMeters;
            var ny = ddx / len * wingMeters;
            var pa = new Point2d(tail.X + nx, tail.Y + ny);
            var pb = new Point2d(tail.X - nx, tail.Y - ny);
            pl.AddVertexAt(0, new Point2d(head.X, head.Y), 0, 0, 0);
            pl.AddVertexAt(1, pa, 0, 0, 0);
            pl.AddVertexAt(2, pb, 0, 0, 0);
            pl.Closed = true;
            pl.Layer = layer;
            pl.ColorIndex = 256;
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            TagEntity(tr, db, pl, templateId);
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
            ObjectId textStyleId = default(ObjectId))
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
            CrossSectionAnnotationStyle annotationStyle)
        {
            int added = 0;
            ObjectId textStyleId = ObjectId.Null;
            double h = Math.Max(0.3, 0.5 * s);
            if (annotationStyle != null)
            {
                textStyleId = annotationStyle.TextStyleId;
                if (annotationStyle.TextHeightModel > 1e-6)
                {
                    h = annotationStyle.TextHeightModel;
                }
            }

            double y = o.Y * s;
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
            ms.AppendEntity(left);
            tr.AddNewlyCreatedDBObject(left, true);
            TagEntity(tr, db, left, templateId);
            added++;

            // 左箭头头（简单三角 Polyline）
            added += AddArrow(tr, ms, db, templateId,
                new Point3d(origin.X + leftX, origin.Y + y, 0),
                new Point3d(origin.X + leftX + 0.8 * s, origin.Y + y, 0));

            // 右箭头
            var right = new Line(
                new Point3d(origin.X + leftX, origin.Y + y, 0),
                new Point3d(origin.X + rightX, origin.Y + y, 0))
            {
                Layer = HyRoadLayers.CrossSectionOrientationLayer,
                ColorIndex = 256,
            };
            // 同一条线与 left 方向相反，忽略重复不直接添加 —— 上面已绘制
            // 添加右箭头头
            added += AddArrow(tr, ms, db, templateId,
                new Point3d(origin.X + rightX, origin.Y + y, 0),
                new Point3d(origin.X + rightX - 0.8 * s, origin.Y + y, 0));

            // 左右文字
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + leftX - 1.4 * s, origin.Y + y, 0),
                o.LeftLabel, HyRoadLayers.CrossSectionOrientationLayer, h, textStyleId: textStyleId);
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + rightX + 0.4 * s, origin.Y + y, 0),
                o.RightLabel, HyRoadLayers.CrossSectionOrientationLayer, h, textStyleId: textStyleId);

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
            foreach (var (name, color) in HyRoadLayers.GetAll())
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
