using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 平面线位服务。
    ///
    /// 职责：
    /// - P0：打通"命令 → 服务 → Domain → 事件总线 → JSON 持久化"闭环（<see cref="Create"/> 空白创建）。
    /// - P1：从 AutoCAD 多段线导入 / 同步 Alignment（<see cref="ImportFromPolyline"/>），写 Xdata 挂 GUID。
    /// - 后续（P1.b）：从 Domain 反向绘制 → <see cref="RedrawCenterline"/>（另见 <c>RoadGeometryBridge.CreatePolylineFrom3d</c>）。
    ///
    /// Xdata 策略：
    /// - 同一条 AutoCAD 多段线重复拾取时，若已挂 HY_ROAD/ID 则按 Updated 同步几何，否则按 Created 新建。
    /// - 若 Xdata 记录的 GUID 在 Domain 里不存在（文件被删 / 剪切等），按"Xdata 孤儿"处理：
    ///   沿用原 GUID 重建 Alignment，保证 DWG ↔ JSON 同步。
    /// </summary>
    public sealed class RoadAlignmentService
    {
        private readonly RoadDesignRegistry _registry;
        private readonly IRoadEventBus _eventBus;

        public RoadAlignmentService(RoadDesignRegistry registry, IRoadEventBus eventBus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// P0 占位：为指定文档创建一个空白平面线位（仅 Domain 层，不拾取）。
        /// </summary>
        public Alignment Create(string documentName, string displayName = null)
        {
            var design = _registry.GetOrCreate(documentName);
            var alignment = new Alignment
            {
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? $"Alignment {design.Alignments.Count + 1}"
                    : displayName
            };
            design.Alignments.Add(alignment);
            design.LastModifiedUtc = DateTime.UtcNow;

            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, RoadChangeKind.Created));
            return alignment;
        }

        /// <summary>
        /// P1：从 AutoCAD 2D 多段线导入 / 同步平面线位。
        /// </summary>
        /// <param name="documentName">DWG 文档名（用于索引 <see cref="RoadDesignRegistry"/>）。</param>
        /// <param name="transaction">调用方开启的 Transaction（本方法不自主提交，由调用方负责 <c>Commit</c>）。</param>
        /// <param name="database">活动 Database（用于注册 RegApp）。</param>
        /// <param name="polyline">已在 <paramref name="transaction"/> 作用域内打开的多段线（需 ForRead 或更高）。</param>
        /// <param name="addedNewAlignment">
        /// 本次调用是否新向 <see cref="RoadDesign.Alignments"/> 追加了一条线位（不含「更新已有」）。
        /// 供 <c>hyRoadA</c> 仅在首次登记时套用 hy-settings 默认起桩号。
        /// </param>
        /// <param name="displayName">可选显示名；为空时自动生成 <c>"Alignment N"</c>。</param>
        /// <returns>创建或更新后的 <see cref="Alignment"/>。</returns>
        public Alignment ImportFromPolyline(
            string documentName,
            Transaction transaction,
            Database database,
            Polyline polyline,
            out bool addedNewAlignment,
            string displayName = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            var design = _registry.GetOrCreate(documentName);
            var centerline = RoadGeometryBridge.ToDomain(polyline);

            var existingId = HyRoadXdata.ReadId(transaction, polyline);
            Alignment alignment;
            RoadChangeKind kind;

            if (existingId != Guid.Empty)
            {
                alignment = design.Alignments.FirstOrDefault(a => a.Id == existingId);
                if (alignment != null)
                {
                    alignment.Centerline = centerline;
                    kind = RoadChangeKind.Updated;
                    addedNewAlignment = false;
                }
                else
                {
                    // Xdata 孤儿：沿用 Xdata 的 Id 重建，避免破坏 AutoCAD ↔ JSON 的身份一致性。
                    alignment = new Alignment
                    {
                        Id = existingId,
                        Name = string.IsNullOrWhiteSpace(displayName)
                            ? $"Alignment {design.Alignments.Count + 1}"
                            : displayName,
                        Centerline = centerline
                    };
                    design.Alignments.Add(alignment);
                    kind = RoadChangeKind.Created;
                    addedNewAlignment = true;
                }
            }
            else
            {
                alignment = new Alignment
                {
                    Name = string.IsNullOrWhiteSpace(displayName)
                        ? $"Alignment {design.Alignments.Count + 1}"
                        : displayName,
                    Centerline = centerline
                };
                design.Alignments.Add(alignment);
                kind = RoadChangeKind.Created;
                addedNewAlignment = true;
            }

            ApplyPiSourceSnapshot(alignment, centerline);
            AssignAlignmentLayerIfExists(transaction, database, polyline);

            HyRoadXdata.Write(transaction, database, polyline, alignment.Id, "Alignment", SchemaVersion.Current);

            alignment.CaptureRaw();

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, kind));
            return alignment;
        }

        /// <summary>
        /// 与 <c>hyRoadAlnByPi</c> 一致：把已登记中心线放到标准平面线位图层（若该图层存在）。
        /// </summary>
        private static void AssignAlignmentLayerIfExists(Transaction transaction, Database database, Polyline polyline)
        {
            var lt = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (lt.Has(HyRoadLayers.AlignmentLayer))
                polyline.Layer = HyRoadLayers.AlignmentLayer;
        }

        /// <summary>
        /// 纯直线多段线：按顶点生成 PI 表快照；含 bulge 弧段时对开放线位反求切线交点 PI（见 <see cref="AlignmentSource.TryCreatePiTableFromBulgeCenterline"/>）。
        /// 反解失败时仅在尚无有效 PI 表时保持 <c>Source=null</c>。
        /// </summary>
        private static void ApplyPiSourceSnapshot(Alignment alignment, Polyline3D centerline)
        {
            if (!centerline.HasArcs)
            {
                var src = AlignmentSource.TryCreatePiTableFromStraightCenterline(centerline);
                if (src != null)
                    alignment.Source = src;
                return;
            }

            var fromBulge = AlignmentSource.TryCreatePiTableFromBulgeCenterline(centerline);
            if (fromBulge != null)
            {
                alignment.Source = fromBulge;
                return;
            }

            if (alignment.Source == null
                || alignment.Source.PiElements == null
                || alignment.Source.PiElements.Count < 2)
            {
                alignment.Source = null;
            }
        }

        /// <summary>
        /// P1.b：从 Domain（JSON 权威源）反向对齐 DWG 的中心线图元。
        ///
        /// Load 语义是"以 JSON 为准覆盖 DWG 几何"，所以策略是 update-in-place-or-create：
        /// - 扫描 ModelSpace，收集已挂 HY_ROAD/ID=Alignment 的 Polyline，建立 <c>Guid → Polyline</c> 字典；
        /// - 对 Domain 中每个 <see cref="Alignment"/>：
        ///     · 字典命中 ⇒ 通过 <see cref="RoadGeometryBridge.UpdateAutoCadPolyline"/> 就地替换顶点 + bulge，
        ///       保留 ObjectId / 图层 / Xdata / 用户自定义属性；
        ///     · 字典未命中 ⇒ 新建 Polyline + 写 HY_ROAD Xdata；
        /// - 用户未挂 Xdata 的普通 polyline 不受影响（道路模块只管"道路图元"）。
        ///
        /// 这个行为切换修复了 P1 初版的致命 bug：原策略"已存在就跳过"会让 Load 无法更新几何，
        /// 导致 JSON 里再改什么都拉不进 DWG —— 用户看到的永远是"第一次生成的直线"。
        /// </summary>
        /// <returns>新建与更新的 polyline 数量。</returns>
        public (int Created, int Updated) RedrawCenterlines(
            string documentName,
            Transaction transaction,
            Database database,
            string layerName = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));

            if (!_registry.TryGet(documentName, out var design)) return (0, 0);
            if (design.Alignments.Count == 0) return (0, 0);

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            // 只收集 HY_ROAD/KIND=Alignment 的 polyline；
            // 避免未来 Profile / Template 用同机制时相互误伤。
            var existing = new Dictionary<Guid, Polyline>();
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (!(ent is Polyline poly)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid == Guid.Empty) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                existing[gid] = poly;
            }

            int created = 0;
            int updated = 0;
            string effectiveLayer = string.IsNullOrWhiteSpace(layerName)
                ? HyRoadLayers.AlignmentLayer
                : layerName;
            bool layerUsable = LayerExists(transaction, database, effectiveLayer);

            foreach (var a in design.Alignments)
            {
                if (a.Centerline == null || a.Centerline.VertexCount < 2) continue;

                if (existing.TryGetValue(a.Id, out var oldPoly))
                {
                    if (!oldPoly.IsWriteEnabled) oldPoly.UpgradeOpen();
                    RoadGeometryBridge.UpdateAutoCadPolyline(oldPoly, a.Centerline);
                    updated++;
                }
                else
                {
                    var poly = RoadGeometryBridge.ToAutoCadPolyline(a.Centerline);
                    if (layerUsable) poly.Layer = effectiveLayer;
                    ms.AppendEntity(poly);
                    transaction.AddNewlyCreatedDBObject(poly, true);

                    HyRoadXdata.Write(transaction, database, poly, a.Id, "Alignment", SchemaVersion.Current);
                    created++;
                }
            }

            return (created, updated);
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }

        /// <summary>
        /// HY_ROAD 里桩号标注实体统一的 KIND 值。
        /// 清理 / 识别逻辑基于它 + <c>ID = AlignmentId</c>，实现"针对某条 Alignment 的重绘幂等"。
        /// </summary>
        private const string StationLabelKind = "StationLabel";

        /// <summary>
        /// 沿指定 Alignment 生成一组"桩号标注"实体（短刻度线 + 主桩文字）。
        ///
        /// 幂等策略（与 <see cref="RedrawCenterlines"/> 一致）：
        /// - 本方法内部先调 <see cref="ClearStationLabels"/> 删除同一 AlignmentId 的历史桩号实体；
        /// - 再按 <paramref name="options"/> 的主 / 副间隔批量生成。
        /// 这样用户可以反复跑 <c>hyRoadAlnStation</c> 更新标注，而不会累积重复图元。
        ///
        /// 落图规则：
        /// - 刻度线：<see cref="Line"/>，跨中心线两侧（法向对称），挂到 <see cref="HyRoadLayers.StationLayer"/> 图层；
        /// - 主桩文字：<see cref="DBText"/>，默认沿中心线切向旋转、放在 <see cref="StationTextSide.Left"/> 侧；
        /// - 所有新生实体挂 HY_ROAD XData：<c>KIND=<see cref="StationLabelKind"/></c>，<c>ID=alignmentId</c>。
        /// </summary>
        /// <param name="documentName">当前 DWG 名（用于从 Registry 取 design）。</param>
        /// <param name="transaction">调用方开启的 Transaction（本方法不自主 Commit）。</param>
        /// <param name="database">活动 Database。</param>
        /// <param name="alignmentId">要标注的 Alignment GUID。</param>
        /// <param name="options">标注参数；<c>null</c> 时使用 <see cref="RoadStationLabelOptions.Default"/>。</param>
        /// <returns>(mainCount, subCount) 本次生成的主桩数量与副桩数量。</returns>
        public (int MainCount, int SubCount) DrawStationLabels(
            string documentName,
            Transaction transaction,
            Database database,
            Guid alignmentId,
            RoadStationLabelOptions options = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (alignmentId == Guid.Empty) throw new ArgumentException("alignmentId cannot be empty", nameof(alignmentId));

            options = options ?? RoadStationLabelOptions.Default;
            options.Validate();

            if (!_registry.TryGet(documentName, out var design)) return (0, 0);
            var alignment = design.Alignments.FirstOrDefault(a => a.Id == alignmentId);
            if (alignment == null) return (0, 0);
            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2) return (0, 0);

            // 先清旧，保证幂等；不依赖调用方的 Clear。
            ClearStationLabels(transaction, database, alignmentId);

            string layerName = HyRoadLayers.StationLayer;
            bool useLayer = LayerExists(transaction, database, layerName);

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            // 主/副桩：按"显示桩号整数倍"对齐（方程之间分段对齐），不是 raw 距离整数倍。
            // 这样 K0+000 / K0+020 等整桩号永远是主桩，跨方程后也能重新从整 display 开始。
            var mainTicks = PlanDisplayAlignedTicks(alignment, options.MainInterval);
            int mainCount = 0;
            var mainKeySet = new HashSet<long>(mainTicks.Count);
            foreach (var t in mainTicks)
            {
                AppendStationTick(transaction, ms, database, alignment, t.Sample, t.Display, options,
                    isMain: true, layerName: useLayer ? layerName : null);
                mainCount++;
                mainKeySet.Add(DisplayKey(t.Display));
            }

            int subCount = 0;
            if (options.SubInterval > 0 && options.SubInterval < options.MainInterval)
            {
                foreach (var t in PlanDisplayAlignedTicks(alignment, options.SubInterval))
                {
                    if (mainKeySet.Contains(DisplayKey(t.Display))) continue;

                    AppendStationTick(transaction, ms, database, alignment, t.Sample, t.Display, options,
                        isMain: false, layerName: useLayer ? layerName : null);
                    subCount++;
                }
            }

            return (mainCount, subCount);
        }

        /// <summary>
        /// 按"显示桩号整数倍"规划 tick：遍历 [0..totalRaw] 上的每段方程区间，
        /// 在每段内找首个 ≥ displayLo 的 <paramref name="interval"/> 倍数，再按 interval 步进到 displayHi。
        /// 对应的 raw = rawLo + (display − displayLo)，保证每个 tick 的 <b>显示桩号</b> 恰好整除 interval。
        /// </summary>
        private static List<(StationSample Sample, double Display)> PlanDisplayAlignedTicks(
            global::HyCADTool.Domain.Models.Road.Alignment alignment, double interval)
        {
            var result = new List<(StationSample, double)>();
            var poly = alignment.Centerline;
            if (poly == null || poly.VertexCount < 2 || interval <= 0) return result;

            double totalRaw = poly.GetPlanarLength();
            if (totalRaw <= 1e-9) return result;

            double startStation = alignment.StartStation;
            var eqs = alignment.StationEquations ?? new List<global::HyCADTool.Domain.ValueObjects.Road.StationEquation>();

            // 构造分段区间：(rawLo, rawHi, displayAtLo)
            var ranges = new List<(double RawLo, double RawHi, double DispLo)>();
            double prevRaw = 0;
            double prevDisp = startStation;
            foreach (var eq in StationConverter.CloneSorted(eqs))
            {
                if (eq.BeforeRaw > prevRaw + 1e-9 && eq.BeforeRaw <= totalRaw + 1e-9)
                {
                    ranges.Add((prevRaw, Math.Min(eq.BeforeRaw, totalRaw), prevDisp));
                    prevRaw = eq.BeforeRaw;
                    prevDisp = eq.AheadStation;
                }
            }
            if (prevRaw < totalRaw - 1e-9) ranges.Add((prevRaw, totalRaw, prevDisp));

            const double eps = 1e-9;
            foreach (var r in ranges)
            {
                double segLen = r.RawHi - r.RawLo;
                if (segLen <= eps) continue;
                double dispLo = r.DispLo;
                double dispHi = dispLo + segLen;

                // 首个 ≥ dispLo 的 interval 倍数
                double firstMul = Math.Ceiling(dispLo / interval - eps) * interval;

                for (double d = firstMul; d <= dispHi + eps; d += interval)
                {
                    double raw = r.RawLo + (d - dispLo);
                    if (raw < -eps || raw > totalRaw + eps) continue;
                    raw = Math.Max(0, Math.Min(totalRaw, raw));
                    var sample = new StationSample(raw,
                        poly.PointAtPlanarStation(raw),
                        poly.TangentAtPlanarStation(raw));
                    result.Add((sample, d));
                }
            }
            return result;
        }

        /// <summary>把 display 桩号化为"毫米级"整数键，用于主/副桩去重。</summary>
        private static long DisplayKey(double display) => (long)Math.Round(display * 1000.0);

        /// <summary>
        /// 清除指定 Alignment 挂在 DWG 上的全部桩号标注实体（HY_ROAD KIND=StationLabel，ID=alignmentId）。
        /// 当 Alignment 被删除或用户主动擦除标注时调用。
        /// </summary>
        /// <returns>被擦除的实体数量。</returns>
        public int ClearStationLabels(Transaction transaction, Database database, Guid alignmentId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (alignmentId == Guid.Empty) return 0;

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, StationLabelKind, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }

            foreach (var id in toErase)
            {
                var ent = transaction.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        /// <summary>
        /// 单个桩号"钉子"：刻度线 + （主桩时）桩号文字。
        /// 切向 / 法向 / 锚点计算全部基于 Domain 的 <see cref="Vector2D"/>，避免与 AutoCAD 几何库反复转换。
        /// </summary>
        private static void AppendStationTick(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Alignment alignment,
            StationSample sample,
            double displayStation,
            RoadStationLabelOptions options,
            bool isMain,
            string layerName)
        {
            var tangent = sample.Tangent;
            if (!tangent.TryNormalize(out var tUnit)) tUnit = Vector2D.UnitX;

            // 左侧法向（逆时针 90°）；根据 TextSide 决定文字朝哪侧
            var leftNormal = tUnit.Perpendicular();
            double tickHalf = (isMain ? options.TickLengthMain : options.TickLengthSub) / 2.0;

            var center = sample.Point;
            var a = new Point3d(center.X - leftNormal.X * tickHalf, center.Y - leftNormal.Y * tickHalf, center.Z);
            var b = new Point3d(center.X + leftNormal.X * tickHalf, center.Y + leftNormal.Y * tickHalf, center.Z);

            // 刻度线
            var line = new Line(a, b);
            if (!string.IsNullOrEmpty(layerName)) line.Layer = layerName;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            HyRoadXdata.Write(tr, db, line, alignment.Id, StationLabelKind, SchemaVersion.Current);

            // 仅主桩才写文字
            if (!isMain) return;

            var textSideNormal = options.TextSide == StationTextSide.Left ? leftNormal : -leftNormal;
            double textAnchorOffset = tickHalf + options.TextMargin;
            double textX = center.X + textSideNormal.X * textAnchorOffset;
            double textY = center.Y + textSideNormal.Y * textAnchorOffset;

            // 桩号字符串：调用方已把 display 值算好（对齐到 display 整数倍），此处直接使用。
            var text = new DBText
            {
                TextString = AlignmentStationBreakdown.FormatStation(displayStation),
                Height = options.TextHeight,
                Position = new Point3d(textX, textY, center.Z)
            };
            if (!string.IsNullOrEmpty(layerName)) text.Layer = layerName;
            if (options.RotateTextAlongTangent)
            {
                text.Rotation = Math.Atan2(tUnit.Y, tUnit.X);
            }

            ms.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
            HyRoadXdata.Write(tr, db, text, alignment.Id, StationLabelKind, SchemaVersion.Current);
        }

        /// <summary>
        /// HY_ROAD 里几何点标注实体统一的 KIND 值（hyRoadAlnGeomPt 专用）。
        /// </summary>
        private const string GeometryPointLabelKind = "GeometryPointLabel";

        /// <summary>
        /// 沿指定 Alignment 在每个几何点（BP / EP / BC / EC / TS / SC / CS / ST，可选 PI）画一组"钉子 + 引线 + 两行文字"。
        ///
        /// 幂等策略：
        /// - 本方法内部先调 <see cref="ClearGeometryPointLabels"/> 删除同一 AlignmentId 的历史几何点实体；
        /// - 再按 <paramref name="options"/> 的样式批量生成。
        /// 这样用户可以反复跑 <c>hyRoadAlnGeomPt</c> 更新标注，而不会累积重复图元。
        ///
        /// 落图规则：
        /// - 标记圆：<see cref="Circle"/>，挂到 <see cref="HyRoadLayers.GeometryPointLayer"/>；
        /// - 引线 + 文字：<see cref="Line"/> + <see cref="DBText"/>（两行：点名 / 桩号）；
        /// - 所有实体挂 HY_ROAD XData：<c>KIND=<see cref="GeometryPointLabelKind"/></c>，<c>ID=alignmentId</c>。
        ///
        /// 几何点的桩号 / 坐标 / 切向由 <see cref="AlignmentStationBreakdown.Build"/> 提供；
        /// 因此需要 <see cref="Alignment.Source"/>.PiElements 非空（按 PI 创建的 Alignment）。
        /// </summary>
        /// <returns>生成的几何点数量（未登记 / PI 表缺失 / 无效 Alignment 时返回 0）。</returns>
        public int DrawGeometryPointLabels(
            string documentName,
            Transaction transaction,
            Database database,
            Guid alignmentId,
            RoadGeometryPointLabelOptions options = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (alignmentId == Guid.Empty) throw new ArgumentException("alignmentId cannot be empty", nameof(alignmentId));

            options = options ?? RoadGeometryPointLabelOptions.Default;
            options.Validate();

            if (!_registry.TryGet(documentName, out var design)) return 0;
            var alignment = design.Alignments.FirstOrDefault(a => a.Id == alignmentId);
            if (alignment == null) return 0;
            if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
                return 0;

            var elements = alignment.Source.PiElements
                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                .ToList();

            AlignmentBreakdown breakdown;
            try
            {
                breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation, null, alignment.StationEquations);
            }
            catch
            {
                return 0;
            }

            // 先清旧（幂等）
            ClearGeometryPointLabels(transaction, database, alignmentId);

            string layerName = HyRoadLayers.GeometryPointLayer;
            bool useLayer = LayerExists(transaction, database, layerName);

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int count = 0;
            foreach (var gp in breakdown.GeometryPoints)
            {
                // 根据选项决定是否跳过纯 PI 点
                if (gp.Kind == GeometryPointKind.PI && !options.LabelPlainPi) continue;

                // 求该点处的切向：优先"以该点为起点的段"的 startBearing；
                // 没有就用"以该点为终点的段"的 endBearing（末点 EP 常见）。
                if (!TryFindTangent(breakdown, gp, out double bearingRad))
                {
                    bearingRad = 0; // 退回水平
                }

                AppendGeometryPointLabel(
                    transaction, ms, database, alignment, gp, bearingRad,
                    options, useLayer ? layerName : null);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 清除指定 Alignment 挂在 DWG 上的全部几何点标注实体（HY_ROAD KIND=<see cref="GeometryPointLabelKind"/>、ID=alignmentId）。
        /// </summary>
        /// <returns>被擦除的实体数量。</returns>
        public int ClearGeometryPointLabels(Transaction transaction, Database database, Guid alignmentId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (alignmentId == Guid.Empty) return 0;

            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, GeometryPointLabelKind, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }

            foreach (var id in toErase)
            {
                var ent = transaction.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        /// <summary>
        /// 在 <paramref name="breakdown"/> 的 Segments 中查找与 <paramref name="gp"/> 桩号吻合的切向。
        /// 规则：按"以 gp 为起点的段"优先，否则按"以 gp 为终点的段"兜底；都找不到返回 false。
        /// </summary>
        private static bool TryFindTangent(
            AlignmentBreakdown breakdown,
            GeometryPoint gp,
            out double bearingRad)
        {
            const double sTol = 1e-6;
            foreach (var seg in breakdown.Segments)
            {
                if (Math.Abs(seg.StationStartM - gp.StationM) < sTol)
                {
                    bearingRad = seg.StartBearingRad;
                    return true;
                }
            }
            foreach (var seg in breakdown.Segments)
            {
                if (Math.Abs(seg.StationEndM - gp.StationM) < sTol)
                {
                    bearingRad = seg.EndBearingRad;
                    return true;
                }
            }
            bearingRad = 0;
            return false;
        }

        /// <summary>
        /// 单个几何点"钉子"：标记圆 + 引线 + 两行文字（点名 / 桩号）。
        /// 文字保持水平（不跟切向旋转），方便出图阅读；位置沿切向法向偏出引线。
        /// </summary>
        private static void AppendGeometryPointLabel(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Alignment alignment,
            GeometryPoint gp,
            double bearingRad,
            RoadGeometryPointLabelOptions options,
            string layerName)
        {
            var tUnit = new Vector2D(Math.Cos(bearingRad), Math.Sin(bearingRad));
            var leftNormal = tUnit.Perpendicular();
            var sideNormal = options.TextSide == StationTextSide.Left ? leftNormal : -leftNormal;

            var center = gp.Point;
            var centerPt3 = new Point3d(center.X, center.Y, 0);

            // 1) 标记圆
            var circle = new Circle(centerPt3, Vector3d.ZAxis, options.MarkerRadius);
            if (!string.IsNullOrEmpty(layerName)) circle.Layer = layerName;
            ms.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            HyRoadXdata.Write(tr, db, circle, alignment.Id, GeometryPointLabelKind, SchemaVersion.Current);

            // 2) 引线：圆外缘 → 外侧文字锚点
            double leaderStartOffset = options.MarkerRadius;
            double leaderEndOffset = options.MarkerRadius + options.LeaderLength;
            var leaderStart = new Point3d(
                center.X + sideNormal.X * leaderStartOffset,
                center.Y + sideNormal.Y * leaderStartOffset,
                0);
            var leaderEnd = new Point3d(
                center.X + sideNormal.X * leaderEndOffset,
                center.Y + sideNormal.Y * leaderEndOffset,
                0);
            var leader = new Line(leaderStart, leaderEnd);
            if (!string.IsNullOrEmpty(layerName)) leader.Layer = layerName;
            ms.AppendEntity(leader);
            tr.AddNewlyCreatedDBObject(leader, true);
            HyRoadXdata.Write(tr, db, leader, alignment.Id, GeometryPointLabelKind, SchemaVersion.Current);

            // 3) 两行文字：点名（第 1 行，紧邻引线末端） / 桩号（第 2 行）
            double textAnchorOffset = leaderEndOffset + options.TextMargin;
            double nameTextX = center.X + sideNormal.X * textAnchorOffset;
            double nameTextY = center.Y + sideNormal.Y * textAnchorOffset;

            var nameText = new DBText
            {
                TextString = gp.Kind.ToString(),
                Height = options.NameTextHeight,
                Position = new Point3d(nameTextX, nameTextY, 0)
            };
            if (!string.IsNullOrEmpty(layerName)) nameText.Layer = layerName;
            ms.AppendEntity(nameText);
            tr.AddNewlyCreatedDBObject(nameText, true);
            HyRoadXdata.Write(tr, db, nameText, alignment.Id, GeometryPointLabelKind, SchemaVersion.Current);

            // 桩号文字挂在点名下方：延同一法向 + 一个文字高度向下偏移（沿中心线的"上游方向"= -tUnit）
            // 为了在地图平面上"往下"看起来像换行，实际用 sideNormal 继续推得远一点更稳：但这样会被误读成另一点。
            // 折中：沿 -sideNormal 的垂直方向"行距"往"更远外侧"偏一行（即 sideNormal * lineSpacing）。
            double stationAnchorOffset = textAnchorOffset + options.NameTextHeight + options.TextLineSpacing;
            double stationTextX = center.X + sideNormal.X * stationAnchorOffset;
            double stationTextY = center.Y + sideNormal.Y * stationAnchorOffset;

            var stationText = new DBText
            {
                TextString = AlignmentStationBreakdown.FormatStation(gp.StationM),
                Height = options.StationTextHeight,
                Position = new Point3d(stationTextX, stationTextY, 0)
            };
            if (!string.IsNullOrEmpty(layerName)) stationText.Layer = layerName;
            ms.AppendEntity(stationText);
            tr.AddNewlyCreatedDBObject(stationText, true);
            HyRoadXdata.Write(tr, db, stationText, alignment.Id, GeometryPointLabelKind, SchemaVersion.Current);
        }

        /// <summary>
        /// 就地重建指定 Alignment 的中心线几何：同步 Domain + DWG Polyline + JSON。
        ///
        /// 语义：
        /// - 保持 <see cref="Alignment.Id"/>、Xdata、ObjectId、图层、用户标签不变；
        /// - 替换 Domain <see cref="Alignment.Centerline"/>；
        /// - 查找同 AlignmentId 的 HY_ROAD Polyline，就地 <see cref="RoadGeometryBridge.UpdateAutoCadPolyline"/>；
        /// - 发布 <see cref="RoadChangeKind.Updated"/> 事件，触发写盘。
        ///
        /// 使用场景：
        /// - hyRoadAlnEditPi 编辑一个 PI 的 R / Ls 参数后，调用 Designer 重建 Polyline3D，再调本方法落图。
        /// - 未来批量参数编辑器（P1.c 面板）同样可以复用。
        /// </summary>
        /// <returns>true = 找到并重建；false = Domain / DWG 不匹配（未找到对应 Alignment 或 Polyline）。</returns>
        public bool RebuildCenterline(
            string documentName,
            Transaction transaction,
            Database database,
            Guid alignmentId,
            Polyline3D newCenterline)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (newCenterline == null) throw new ArgumentNullException(nameof(newCenterline));
            if (alignmentId == Guid.Empty) throw new ArgumentException("alignmentId cannot be empty", nameof(alignmentId));

            if (!_registry.TryGet(documentName, out var design)) return false;
            var alignment = design.Alignments.FirstOrDefault(a => a.Id == alignmentId);
            if (alignment == null) return false;

            // 在 ModelSpace 中定位同一 AlignmentId 的 Polyline
            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            Polyline target = null;
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (!(ent is Polyline poly)) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid != alignmentId) continue;
                target = poly;
                break;
            }

            if (target == null) return false;

            if (!target.IsWriteEnabled) target.UpgradeOpen();
            RoadGeometryBridge.UpdateAutoCadPolyline(target, newCenterline);
            alignment.Centerline = newCenterline;

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignment.Id, RoadChangeKind.Updated));
            return true;
        }

        /// <summary>
        /// 从 Domain 聚合根删除平面线位。真实 DWG 清理留给命令层（可选）。
        /// </summary>
        public bool Delete(string documentName, Guid alignmentId)
        {
            if (!_registry.TryGet(documentName, out var design)) return false;
            int removed = design.Alignments.RemoveAll(a => a.Id == alignmentId);
            if (removed == 0) return false;

            design.LastModifiedUtc = DateTime.UtcNow;
            _eventBus.Publish(new AlignmentChangedEvent(design.Id, alignmentId, RoadChangeKind.Deleted));
            return true;
        }

        /// <summary>
        /// 扫描当前 DWG 的 HY_ROAD Xdata，必要时把 Registry 里 "属于本 DWG 但挂在老 key" 的 design
        /// 迁到 <paramref name="documentName"/> 这个新 key。
        ///
        /// 场景：
        /// - SAVEAS 把 <c>Drawing1.dwg</c> 另存为 <c>Drawing3.dwg</c> 后，<c>MdiActiveDocument.Name</c> 变成新路径，
        ///   但 Registry 里的道路数据还挂在老 key 下。直接跑 <c>hyRoadSave</c> / <c>hyRoadA</c> 会 <c>GetOrCreate</c>
        ///   出一个同名但空的 design → 最终产生"空 <c>.roaddesign.json</c>"、"hyRoadLoad 一直跑回老数据"等诡异现象。
        /// - 打开另一个"Xdata 相同但 DWG 不同名"的副本时，也希望优先复用内存中的 design 而不是新建。
        ///
        /// 决策：
        /// - 以 <strong>Xdata 里的 <see cref="Alignment.Id"/> 作为身份锚</strong>（决策 4 - DWG Xdata）；
        /// - 当前 doc.Name 对应的 design 已包含至少一个 Xdata Guid ⇒ 不动；
        /// - 其他 key 下的 design 包含 Xdata Guid ⇒ 通过 <see cref="RoadDesignRegistry.Rekey"/> 迁移，
        ///   或（当前 key 已有空壳时）<see cref="RoadDesignRegistry.Replace"/> 覆盖，顺带发布 Reloaded 事件、触发写盘；
        /// - DWG 里根本没有 HY_ROAD Xdata ⇒ 不动（Registry 应保持和 DWG 一致）。
        /// </summary>
        /// <returns>绑定到 <paramref name="documentName"/> 的 design；没有可绑定的候选时返回 <c>null</c>（或当前 key 下已有的空 design）。</returns>
        public RoadDesign RebindForDocument(string documentName, Transaction transaction, Database database)
        {
            if (string.IsNullOrEmpty(documentName)) return null;
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));

            // 1) 扫 Xdata：收集本 DWG 的所有 HY_ROAD Alignment Guid
            var xdataIds = new HashSet<Guid>();
            var bt = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = transaction.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var gid = HyRoadXdata.ReadId(transaction, ent);
                if (gid == Guid.Empty) continue;
                var kind = HyRoadXdata.ReadKind(transaction, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                xdataIds.Add(gid);
            }

            _registry.TryGet(documentName, out var current);

            // 2) 当前 key 已经能覆盖所有 Xdata 身份 ⇒ 无需 rebind
            if (current != null)
            {
                if (xdataIds.Count == 0) return current;
                bool allCovered = true;
                foreach (var xid in xdataIds)
                {
                    if (!current.Alignments.Any(a => a.Id == xid))
                    {
                        allCovered = false;
                        break;
                    }
                }
                if (allCovered) return current;
            }

            if (xdataIds.Count == 0) return current;

            // 3) 找"至少含一个 Xdata Guid"的旧 key
            string oldKey = null;
            RoadDesign oldDesign = null;
            foreach (var kv in _registry.Snapshot())
            {
                if (string.Equals(kv.Key, documentName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var a in kv.Value.Alignments)
                {
                    if (xdataIds.Contains(a.Id))
                    {
                        oldKey = kv.Key;
                        oldDesign = kv.Value;
                        break;
                    }
                }
                if (oldDesign != null) break;
            }

            if (oldDesign == null) return current;

            // 4) 执行迁移
            if (current == null)
            {
                // 新 key 空 → 直接 rekey，不发事件、不触发写盘；调用方会按需发起 Save
                _registry.Rekey(oldKey, documentName);
                return oldDesign;
            }

            // 新 key 已存在（多半是 GetOrCreate 出来的空壳）：
            // 先把老 key 删掉避免孤儿，再 Replace 新 key（Replace 会发布 Reloaded 事件，
            // 自动持久化服务会据此把 JSON 写到新 path；老 path 的 .roaddesign.json 作为历史残留）
            _registry.Remove(oldKey);
            _registry.Replace(documentName, oldDesign);
            return oldDesign;
        }
    }
}
