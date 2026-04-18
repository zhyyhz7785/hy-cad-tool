using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// M3 标准横断面图出图服务。
    ///
    /// 职责：把 <see cref="CrossSectionFigure"/> 绘制到 AutoCAD ModelSpace：
    /// <list type="bullet">
    ///   <item>主轮廓 LWPolyline（闭合）+ 顶面 polyline。</item>
    ///   <item>按 <see cref="FigurePanel.Kind"/> 分色的分段 Hatch（可选；v1 仅 LWPoly 切片）。</item>
    ///   <item>中心虚线 Line。</item>
    ///   <item>底部尺寸链（Tier=0/1）+ 顶部尺寸链（Tier=2），用 <see cref="Line"/> + <see cref="DBText"/> 手绘，不依赖 DIMSTYLE。</item>
    ///   <item>横坡标注、高差标注、顶部条带名（DBText）。</item>
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
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (figure == null) throw new ArgumentNullException(nameof(figure));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (modelUnitPerMeter <= 0) throw new ArgumentOutOfRangeException(nameof(modelUnitPerMeter));

            HyRoadXdata.EnsureRegApp(transaction, database);
            EnsureLayersVisible(transaction, database);

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
            foreach (var panel in figure.Panels)
            {
                int i0 = Math.Max(0, panel.StartVertexIndex);
                int i1 = Math.Min(figure.Vertices.Count - 1, panel.EndVertexIndex);
                if (i1 - i0 < 1) continue;

                // panel 闭合 polyline：顶 + 底
                int count = (i1 - i0 + 1) + 2;
                var pl = new Polyline(count);
                int idx = 0;
                for (int i = i0; i <= i1; i++)
                {
                    var v = figure.Vertices[i];
                    pl.AddVertexAt(idx++, new Point2d(origin.X + v.X * s, origin.Y + v.Y * s), 0, 0, 0);
                }
                // 底边两点
                pl.AddVertexAt(idx++, new Point2d(origin.X + figure.Vertices[i1].X * s, origin.Y), 0, 0, 0);
                pl.AddVertexAt(idx, new Point2d(origin.X + figure.Vertices[i0].X * s, origin.Y), 0, 0, 0);
                pl.Closed = true;
                pl.Layer = PickPanelLayer(panel.Kind);
                pl.ColorIndex = 256;

                ms.AppendEntity(pl);
                transaction.AddNewlyCreatedDBObject(pl, true);
                TagEntity(transaction, database, pl, template.Id);
                added++;
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

            // ---------------- 4. 顶部 Tier=2 总宽尺寸线（单横线） ----------------
            double txtH = Math.Max(0.15, 0.3 * s); // 模型空间 0.3m 高（1:100 图上 3mm）
            double dimTopY = (ymax + 1.2) * s;
            foreach (var seg in figure.DimensionSegments)
            {
                if (seg.Tier != 2) continue;
                added += DrawDimSegment(transaction, ms, database, template.Id,
                    new Point3d(origin.X + seg.StartX * s, origin.Y + dimTopY, 0),
                    new Point3d(origin.X + seg.EndX * s, origin.Y + dimTopY, 0),
                    seg.Text, txtH, above: true);
            }

            // ---------------- 5. 底部 Tier=1 分段 + Tier=0 总长 ----------------
            double dimT1Y = -1.0 * s;  // 靠近地面
            double dimT0Y = -2.0 * s;  // 更下方
            foreach (var seg in figure.DimensionSegments)
            {
                Point3d a, b;
                double y;
                if (seg.Tier == 1) y = dimT1Y;
                else if (seg.Tier == 0) y = dimT0Y;
                else continue;
                a = new Point3d(origin.X + seg.StartX * s, origin.Y + y, 0);
                b = new Point3d(origin.X + seg.EndX * s, origin.Y + y, 0);
                added += DrawDimSegment(transaction, ms, database, template.Id, a, b, seg.Text, txtH, above: false);
            }

            // ---------------- 6. 横坡标注 ----------------
            foreach (var slope in figure.SlopeLabels)
            {
                var p = Map(slope.PositionX, slope.PositionY + 0.15);
                added += AddText(transaction, ms, database, template.Id, p, slope.Text,
                    HyRoadLayers.CrossSectionAnnotationLayer, txtH);
            }

            // ---------------- 7. 高差标注 ----------------
            foreach (var h in figure.HeightLabels)
            {
                var p = Map(h.PositionX + 0.15, h.PositionY);
                added += AddText(transaction, ms, database, template.Id, p, h.Text,
                    HyRoadLayers.CrossSectionAnnotationLayer, txtH);
            }

            // ---------------- 8. 顶部条带名（竖写；v1 退化为水平） ----------------
            foreach (var top in figure.TopLabels)
            {
                var p = Map(top.CenterX - 0.3, top.CenterY);
                added += AddText(transaction, ms, database, template.Id, p, top.Text,
                    HyRoadLayers.CrossSectionAnnotationLayer, txtH,
                    rotationDeg: top.Vertical ? 90 : 0);
            }

            // ---------------- 9. 方位箭头 ----------------
            if (!string.IsNullOrWhiteSpace(figure.Orientation.LeftLabel) ||
                !string.IsNullOrWhiteSpace(figure.Orientation.RightLabel))
            {
                added += DrawOrientation(transaction, ms, database, template.Id,
                    origin, figure.Orientation, s);
            }

            // ---------------- 10. 标题 ----------------
            if (!string.IsNullOrWhiteSpace(figure.Title.Text))
            {
                var p = Map(figure.Title.CenterX, figure.Title.Y);
                double titleH = Math.Max(0.3, 0.6 * s);
                added += AddText(transaction, ms, database, template.Id, p, figure.Title.Text,
                    HyRoadLayers.CrossSectionTitleLayer, titleH, alignCenter: true);
            }

            return added;
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

        private int DrawDimSegment(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateId,
            Point3d a,
            Point3d b,
            string text,
            double txtH,
            bool above)
        {
            int added = 0;
            // 水平尺寸线
            var line = new Line(a, b)
            {
                Layer = HyRoadLayers.CrossSectionDimensionLayer,
                ColorIndex = 256,
            };
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            TagEntity(tr, db, line, templateId);
            added++;

            // ticks
            double th = txtH * 0.8;
            foreach (var p in new[] { a, b })
            {
                var t = new Line(
                    new Point3d(p.X, p.Y - th, 0),
                    new Point3d(p.X, p.Y + th, 0))
                {
                    Layer = HyRoadLayers.CrossSectionDimensionLayer,
                    ColorIndex = 256,
                };
                ms.AppendEntity(t);
                tr.AddNewlyCreatedDBObject(t, true);
                TagEntity(tr, db, t, templateId);
                added++;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var mid = new Point3d((a.X + b.X) / 2, a.Y + (above ? txtH * 0.6 : -txtH * 1.5), 0);
                added += AddText(tr, ms, db, templateId, mid, text,
                    HyRoadLayers.CrossSectionDimensionLayer, txtH, alignCenter: true);
            }
            return added;
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
            bool alignCenter = false)
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
            double s)
        {
            int added = 0;

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
            double h = Math.Max(0.3, 0.5 * s);
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + leftX - 1.4 * s, origin.Y + y, 0),
                o.LeftLabel, HyRoadLayers.CrossSectionOrientationLayer, h);
            added += AddText(tr, ms, db, templateId,
                new Point3d(origin.X + rightX + 0.4 * s, origin.Y + y, 0),
                o.RightLabel, HyRoadLayers.CrossSectionOrientationLayer, h);

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
                case TemplateComponentKind.Kerb:
                    return HyRoadLayers.CrossSectionSidewalkLayer;
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
