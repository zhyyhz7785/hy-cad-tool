using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 纵断面设计线 (FG = Finished Grade) 几何生成器。纯 Domain 服务，不依赖 AutoCAD。
    ///
    /// 工程背景：
    /// - 输入是按桩号升序的 PVI（Point of Vertical Intersection，变坡点）序列；
    /// - 每个内部 PVI 携带一个"竖曲线半径" R；R = 0 表示折线（不设竖曲线）。
    /// - 输出是"直坡 ↔ 二次抛物线竖曲线"分段几何，可直接绘制 / 采样。
    ///
    /// 几何公式（CJJ 193-2012 §4.3.3，与鸿业 / 纬地 / Civil 3D 一致）：
    /// <list type="bullet">
    ///   <item>相邻 PVI 间坡度 <c>g = (H_{i+1} - H_i) / (S_{i+1} - S_i)</c>（小数，下坡为负）。</item>
    ///   <item>变坡点 PVI_i 的代数差 <c>ω_i = g_out - g_in</c>（凸 ω &lt; 0，凹 ω &gt; 0）。</item>
    ///   <item>竖曲线长 <c>L_i = |R_i · ω_i|</c>，对称布置：<c>BCV = S_i - L/2</c>，<c>ECV = S_i + L/2</c>。</item>
    ///   <item>竖曲线为二次抛物线，局部坐标 x ∈ [0, L]：<c>y(x) = h_BCV + g_in · x + ω · x² / (2L)</c>。</item>
    ///   <item>BCV 高程 <c>h_BCV = h_PVI - g_in · L/2</c>；ECV 高程 <c>h_ECV = h_PVI + g_out · L/2</c>。</item>
    /// </list>
    ///
    /// 设计约束：
    /// - 不抛异常，错误以 <see cref="ProfileFgResult.Errors"/> 形式返回（除非 vertices 为 null）；
    ///   保证 UI 实时编辑过程中不会因为非法状态而崩溃。
    /// - 端点 PVI 强制 R = 0（首尾没有"前/后坡度"，几何上无法构造竖曲线）。
    /// - 相邻竖曲线重叠（<c>L_i/2 + L_{i+1}/2 &gt; ΔS</c>）以 Warning 形式回填，
    ///   仍按当前 R 切线/抛物线，由 UI 红字提醒，规范校核归 <see cref="ProfileCodeChecker"/>。
    /// </summary>
    public static class ProfileFgDesigner
    {
        /// <summary>
        /// 默认桩号容差（m）。低于此值判定为"重合"。
        /// </summary>
        public const double DefaultTolerance = 1e-6;

        /// <summary>
        /// 由 PVI 列表构造纵断面设计线几何。
        /// </summary>
        /// <param name="vertices">PVI 序列（按桩号升序）；可空（返回带错误的空结果）。</param>
        /// <param name="tolerance">桩号容差，默认 <see cref="DefaultTolerance"/>。</param>
        /// <exception cref="ArgumentNullException">vertices 为 null。</exception>
        public static ProfileFgResult Build(IReadOnlyList<ProfileVertex> vertices, double tolerance = DefaultTolerance)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            var pvis = new List<ProfilePviInfo>(vertices.Count);
            var segments = new List<ProfileSegment>();
            var warnings = new List<string>();
            var errors = new List<string>();

            int n = vertices.Count;
            if (n == 0)
            {
                errors.Add("PVI 序列为空，无法构造纵断面。");
                return new ProfileFgResult(pvis, segments, warnings, errors);
            }

            // 1) 桩号严格升序（不允许 ΔS ≤ tolerance）
            for (int i = 1; i < n; i++)
            {
                double ds = vertices[i].Station - vertices[i - 1].Station;
                if (ds <= tolerance)
                {
                    errors.Add(
                        $"PVI[{i - 1}] 与 PVI[{i}] 桩号倒序或重合（ΔS = {ds:F4} m ≤ {tolerance:E2}）。请按桩号升序排列。");
                }
            }

            if (errors.Count > 0)
            {
                // 计算 PVI 派生信息时桩号顺序是大前提，若错则直接返回（避免后续除零）
                for (int i = 0; i < n; i++)
                {
                    pvis.Add(new ProfilePviInfo(i, vertices[i], 0, 0, 0, 0, 0, 0, 0, 0, false));
                }
                return new ProfileFgResult(pvis, segments, warnings, errors);
            }

            // 2) 计算每个 PVI 的派生信息：g_in, g_out, ω, L, BCV/ECV 桩号 / 高程
            var grades = new double[n + 1];
            for (int i = 1; i < n; i++)
            {
                double dh = vertices[i].Elevation - vertices[i - 1].Elevation;
                double ds = vertices[i].Station - vertices[i - 1].Station;
                grades[i] = dh / ds;
            }
            // 端点 g_in[0] = 0, g_out[n-1] = 0 通过下标越界 0 默认实现

            for (int i = 0; i < n; i++)
            {
                double gIn = (i == 0) ? 0 : grades[i];
                double gOut = (i == n - 1) ? 0 : grades[i + 1];
                bool isInner = i > 0 && i < n - 1;
                double r = isInner ? Math.Max(0, vertices[i].CurveRadius) : 0;
                double omega = isInner ? (gOut - gIn) : 0;
                double length = (r > 0 && Math.Abs(omega) > 1e-12) ? r * Math.Abs(omega) : 0;
                double bcvS = vertices[i].Station - length / 2.0;
                double ecvS = vertices[i].Station + length / 2.0;
                double bcvE = vertices[i].Elevation - gIn * length / 2.0;
                double ecvE = vertices[i].Elevation + gOut * length / 2.0;
                bool isSag = omega > 0; // ω > 0 → 凹（变上坡更陡 / 由下坡转上坡）

                pvis.Add(new ProfilePviInfo(
                    index: i,
                    vertex: vertices[i],
                    gradeIn: gIn,
                    gradeOut: gOut,
                    omega: omega,
                    curveLength: length,
                    bcvStation: bcvS,
                    ecvStation: ecvS,
                    bcvElevation: bcvE,
                    ecvElevation: ecvE,
                    isSag: isSag));
            }

            // 3) 重叠检查：相邻竖曲线的 ECV/BCV 顺序不应反转
            for (int i = 1; i < n - 1; i++)
            {
                if (pvis[i].CurveLength <= 0) continue;

                // 与前一段：当前 BCV ≥ 前一 PVI 桩号（若前一 PVI 是端点）或前一 ECV
                double prevBoundary = (i - 1 == 0)
                    ? vertices[0].Station
                    : pvis[i - 1].EcvStation;
                if (pvis[i].BcvStation + tolerance < prevBoundary)
                {
                    warnings.Add(
                        $"PVI[{i}] 竖曲线 BCV (K{pvis[i].BcvStation:F2}) 与上一段终点 (K{prevBoundary:F2}) 重叠 {prevBoundary - pvis[i].BcvStation:F2} m。");
                }

                // 与后一段：当前 ECV ≤ 下一 BCV（若下一 PVI 是端点）或下一 PVI 桩号
                double nextBoundary = (i + 1 == n - 1)
                    ? vertices[n - 1].Station
                    : pvis[i + 1].BcvStation;
                if (pvis[i].EcvStation > nextBoundary + tolerance)
                {
                    warnings.Add(
                        $"PVI[{i}] 竖曲线 ECV (K{pvis[i].EcvStation:F2}) 超出下一段起点 (K{nextBoundary:F2}) {pvis[i].EcvStation - nextBoundary:F2} m。");
                }
            }

            // 4) 拼接 Segments（直坡 / 竖曲线 交替；单 PVI 无段）
            if (n == 1)
            {
                return new ProfileFgResult(pvis, segments, warnings, errors);
            }

            double cursorS = vertices[0].Station;
            double cursorE = vertices[0].Elevation;
            for (int i = 1; i < n; i++)
            {
                bool curAtPviHasCurve = (i < n - 1) && pvis[i].CurveLength > 0;

                double tangentEndS = curAtPviHasCurve ? pvis[i].BcvStation : vertices[i].Station;
                double tangentEndE = curAtPviHasCurve ? pvis[i].BcvElevation : vertices[i].Elevation;
                double tangentGrade = grades[i]; // 该直坡段对应的坡度

                // 即便 tangentEndS ≤ cursorS（重叠告警情况），仍记录该 segment，便于上层 UI 显示原始几何
                segments.Add(new ProfileSegment(
                    type: ProfileSegmentType.Tangent,
                    startStation: cursorS,
                    endStation: tangentEndS,
                    startElevation: cursorE,
                    endElevation: tangentEndE,
                    gradeIn: tangentGrade,
                    omega: 0,
                    length: tangentEndS - cursorS,
                    isSag: false,
                    pviIndex: -1));

                if (curAtPviHasCurve)
                {
                    var pi = pvis[i];
                    segments.Add(new ProfileSegment(
                        type: ProfileSegmentType.VerticalCurve,
                        startStation: pi.BcvStation,
                        endStation: pi.EcvStation,
                        startElevation: pi.BcvElevation,
                        endElevation: pi.EcvElevation,
                        gradeIn: pi.GradeIn,
                        omega: pi.Omega,
                        length: pi.CurveLength,
                        isSag: pi.IsSag,
                        pviIndex: i));

                    cursorS = pi.EcvStation;
                    cursorE = pi.EcvElevation;
                }
                else
                {
                    cursorS = vertices[i].Station;
                    cursorE = vertices[i].Elevation;
                }
            }

            return new ProfileFgResult(pvis, segments, warnings, errors);
        }
    }

    /// <summary>
    /// 单个 PVI 的派生几何信息（v1 全部预算好，UI 与命令直接读取，避免重复计算）。
    /// </summary>
    public readonly struct ProfilePviInfo
    {
        public int Index { get; }

        /// <summary>原始 PVI（引用，不拷贝）。</summary>
        public ProfileVertex Vertex { get; }

        /// <summary>前坡度（小数，下坡为负）。端点为 0。</summary>
        public double GradeIn { get; }

        /// <summary>后坡度。端点为 0。</summary>
        public double GradeOut { get; }

        /// <summary>代数差 ω = g_out - g_in（凸 &lt; 0，凹 &gt; 0）。端点为 0。</summary>
        public double Omega { get; }

        /// <summary>竖曲线长 L = |R · ω|。</summary>
        public double CurveLength { get; }

        public double BcvStation { get; }
        public double EcvStation { get; }
        public double BcvElevation { get; }
        public double EcvElevation { get; }

        /// <summary>是否为凹曲线（ω &gt; 0）。</summary>
        public bool IsSag { get; }

        /// <summary>是否为凸曲线（ω &lt; 0）。</summary>
        public bool IsCrest => Omega < 0;

        public ProfilePviInfo(
            int index,
            ProfileVertex vertex,
            double gradeIn,
            double gradeOut,
            double omega,
            double curveLength,
            double bcvStation,
            double ecvStation,
            double bcvElevation,
            double ecvElevation,
            bool isSag)
        {
            Index = index;
            Vertex = vertex;
            GradeIn = gradeIn;
            GradeOut = gradeOut;
            Omega = omega;
            CurveLength = curveLength;
            BcvStation = bcvStation;
            EcvStation = ecvStation;
            BcvElevation = bcvElevation;
            EcvElevation = ecvElevation;
            IsSag = isSag;
        }
    }

    /// <summary>
    /// 设计线分段类型。
    /// </summary>
    public enum ProfileSegmentType
    {
        Tangent,
        VerticalCurve,
    }

    /// <summary>
    /// 设计线一段：直坡 or 竖曲线。
    /// </summary>
    public readonly struct ProfileSegment
    {
        public ProfileSegmentType Type { get; }
        public double StartStation { get; }
        public double EndStation { get; }
        public double StartElevation { get; }
        public double EndElevation { get; }

        /// <summary>该段起点处坡度（直坡段 = 段坡度；竖曲线段 = g_in）。</summary>
        public double GradeIn { get; }

        /// <summary>竖曲线代数差 ω；直坡段为 0。</summary>
        public double Omega { get; }

        /// <summary>段长（直坡 = ΔS；竖曲线 = L）。</summary>
        public double Length { get; }

        /// <summary>竖曲线段是否为凹（IsSag = true，ω &gt; 0）。</summary>
        public bool IsSag { get; }

        /// <summary>竖曲线对应的 PVI 索引；直坡段为 -1。</summary>
        public int PviIndex { get; }

        public ProfileSegment(
            ProfileSegmentType type,
            double startStation,
            double endStation,
            double startElevation,
            double endElevation,
            double gradeIn,
            double omega,
            double length,
            bool isSag,
            int pviIndex)
        {
            Type = type;
            StartStation = startStation;
            EndStation = endStation;
            StartElevation = startElevation;
            EndElevation = endElevation;
            GradeIn = gradeIn;
            Omega = omega;
            Length = length;
            IsSag = isSag;
            PviIndex = pviIndex;
        }

        /// <summary>
        /// 在该段内按桩号求高程；超出 [StartStation, EndStation] 区间将被 clamp。
        /// </summary>
        public double ElevationAt(double station)
        {
            if (Length <= 1e-12) return StartElevation;
            double s = station;
            if (s <= StartStation) return StartElevation;
            if (s >= EndStation) return EndElevation;
            double x = s - StartStation;

            if (Type == ProfileSegmentType.Tangent)
            {
                return StartElevation + GradeIn * x;
            }
            // 竖曲线：y = h_BCV + g_in · x + ω · x² / (2L)
            return StartElevation + GradeIn * x + Omega * x * x / (2.0 * Length);
        }
    }

    /// <summary>
    /// <see cref="ProfileFgDesigner.Build"/> 的输出。
    /// </summary>
    public sealed class ProfileFgResult
    {
        public IReadOnlyList<ProfilePviInfo> Pvis { get; }
        public IReadOnlyList<ProfileSegment> Segments { get; }
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

        public ProfileFgResult(
            IReadOnlyList<ProfilePviInfo> pvis,
            IReadOnlyList<ProfileSegment> segments,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors)
        {
            Pvis = pvis ?? Array.Empty<ProfilePviInfo>();
            Segments = segments ?? Array.Empty<ProfileSegment>();
            Warnings = warnings ?? Array.Empty<string>();
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>
        /// 设计线总长（首末 PVI 桩号差）。Pvis ≤ 1 时返回 0。
        /// </summary>
        public double TotalLength
        {
            get
            {
                if (Pvis.Count < 2) return 0;
                return Pvis[Pvis.Count - 1].Vertex.Station - Pvis[0].Vertex.Station;
            }
        }

        /// <summary>
        /// 在任意桩号求设计线高程。超出 [首 PVI 桩号, 末 PVI 桩号] 区间按 clamp 取端点高程。
        /// 单 PVI 时返回该 PVI 高程；空 PVI 时返回 0。
        /// </summary>
        public double ElevationAt(double station)
        {
            if (Pvis.Count == 0) return 0;
            if (Pvis.Count == 1) return Pvis[0].Vertex.Elevation;

            double minS = Pvis[0].Vertex.Station;
            double maxS = Pvis[Pvis.Count - 1].Vertex.Station;
            if (station <= minS) return Pvis[0].Vertex.Elevation;
            if (station >= maxS) return Pvis[Pvis.Count - 1].Vertex.Elevation;

            // 二分定位段：Segments 按桩号升序，但重叠告警时可能存在区间倒序，
            // 退化为线性扫描以保证健壮性。
            for (int i = 0; i < Segments.Count; i++)
            {
                var seg = Segments[i];
                if (station >= seg.StartStation - 1e-9 && station <= seg.EndStation + 1e-9)
                {
                    return seg.ElevationAt(station);
                }
            }

            // 兜底：未命中（理论上不应发生），按相邻 PVI 线性插值
            for (int i = 1; i < Pvis.Count; i++)
            {
                var p0 = Pvis[i - 1].Vertex;
                var p1 = Pvis[i].Vertex;
                if (station >= p0.Station && station <= p1.Station)
                {
                    double t = (station - p0.Station) / (p1.Station - p0.Station);
                    return p0.Elevation + t * (p1.Elevation - p0.Elevation);
                }
            }
            return Pvis[0].Vertex.Elevation;
        }
    }
}
