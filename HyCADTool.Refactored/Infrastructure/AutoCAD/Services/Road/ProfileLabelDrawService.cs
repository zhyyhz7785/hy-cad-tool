using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 纵断面 DWG 标注服务（v1，最小可用）。
    ///
    /// 把 Domain 中的 FG / EG <see cref="Profile"/> 与 <see cref="ProfileFgResult"/> 几何
    /// 转化为模型空间里的 <c>Line</c> / <c>Polyline</c> / <c>Circle</c> / <c>DBText</c>，
    /// 形成"高程网格 + FG/EG 曲线 + PVI 标注"的纵断面图。
    ///
    /// 设计原则：
    /// - 所有实体的 layer 复用 <see cref="HyRoadLayers.StationLayer"/>（网格与文字）+
    ///   <see cref="HyRoadLayers.ProfileLayer"/>（FG / EG 曲线 + PVI 标注），
    ///   未在初始化时创建图层时回退到默认图层（仍可绘制，仅样式不一致）；
    /// - 全部实体挂 HY_ROAD Xdata <c>KIND = <see cref="ProfileLabelKind"/></c> + <c>ID = alignmentId</c>，
    ///   <see cref="Clear"/> 据此做幂等擦除（与 RoadAlignmentService.DrawStationLabels 同款）；
    /// - 不修改 Domain，不发事件总线，不写 JSON（纯视觉副产物）；
    /// - 竖曲线用 <see cref="ProfileLabelDrawOptions.VerticalCurveSegments"/> 等分多段线近似，
    ///   避免引入 Spline（文件体积 + 旧版 AutoCAD 兼容）。
    /// </summary>
    public sealed class ProfileLabelDrawService
    {
        /// <summary>挂在所有"纵断面标注"实体上的 HY_ROAD KIND 值。</summary>
        public const string ProfileLabelKind = "ProfileLabel";

        /// <summary>EG 曲线在 ProfileLayer 上的颜色索引（品紫，与 FG 黄色对比）。</summary>
        private const short EgColorIndex = 6;

        /// <summary>PVI 圆点 + 文字的颜色索引（红，对应"PVI 红点"约定）。</summary>
        private const short PviAnnotationColorIndex = 1;

        // ============================== 入口 ==============================

        /// <summary>
        /// 绘制纵断面标注。调用方负责 <c>tr.Commit()</c>。
        /// </summary>
        /// <param name="tr">已开启的事务。</param>
        /// <param name="db">活动 Database。</param>
        /// <param name="alignmentId">关联 Alignment 的 GUID（写入 HY_ROAD/ID）。</param>
        /// <param name="fg">设计纵断面（必填，<see cref="Profile.IsDesignProfile"/> 应为 true）。</param>
        /// <param name="eg">现状地面线（可选）。</param>
        /// <param name="fgResult">由 <see cref="ProfileFgDesigner.Build"/> 预先构造的 FG 几何。</param>
        /// <param name="origin">纵断面图绘制原点（图纸坐标系）。</param>
        /// <param name="options">绘制配置；null 时使用 <see cref="ProfileLabelDrawOptions.Default"/>。</param>
        public ProfileLabelDrawResult Draw(
            Transaction tr,
            Database db,
            Guid alignmentId,
            Profile fg,
            Profile eg,
            ProfileFgResult fgResult,
            Point3d origin,
            ProfileLabelDrawOptions options = null)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (alignmentId == Guid.Empty) throw new ArgumentException("alignmentId cannot be empty", nameof(alignmentId));
            if (fg == null) throw new ArgumentNullException(nameof(fg));
            if (fgResult == null) throw new ArgumentNullException(nameof(fgResult));

            options = options ?? ProfileLabelDrawOptions.Default;
            options.Validate();

            var result = new ProfileLabelDrawResult();
            if (fg.Vertices.Count == 0 && (eg == null || eg.Vertices.Count == 0))
            {
                return result;
            }

            // 先按 alignmentId 清旧标注，保证幂等
            Clear(tr, db, alignmentId);

            // 计算坐标变换基准
            ResolveBounds(fg, eg, options, out double sMin, out double sMax, out double hRef, out double hMax);
            if (sMax - sMin <= options.MinTotalLengthM)
            {
                return result; // 桩号跨度过小，直接退出
            }

            // 拿 modelspace
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            string profileLayer = LayerExists(tr, db, HyRoadLayers.ProfileLayer) ? HyRoadLayers.ProfileLayer : null;
            string stationLayer = LayerExists(tr, db, HyRoadLayers.StationLayer) ? HyRoadLayers.StationLayer : null;

            // ============ 1) 网格 + 文字 ============
            if (options.DrawGrid)
            {
                DrawGrid(tr, ms, db, alignmentId, options, sMin, sMax, hRef, hMax, origin, stationLayer, result);
            }

            // ============ 2) FG 曲线 ============
            DrawFgCurve(tr, ms, db, alignmentId, options, fgResult, origin, sMin, hRef, profileLayer, result);

            // ============ 3) EG 曲线 ============
            if (options.DrawEg && eg != null && eg.Vertices.Count >= 2)
            {
                DrawEgCurve(tr, ms, db, alignmentId, options, eg, origin, sMin, hRef, profileLayer, result);
            }

            // ============ 4) PVI 红点 + 文字标注 ============
            if (options.DrawPviAnnotations)
            {
                DrawPviAnnotations(tr, ms, db, alignmentId, options, fgResult, origin, sMin, hRef, profileLayer, result);
            }

            return result;
        }

        /// <summary>
        /// 按 <paramref name="alignmentId"/> 擦除当前模型空间内的全部纵断面标注实体。
        /// 与 <see cref="Draw"/> 形成幂等闭环。
        /// </summary>
        public int Clear(Transaction tr, Database db, Guid alignmentId)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (alignmentId == Guid.Empty) return 0;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, ProfileLabelKind, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        // ============================== 内部：坐标 / 边界 ==============================

        /// <summary>
        /// 根据 FG / EG 的 PVI 推算桩号 / 高程包络，决定网格基准。
        /// hRef 取 floor(minH) 以便高程网格落在整数刻度。
        /// </summary>
        internal static void ResolveBounds(Profile fg, Profile eg, ProfileLabelDrawOptions options,
            out double sMin, out double sMax, out double hRef, out double hMax)
        {
            // 用本地变量累计，避免在嵌套函数 / 表达式里直接写 out 参数（C# 编译限制 CS1628）
            double sMinLocal = double.MaxValue;
            double sMaxLocal = double.MinValue;
            double hMinLocal = double.MaxValue;
            double hMaxLocal = double.MinValue;

            AccumulateBounds(fg?.Vertices, ref sMinLocal, ref sMaxLocal, ref hMinLocal, ref hMaxLocal);
            if (eg != null)
            {
                AccumulateBounds(eg.Vertices, ref sMinLocal, ref sMaxLocal, ref hMinLocal, ref hMaxLocal);
            }

            if (sMinLocal == double.MaxValue)
            {
                sMin = 0; sMax = 0; hRef = 0; hMax = 0;
                return;
            }

            sMin = sMinLocal;
            sMax = sMaxLocal;

            // 高程基准向下取整到 ElevationGridIntervalM 的整数倍，使网格刻度落在工整数字
            double step = options.ElevationGridIntervalM > 0 ? options.ElevationGridIntervalM : 1.0;
            hRef = Math.Floor(hMinLocal / step) * step;
            hMax = Math.Ceiling(hMaxLocal / step) * step;
            if (hMax - hRef < step) hMax = hRef + step; // 至少留 1 行高程网格
        }

        private static void AccumulateBounds(
            IEnumerable<ProfileVertex> verts,
            ref double sMin, ref double sMax,
            ref double hMin, ref double hMax)
        {
            if (verts == null) return;
            foreach (var v in verts)
            {
                if (v.Station < sMin) sMin = v.Station;
                if (v.Station > sMax) sMax = v.Station;
                if (v.Elevation < hMin) hMin = v.Elevation;
                if (v.Elevation > hMax) hMax = v.Elevation;
            }
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }

        internal static Point3d ToDrawing(double station, double elevation,
            double sMin, double hRef, double xScale, double yScale, Point3d origin)
        {
            return new Point3d(
                origin.X + (station - sMin) * xScale,
                origin.Y + (elevation - hRef) * yScale,
                origin.Z);
        }

        // ============================== 内部：网格 ==============================

        private static void DrawGrid(
            Transaction tr, BlockTableRecord ms, Database db, Guid alignmentId,
            ProfileLabelDrawOptions options,
            double sMin, double sMax, double hRef, double hMax,
            Point3d origin, string stationLayer, ProfileLabelDrawResult result)
        {
            double sStep = options.StationGridIntervalM;
            double hStep = options.ElevationGridIntervalM;
            double yTop = (hMax - hRef) * options.YScale + origin.Y;
            double yBottom = origin.Y;
            double xLeft = origin.X;
            double xRight = (sMax - sMin) * options.XScale + origin.X;

            // 横向（桩号）竖线 + 桩号文字
            double startS = Math.Ceiling(sMin / sStep) * sStep;
            for (double s = startS; s <= sMax + 1e-9; s += sStep)
            {
                double x = origin.X + (s - sMin) * options.XScale;
                AppendLineWithXdata(tr, ms, db, alignmentId,
                    new Point3d(x, yBottom, origin.Z),
                    new Point3d(x, yTop, origin.Z),
                    stationLayer, colorIndex: 0, result);

                // 桩号文字（K0+xxx）落在底边下方
                var text = new DBText
                {
                    TextString = FormatStation(s),
                    Height = options.TextHeight,
                    Position = new Point3d(x, yBottom - options.TextHeight * 1.5, origin.Z),
                    Rotation = options.RotateStationText ? Math.PI / 2 : 0,
                };
                ApplyLayer(text, stationLayer);
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                HyRoadXdata.Write(tr, db, text, alignmentId, ProfileLabelKind, SchemaVersion.Current);
                result.TextsDrawn++;
            }

            // 纵向（高程）横线 + 高程文字
            for (double h = hRef; h <= hMax + 1e-9; h += hStep)
            {
                double y = origin.Y + (h - hRef) * options.YScale;
                AppendLineWithXdata(tr, ms, db, alignmentId,
                    new Point3d(xLeft, y, origin.Z),
                    new Point3d(xRight, y, origin.Z),
                    stationLayer, colorIndex: 0, result);

                var text = new DBText
                {
                    TextString = h.ToString("F2"),
                    Height = options.TextHeight,
                    Position = new Point3d(xLeft - options.TextHeight * 4, y - options.TextHeight / 2, origin.Z),
                };
                ApplyLayer(text, stationLayer);
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                HyRoadXdata.Write(tr, db, text, alignmentId, ProfileLabelKind, SchemaVersion.Current);
                result.TextsDrawn++;
            }
        }

        // ============================== 内部：FG 曲线 ==============================

        private static void DrawFgCurve(
            Transaction tr, BlockTableRecord ms, Database db, Guid alignmentId,
            ProfileLabelDrawOptions options, ProfileFgResult fgResult,
            Point3d origin, double sMin, double hRef, string profileLayer,
            ProfileLabelDrawResult result)
        {
            foreach (var seg in fgResult.Segments)
            {
                if (seg.Type == ProfileSegmentType.Tangent)
                {
                    var a = ToDrawing(seg.StartStation, seg.StartElevation, sMin, hRef, options.XScale, options.YScale, origin);
                    var b = ToDrawing(seg.EndStation, seg.EndElevation, sMin, hRef, options.XScale, options.YScale, origin);
                    AppendLineWithXdata(tr, ms, db, alignmentId, a, b, profileLayer, colorIndex: 0, result);
                }
                else // VerticalCurve
                {
                    int n = Math.Max(2, options.VerticalCurveSegments);
                    var pl = new Polyline(n + 1);
                    for (int i = 0; i <= n; i++)
                    {
                        double t = (double)i / n;
                        double s = seg.StartStation + t * seg.Length;
                        double h = seg.ElevationAt(s);
                        var p = ToDrawing(s, h, sMin, hRef, options.XScale, options.YScale, origin);
                        pl.AddVertexAt(i, new Point2d(p.X, p.Y), 0, 0, 0);
                    }
                    ApplyLayer(pl, profileLayer);
                    ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    HyRoadXdata.Write(tr, db, pl, alignmentId, ProfileLabelKind, SchemaVersion.Current);
                    result.PolylinesDrawn++;
                }
            }
        }

        // ============================== 内部：EG 曲线 ==============================

        private static void DrawEgCurve(
            Transaction tr, BlockTableRecord ms, Database db, Guid alignmentId,
            ProfileLabelDrawOptions options, Profile eg,
            Point3d origin, double sMin, double hRef, string profileLayer,
            ProfileLabelDrawResult result)
        {
            var pl = new Polyline(eg.Vertices.Count);
            for (int i = 0; i < eg.Vertices.Count; i++)
            {
                var v = eg.Vertices[i];
                var p = ToDrawing(v.Station, v.Elevation, sMin, hRef, options.XScale, options.YScale, origin);
                pl.AddVertexAt(i, new Point2d(p.X, p.Y), 0, 0, 0);
            }
            ApplyLayer(pl, profileLayer);
            pl.ColorIndex = EgColorIndex;
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            HyRoadXdata.Write(tr, db, pl, alignmentId, ProfileLabelKind, SchemaVersion.Current);
            result.PolylinesDrawn++;
        }

        // ============================== 内部：PVI 标注 ==============================

        private static void DrawPviAnnotations(
            Transaction tr, BlockTableRecord ms, Database db, Guid alignmentId,
            ProfileLabelDrawOptions options, ProfileFgResult fgResult,
            Point3d origin, double sMin, double hRef, string profileLayer,
            ProfileLabelDrawResult result)
        {
            for (int i = 0; i < fgResult.Pvis.Count; i++)
            {
                var pi = fgResult.Pvis[i];
                var v = pi.Vertex;
                var p = ToDrawing(v.Station, v.Elevation, sMin, hRef, options.XScale, options.YScale, origin);

                // 红点
                var circle = new Circle(p, Vector3d.ZAxis, options.PviCircleRadius);
                ApplyLayer(circle, profileLayer);
                circle.ColorIndex = PviAnnotationColorIndex;
                ms.AppendEntity(circle);
                tr.AddNewlyCreatedDBObject(circle, true);
                HyRoadXdata.Write(tr, db, circle, alignmentId, ProfileLabelKind, SchemaVersion.Current);
                result.CirclesDrawn++;

                // 文字（K0+xxx, H=hh.hh, R=rrrr）
                string label = pi.CurveLength > 0
                    ? $"{FormatStation(v.Station)}, H={v.Elevation:F2}, R={v.CurveRadius:F0}"
                    : $"{FormatStation(v.Station)}, H={v.Elevation:F2}";
                var text = new DBText
                {
                    TextString = label,
                    Height = options.TextHeight,
                    Position = new Point3d(p.X + options.PviCircleRadius * 1.2, p.Y + options.PviCircleRadius * 1.2, p.Z),
                };
                ApplyLayer(text, profileLayer);
                text.ColorIndex = PviAnnotationColorIndex;
                ms.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(text, true);
                HyRoadXdata.Write(tr, db, text, alignmentId, ProfileLabelKind, SchemaVersion.Current);
                result.TextsDrawn++;
            }
        }

        // ============================== 杂项 ==============================

        private static void AppendLineWithXdata(
            Transaction tr, BlockTableRecord ms, Database db, Guid alignmentId,
            Point3d a, Point3d b, string layer, short colorIndex,
            ProfileLabelDrawResult result)
        {
            var line = new Line(a, b);
            ApplyLayer(line, layer);
            if (colorIndex > 0) line.ColorIndex = colorIndex;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            HyRoadXdata.Write(tr, db, line, alignmentId, ProfileLabelKind, SchemaVersion.Current);
            result.LinesDrawn++;
        }

        private static void ApplyLayer(Entity ent, string layer)
        {
            if (!string.IsNullOrEmpty(layer)) ent.Layer = layer;
        }

        /// <summary>把桩号 m 格式化为 "K{km}+{m:F2}" 字符串（K0+020.00 / K1+200.00）。</summary>
        internal static string FormatStation(double stationM)
        {
            double absS = Math.Abs(stationM);
            int km = (int)(absS / 1000.0);
            double rem = absS - km * 1000.0;
            string sign = stationM < 0 ? "-" : string.Empty;
            return $"{sign}K{km}+{rem:000.00}";
        }
    }

    /// <summary>
    /// <see cref="ProfileLabelDrawService.Draw"/> 的可调参数。
    /// </summary>
    public sealed class ProfileLabelDrawOptions
    {
        /// <summary>桩号方向缩放（图纸单位 / 米）。默认 1（1 m → 1 图纸单位）。</summary>
        public double XScale { get; set; } = 1.0;

        /// <summary>高程方向缩放（图纸单位 / 米）。默认 10（标准纵断面 1:10 高程放大）。</summary>
        public double YScale { get; set; } = 10.0;

        /// <summary>桩号网格间隔（m）。默认 20。</summary>
        public double StationGridIntervalM { get; set; } = 20.0;

        /// <summary>高程网格间隔（m）。默认 1。</summary>
        public double ElevationGridIntervalM { get; set; } = 1.0;

        /// <summary>竖曲线等分段数。默认 30。</summary>
        public int VerticalCurveSegments { get; set; } = 30;

        /// <summary>文字字高（图纸单位）。默认 0.25。</summary>
        public double TextHeight { get; set; } = 0.25;

        /// <summary>PVI 红点圆半径（图纸单位）。默认 0.5。</summary>
        public double PviCircleRadius { get; set; } = 0.5;

        /// <summary>是否绘制网格 + 网格文字。</summary>
        public bool DrawGrid { get; set; } = true;

        /// <summary>是否绘制 EG（地面线）。</summary>
        public bool DrawEg { get; set; } = true;

        /// <summary>是否绘制 PVI 红点 + 文字标注。</summary>
        public bool DrawPviAnnotations { get; set; } = true;

        /// <summary>桩号文字是否旋转 90°（竖排）。默认 false（水平）。</summary>
        public bool RotateStationText { get; set; } = false;

        /// <summary>桩号跨度低于此值时直接退出（默认 0.01 m，避免空 Profile 触发零除）。</summary>
        public double MinTotalLengthM { get; set; } = 0.01;

        public static ProfileLabelDrawOptions Default => new ProfileLabelDrawOptions();

        public void Validate()
        {
            if (XScale <= 0) throw new ArgumentOutOfRangeException(nameof(XScale), "XScale 必须 > 0。");
            if (YScale <= 0) throw new ArgumentOutOfRangeException(nameof(YScale), "YScale 必须 > 0。");
            if (StationGridIntervalM <= 0) throw new ArgumentOutOfRangeException(nameof(StationGridIntervalM));
            if (ElevationGridIntervalM <= 0) throw new ArgumentOutOfRangeException(nameof(ElevationGridIntervalM));
            if (VerticalCurveSegments < 2) throw new ArgumentOutOfRangeException(nameof(VerticalCurveSegments));
            if (TextHeight <= 0) throw new ArgumentOutOfRangeException(nameof(TextHeight));
            if (PviCircleRadius <= 0) throw new ArgumentOutOfRangeException(nameof(PviCircleRadius));
        }
    }

    /// <summary>
    /// <see cref="ProfileLabelDrawService.Draw"/> 的统计输出。
    /// </summary>
    public sealed class ProfileLabelDrawResult
    {
        public int LinesDrawn { get; set; }
        public int TextsDrawn { get; set; }
        public int CirclesDrawn { get; set; }
        public int PolylinesDrawn { get; set; }

        /// <summary>已绘制实体总数。</summary>
        public int Total => LinesDrawn + TextsDrawn + CirclesDrawn + PolylinesDrawn;

        public override string ToString()
            => $"Lines={LinesDrawn}, Polylines={PolylinesDrawn}, Texts={TextsDrawn}, Circles={CirclesDrawn} (Total={Total})";
    }
}
