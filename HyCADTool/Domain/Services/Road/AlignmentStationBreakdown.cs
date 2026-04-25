using System;
using System.Collections.Generic;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// PI 法平面线位的"段 + 几何点 + 桩号"三位一体分解器。
    ///
    /// 设计动机（07Alignment.md §8.4 T0）：
    /// <list type="bullet">
    ///   <item><see cref="AlignmentPiDesigner"/> 只输出 <see cref="Polyline3D"/> + 每 PI 诊断，<b>缺桩号信息</b>；</item>
    ///   <item>后续四个上层功能（Sub-Entity 全表 / 几何点标注 / 交点表导出 / 复测表导出）都依赖
    ///     「每一段的起止桩号 + 每一个几何点的桩号」；</item>
    ///   <item>为避免四处重复推导桩号链（易错 / 不一致），抽取为 Domain 单一入口。</item>
    /// </list>
    ///
    /// 算法：沿 <see cref="AlignmentPiDesigner.Build"/> 主循环的分支逻辑重放一次，
    /// 但输出「段 + 几何点」而不是 Polyline 顶点。桩号按工程标准公式（非 bulge 近似）累加：
    /// <list type="bullet">
    ///   <item>直线段长 = 弦长；</item>
    ///   <item>缓和段长 = <c>Ls</c>（回旋线定义即弧长参数）；</item>
    ///   <item>圆曲线段长 = <c>R · θ_c</c>，<c>θ_c = |turn| - (Ls_in + Ls_out) / (2R)</c>；
    ///     无缓和曲线时 <c>θ_c = |turn|</c>。</item>
    /// </list>
    ///
    /// 与 <see cref="Polyline3D.GetPlanarLength"/>（bulge 近似）的差异 &lt; 0.1 m/PI，
    /// 对报表/标注精度足够；这里取「理论值」作为单一真值。
    /// </summary>
    public static class AlignmentStationBreakdown
    {
        private const double ArcAngleTolerance = 1e-9;

        /// <summary>
        /// 按 PI 序列 + 起始桩号 分解为「段记录 + 几何点」。
        ///
        /// 与 <see cref="AlignmentPiDesigner.Build"/> 使用同一份 <see cref="PiDesignOptions"/>，
        /// 以保证分支判定（Straight / Curved / Spiraled / Skipped）与落图几何一致。
        /// </summary>
        /// <param name="elements">PI 序列，≥ 2 个；与落图的 Alignment.Source.PiElements 完全一致。</param>
        /// <param name="startStation">Alignment 起始桩号（m），通常取 <c>Alignment.StartStation</c>。</param>
        /// <param name="options">与 Designer 共享的选项；null 取默认。</param>
        /// <returns>不可变分解结果；首尾始终有 BP / EP 两个几何点。</returns>
        public static AlignmentBreakdown Build(
            IReadOnlyList<PiElement> elements,
            double startStation = 0,
            PiDesignOptions options = null)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (elements.Count < 2)
                throw new ArgumentException("至少需要 2 个 PI 点才能分解。", nameof(elements));

            var opts = options ?? new PiDesignOptions();
            double tolerance = opts.Tolerance;
            double minTurnRad = opts.MinTurnDeg * Math.PI / 180.0;

            for (int k = 1; k < elements.Count; k++)
            {
                if (elements[k - 1].P.DistanceTo(elements[k].P) < tolerance)
                    throw new ArgumentException(
                        $"PI[{k - 1}] 与 PI[{k}] 重合（距离 < {tolerance:E2} m）。",
                        nameof(elements));
            }

            var segments = new List<SegmentRecord>();
            var points = new List<GeometryPoint>();
            double s = startStation;

            var p0 = elements[0].P;
            points.Add(new GeometryPoint(0, GeometryPointKind.BP, s, p0));

            Point2D prevEnd = p0;
            // 上一段末端的切向（= 下一段起始切向），首段等到 AppendLineSegment 第一次调用时再算
            double prevBearing = double.NaN;

            for (int i = 1; i < elements.Count - 1; i++)
            {
                var prev = elements[i - 1].P;
                var curr = elements[i].P;
                var next = elements[i + 1].P;
                double radius = elements[i].Radius > 0 ? elements[i].Radius : opts.FallbackRadius;
                double lsIn = Math.Max(0, elements[i].SpiralIn);
                double lsOut = Math.Max(0, elements[i].SpiralOut);

                var tIn = new Vector2D(curr.X - prev.X, curr.Y - prev.Y);
                var tOut = new Vector2D(next.X - curr.X, next.Y - curr.Y);
                double segInLen = tIn.Length;
                double segOutLen = tOut.Length;

                if (!tIn.TryNormalize(out var tInU, tolerance) ||
                    !tOut.TryNormalize(out var tOutU, tolerance))
                {
                    AppendLineSegment(segments, ref s, ref prevEnd, ref prevBearing, curr, piIndex: -1);
                    points.Add(new GeometryPoint(i, GeometryPointKind.PI, s, curr));
                    continue;
                }

                double cross = tInU.Cross(tOutU);
                double dot = tInU.Dot(tOutU);
                double turn = Math.Atan2(cross, dot);
                double absTurn = Math.Abs(turn);

                // Straight 分支：与 AlignmentPiDesigner 判定逻辑一致
                if (radius <= 0 ||
                    absTurn < minTurnRad ||
                    Math.Abs(absTurn - Math.PI) < minTurnRad)
                {
                    AppendLineSegment(segments, ref s, ref prevEnd, ref prevBearing, curr, piIndex: -1);
                    points.Add(new GeometryPoint(i, GeometryPointKind.PI, s, curr));
                    continue;
                }

                double tanHalf = Math.Abs(Math.Tan(turn / 2.0));
                double tArc = radius * tanHalf;
                bool useSpiral = lsIn > 0 || lsOut > 0;
                double tInTotal = tArc;
                double tOutTotal = tArc;

                if (useSpiral)
                {
                    double pIn = lsIn * lsIn / (24.0 * radius);
                    double qIn = lsIn / 2.0 - Math.Pow(lsIn, 3) / (240.0 * radius * radius);
                    double pOut = lsOut * lsOut / (24.0 * radius);
                    double qOut = lsOut / 2.0 - Math.Pow(lsOut, 3) / (240.0 * radius * radius);
                    tInTotal = (radius + pIn) * tanHalf + qIn;
                    tOutTotal = (radius + pOut) * tanHalf + qOut;
                }

                bool fitsWithSpiral =
                    tInTotal <= segInLen - tolerance &&
                    tOutTotal <= segOutLen - tolerance;

                if (!fitsWithSpiral && useSpiral)
                {
                    bool fitsArcOnly =
                        tArc <= segInLen - tolerance &&
                        tArc <= segOutLen - tolerance;
                    if (fitsArcOnly)
                    {
                        useSpiral = false;
                        lsIn = 0;
                        lsOut = 0;
                        tInTotal = tArc;
                        tOutTotal = tArc;
                        fitsWithSpiral = true;
                    }
                }

                if (!fitsWithSpiral)
                {
                    AppendLineSegment(segments, ref s, ref prevEnd, ref prevBearing, curr, piIndex: -1);
                    points.Add(new GeometryPoint(i, GeometryPointKind.PI, s, curr));
                    continue;
                }

                var ts = new Point2D(curr.X - tInU.X * tInTotal, curr.Y - tInU.Y * tInTotal);
                var st = new Point2D(curr.X + tOutU.X * tOutTotal, curr.Y + tOutU.Y * tOutTotal);

                // 先写一段直线：prevEnd → TS（可能为 0 长度，见 AppendLineSegment 内的 tolerance 跳过逻辑）
                AppendLineSegment(segments, ref s, ref prevEnd, ref prevBearing, ts, piIndex: -1);

                double tInBearing = Math.Atan2(tInU.Y, tInU.X);
                double tOutBearing = Math.Atan2(tOutU.Y, tOutU.X);
                int turnSign = Math.Sign(turn);

                if (useSpiral)
                {
                    var sc = new Point2D(curr.X - tInU.X * tArc, curr.Y - tInU.Y * tArc);
                    var cs = new Point2D(curr.X + tOutU.X * tArc, curr.Y + tOutU.Y * tArc);

                    // 缓和入 TS → SC
                    points.Add(new GeometryPoint(i, GeometryPointKind.TS, s, ts));
                    double scBearing = tInBearing + (lsIn / (2.0 * radius)) * turnSign;
                    AppendSegment(segments, SegmentKind.Spiral,
                        sStart: s, length: lsIn,
                        start: ts, end: sc,
                        startBearing: tInBearing, endBearing: scBearing,
                        radius: radius, spiralA: Math.Sqrt(radius * lsIn), spiralLs: lsIn,
                        piIndex: i,
                        spiralRole: SpiralSegmentRole.Entry);
                    s += lsIn;
                    prevEnd = sc;
                    prevBearing = scBearing;
                    points.Add(new GeometryPoint(i, GeometryPointKind.SC, s, sc));

                    // 圆弧 SC → CS（角度 = |turn| - 两段缓和偏角）
                    double arcAngle = absTurn - (lsIn + lsOut) / (2.0 * radius);
                    if (arcAngle < ArcAngleTolerance) arcAngle = 0;
                    double arcLen = radius * arcAngle;
                    double csBearing = scBearing + arcAngle * turnSign;
                    AppendSegment(segments, SegmentKind.Arc,
                        sStart: s, length: arcLen,
                        start: sc, end: cs,
                        startBearing: scBearing, endBearing: csBearing,
                        radius: radius, spiralA: double.NaN, spiralLs: double.NaN,
                        piIndex: i);
                    s += arcLen;
                    prevEnd = cs;
                    prevBearing = csBearing;
                    points.Add(new GeometryPoint(i, GeometryPointKind.CS, s, cs));

                    // 缓和出 CS → ST
                    AppendSegment(segments, SegmentKind.Spiral,
                        sStart: s, length: lsOut,
                        start: cs, end: st,
                        startBearing: csBearing, endBearing: tOutBearing,
                        radius: radius, spiralA: Math.Sqrt(radius * lsOut), spiralLs: lsOut,
                        piIndex: i,
                        spiralRole: SpiralSegmentRole.Exit);
                    s += lsOut;
                    prevEnd = st;
                    prevBearing = tOutBearing;
                    points.Add(new GeometryPoint(i, GeometryPointKind.ST, s, st));
                }
                else
                {
                    // 纯圆曲线 BC → EC（= TS / ST）
                    points.Add(new GeometryPoint(i, GeometryPointKind.BC, s, ts));
                    double arcLen = radius * absTurn;
                    AppendSegment(segments, SegmentKind.Arc,
                        sStart: s, length: arcLen,
                        start: ts, end: st,
                        startBearing: tInBearing, endBearing: tOutBearing,
                        radius: radius, spiralA: double.NaN, spiralLs: double.NaN,
                        piIndex: i);
                    s += arcLen;
                    prevEnd = st;
                    prevBearing = tOutBearing;
                    points.Add(new GeometryPoint(i, GeometryPointKind.EC, s, st));
                }
            }

            var pEnd = elements[elements.Count - 1].P;
            AppendLineSegment(segments, ref s, ref prevEnd, ref prevBearing, pEnd, piIndex: -1);
            points.Add(new GeometryPoint(elements.Count - 1, GeometryPointKind.EP, s, pEnd));

            return new AlignmentBreakdown(startStation, s - startStation, segments, points);
        }

        /// <summary>
        /// 带桩号方程（Station Equations）的 <see cref="Build(IReadOnlyList{PiElement}, double, PiDesignOptions)"/> 重载。
        ///
        /// 实现思路：先按"无方程"的路径算出 raw breakdown（Segment / GeometryPoint 的桩号 = <paramref name="startStation"/> + rawFromBp），
        /// 再把所有桩号字段（<see cref="SegmentRecord.StationStartM"/> / <see cref="SegmentRecord.StationEndM"/> /
        /// <see cref="GeometryPoint.StationM"/>）经 <see cref="StationConverter.ToDisplayStation"/> 换算为显示桩号。
        ///
        /// <b>不变量</b>：<see cref="AlignmentBreakdown.TotalLengthM"/> 和 <see cref="SegmentRecord.LengthM"/> 永远是
        /// 物理 raw 长度（和方程无关）；段起止桩号差在跨过方程时才会 ≠ 段长，这正是方程的跳变量。
        ///
        /// <paramref name="equations"/> 为 null / 空时等价于普通 Build。
        /// </summary>
        public static AlignmentBreakdown Build(
            IReadOnlyList<PiElement> elements,
            double startStation,
            PiDesignOptions options,
            IReadOnlyList<StationEquation> equations)
        {
            var raw = Build(elements, startStation, options);
            if (equations == null || equations.Count == 0) return raw;

            var mappedSegments = new List<SegmentRecord>(raw.Segments.Count);
            for (int i = 0; i < raw.Segments.Count; i++)
            {
                var seg = raw.Segments[i];
                double rawStart = seg.StationStartM - startStation;
                double rawEnd = seg.StationEndM - startStation;
                double newStart = StationConverter.ToDisplayStation(rawStart, startStation, equations);
                double newEnd = StationConverter.ToDisplayStation(rawEnd, startStation, equations);
                mappedSegments.Add(new SegmentRecord(
                    index: seg.Index,
                    kind: seg.Kind,
                    stationStartM: newStart,
                    stationEndM: newEnd,
                    lengthM: seg.LengthM,
                    radius: seg.Radius,
                    spiralA: seg.SpiralA,
                    spiralLs: seg.SpiralLs,
                    startPoint: seg.StartPoint,
                    endPoint: seg.EndPoint,
                    startBearingRad: seg.StartBearingRad,
                    endBearingRad: seg.EndBearingRad,
                    piIndex: seg.PiIndex,
                    spiralRole: seg.SpiralRole));
            }

            var mappedPoints = new List<GeometryPoint>(raw.GeometryPoints.Count);
            for (int i = 0; i < raw.GeometryPoints.Count; i++)
            {
                var gp = raw.GeometryPoints[i];
                double rawFromBp = gp.StationM - startStation;
                double newStation = StationConverter.ToDisplayStation(rawFromBp, startStation, equations);
                mappedPoints.Add(new GeometryPoint(gp.PiIndex, gp.Kind, newStation, gp.Point));
            }

            // BP 处的显示桩号即 startStation（raw=0），保持不变；TotalLengthM 保留物理长度。
            return new AlignmentBreakdown(startStation, raw.TotalLengthM, mappedSegments, mappedPoints);
        }

        /// <summary>
        /// 添加一段直线；若目标点与 <paramref name="prevEnd"/> 重合则跳过（常见于 Curved PI 两侧直线被缓和/圆弧吃掉的情形）。
        /// </summary>
        private static void AppendLineSegment(
            List<SegmentRecord> segments,
            ref double s,
            ref Point2D prevEnd,
            ref double prevBearing,
            Point2D target,
            int piIndex)
        {
            double dx = target.X - prevEnd.X;
            double dy = target.Y - prevEnd.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return; // 0 长度段不记

            double bearing = Math.Atan2(dy, dx);

            segments.Add(new SegmentRecord(
                index: segments.Count,
                kind: SegmentKind.Line,
                stationStartM: s,
                stationEndM: s + len,
                lengthM: len,
                radius: double.NaN,
                spiralA: double.NaN,
                spiralLs: double.NaN,
                startPoint: prevEnd,
                endPoint: target,
                startBearingRad: bearing,
                endBearingRad: bearing,
                piIndex: piIndex));

            s += len;
            prevEnd = target;
            prevBearing = bearing;
        }

        /// <summary>内部：通用段记录构造。不推进外部状态（由调用点负责）。</summary>
        private static void AppendSegment(
            List<SegmentRecord> segments,
            SegmentKind kind,
            double sStart,
            double length,
            Point2D start,
            Point2D end,
            double startBearing,
            double endBearing,
            double radius,
            double spiralA,
            double spiralLs,
            int piIndex,
            SpiralSegmentRole spiralRole = SpiralSegmentRole.None)
        {
            segments.Add(new SegmentRecord(
                index: segments.Count,
                kind: kind,
                stationStartM: sStart,
                stationEndM: sStart + length,
                lengthM: length,
                radius: radius,
                spiralA: spiralA,
                spiralLs: spiralLs,
                startPoint: start,
                endPoint: end,
                startBearingRad: startBearing,
                endBearingRad: endBearing,
                piIndex: piIndex,
                spiralRole: spiralRole));
        }

        /// <summary>
        /// 把米为单位的 raw 桩号格式化为 "K{km}+{m:000.000}"（Civil 3D / 鸿业 / 国内现场通用）。
        /// 负桩号加前缀 "-"，mm 精度规整后再拆分 km，避免进位抖动。
        /// </summary>
        public static string FormatStation(double stationMeter)
        {
            double abs = Math.Round(Math.Abs(stationMeter), 3, MidpointRounding.AwayFromZero);
            int km = (int)(abs / 1000);
            double remainder = abs - km * 1000;
            string sign = stationMeter < 0 ? "-" : string.Empty;
            return $"{sign}K{km}+{remainder:000.000}";
        }
    }

    /// <summary>Alignment 的段类型（出 Sub-Entity 表 / 出图过滤用）。</summary>
    public enum SegmentKind
    {
        /// <summary>直线段。</summary>
        Line = 0,
        /// <summary>缓和曲线段（回旋线）。</summary>
        Spiral = 1,
        /// <summary>圆曲线段。</summary>
        Arc = 2,
    }

    /// <summary>缓和曲线段在 PI 处的角色（入缓 / 出缓），用于预览着色区分。</summary>
    public enum SpiralSegmentRole
    {
        /// <summary>非缓和段或未定。</summary>
        None = 0,
        /// <summary>入缓 TS → SC。</summary>
        Entry = 1,
        /// <summary>出缓 CS → ST。</summary>
        Exit = 2,
    }

    /// <summary>
    /// Alignment 的几何点（过渡点）类型。对应 Civil 3D / 鸿业的 Geometry Point 标注。
    /// </summary>
    public enum GeometryPointKind
    {
        /// <summary>Begin Point 起点。</summary>
        BP = 0,
        /// <summary>End Point 终点。</summary>
        EP = 1,
        /// <summary>Point of Intersection 切线交点（保留 PI 顶点、不加圆角的情形）。</summary>
        PI = 2,
        /// <summary>Begin of Curve 直 → 圆。</summary>
        BC = 3,
        /// <summary>End of Curve 圆 → 直。</summary>
        EC = 4,
        /// <summary>Tangent → Spiral 直 → 缓。</summary>
        TS = 5,
        /// <summary>Spiral → Curve 缓 → 圆。</summary>
        SC = 6,
        /// <summary>Curve → Spiral 圆 → 缓。</summary>
        CS = 7,
        /// <summary>Spiral → Tangent 缓 → 直。</summary>
        ST = 8,
    }

    /// <summary>
    /// <see cref="AlignmentStationBreakdown.Build"/> 的结果：起桩号 + 总长 + 段列表 + 几何点列表。
    /// </summary>
    public sealed class AlignmentBreakdown
    {
        /// <summary>Alignment 起始桩号（m），即 BP 处的桩号。</summary>
        public double StartStationM { get; }

        /// <summary>总长度（m，理论值；= 段长之和）。</summary>
        public double TotalLengthM { get; }

        /// <summary>段列表，按桩号递增排序，Kind ∈ {Line, Spiral, Arc}。</summary>
        public IReadOnlyList<SegmentRecord> Segments { get; }

        /// <summary>
        /// 几何点列表，按桩号递增排序。首元素恒为 BP，末元素恒为 EP。
        /// 普通圆曲线 PI 产生 BC/EC；带缓和曲线的 PI 产生 TS/SC/CS/ST；
        /// 共线 / 无半径 / 切线长超限的 PI 保留为 PI 类型。
        /// </summary>
        public IReadOnlyList<GeometryPoint> GeometryPoints { get; }

        public AlignmentBreakdown(
            double startStationM,
            double totalLengthM,
            IReadOnlyList<SegmentRecord> segments,
            IReadOnlyList<GeometryPoint> geometryPoints)
        {
            StartStationM = startStationM;
            TotalLengthM = totalLengthM;
            Segments = segments ?? Array.Empty<SegmentRecord>();
            GeometryPoints = geometryPoints ?? Array.Empty<GeometryPoint>();
        }

        /// <summary>终点桩号（m）= <see cref="StartStationM"/> + <see cref="TotalLengthM"/>。</summary>
        public double EndStationM => StartStationM + TotalLengthM;
    }

    /// <summary>
    /// Alignment 中一段（直/缓/圆）的完整记录。字段全部只读，适合作为 UI 数据网格行。
    /// </summary>
    public readonly struct SegmentRecord
    {
        /// <summary>段序号（0 基）。</summary>
        public int Index { get; }

        /// <summary>段类型（<see cref="SegmentKind.Line"/> / <see cref="SegmentKind.Spiral"/> / <see cref="SegmentKind.Arc"/>）。</summary>
        public SegmentKind Kind { get; }

        /// <summary>段起点桩号（m）。</summary>
        public double StationStartM { get; }

        /// <summary>段终点桩号（m）。</summary>
        public double StationEndM { get; }

        /// <summary>段长度（m）。</summary>
        public double LengthM { get; }

        /// <summary>圆/缓和段：R（m）；直线段：<see cref="double.NaN"/>。</summary>
        public double Radius { get; }

        /// <summary>缓和段：参数 A = √(R·Ls)；其他段：<see cref="double.NaN"/>。</summary>
        public double SpiralA { get; }

        /// <summary>缓和段：Ls（m）；其他段：<see cref="double.NaN"/>。</summary>
        public double SpiralLs { get; }

        /// <summary>段起点 (X, Y)。</summary>
        public Point2D StartPoint { get; }

        /// <summary>段终点 (X, Y)。</summary>
        public Point2D EndPoint { get; }

        /// <summary>段起点处的行进方位角（rad，以 X+ 为 0，逆时针正）。</summary>
        public double StartBearingRad { get; }

        /// <summary>段终点处的行进方位角（rad）。</summary>
        public double EndBearingRad { get; }

        /// <summary>所属内部 PI 索引（直线首尾段 = -1）。</summary>
        public int PiIndex { get; }

        /// <summary>缓和段为入缓 / 出缓；非缓和段为 <see cref="SpiralSegmentRole.None"/>。</summary>
        public SpiralSegmentRole SpiralRole { get; }

        public SegmentRecord(
            int index,
            SegmentKind kind,
            double stationStartM,
            double stationEndM,
            double lengthM,
            double radius,
            double spiralA,
            double spiralLs,
            Point2D startPoint,
            Point2D endPoint,
            double startBearingRad,
            double endBearingRad,
            int piIndex,
            SpiralSegmentRole spiralRole = SpiralSegmentRole.None)
        {
            Index = index;
            Kind = kind;
            StationStartM = stationStartM;
            StationEndM = stationEndM;
            LengthM = lengthM;
            Radius = radius;
            SpiralA = spiralA;
            SpiralLs = spiralLs;
            StartPoint = startPoint;
            EndPoint = endPoint;
            StartBearingRad = startBearingRad;
            EndBearingRad = endBearingRad;
            PiIndex = piIndex;
            SpiralRole = spiralRole;
        }

        /// <summary>格式化段类型中文短名：直/缓/圆。</summary>
        public string KindLabel()
        {
            switch (Kind)
            {
                case SegmentKind.Line: return "直";
                case SegmentKind.Spiral: return "缓";
                case SegmentKind.Arc: return "圆";
                default: return "?";
            }
        }
    }

    /// <summary>Alignment 上某个几何点（过渡点）。</summary>
    public readonly struct GeometryPoint
    {
        /// <summary>所属 PI 索引（0 = BP、Count-1 = EP，其他 = 内部 PI）。</summary>
        public int PiIndex { get; }

        /// <summary>几何点类型。</summary>
        public GeometryPointKind Kind { get; }

        /// <summary>桩号（m）。</summary>
        public double StationM { get; }

        /// <summary>XY 坐标。</summary>
        public Point2D Point { get; }

        public GeometryPoint(int piIndex, GeometryPointKind kind, double stationM, Point2D point)
        {
            PiIndex = piIndex;
            Kind = kind;
            StationM = stationM;
            Point = point;
        }

        /// <summary>点名短字符串（与枚举同名）。</summary>
        public string KindLabel() => Kind.ToString();
    }
}
