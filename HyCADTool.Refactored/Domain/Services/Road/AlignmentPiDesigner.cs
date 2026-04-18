using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 导线法（PI 点法）平面线位几何生成器。纯 Domain 服务，不依赖 AutoCAD。
    ///
    /// 工程背景：
    /// - 市政道路设计院最常用的输入形式——拿到坐标表，按 PI（Point of Intersection，导线交点）顺序输入，
    ///   每个内部 PI 加上一个"圆曲线半径"+ 可选缓和曲线长度即可生成"直-缓-圆-缓-直"序列。
    /// - 对齐 Civil 3D 的"Create Alignment by PIs"，以及鸿业/纬地的"逐点导线法"。
    ///
    /// 输出与现有基础设施兼容：
    /// - 返回 <see cref="Polyline3D"/>（带 bulge），可直接经 <c>RoadGeometryBridge.ToAutoCadPolyline</c>
    ///   生成 AutoCAD 2D <c>Polyline</c>，接入 <c>RoadAlignmentService.ImportFromPolyline</c> 主链路。
    /// - 不做样条拟合；缓和曲线（回旋线）以"分段小圆弧 + bulge"近似输出（参数 <see cref="PiDesignOptions.SpiralSteps"/>）。
    ///
    /// 圆曲线几何公式（对内部 PI 点 P_i，0 &lt; i &lt; n）：
    /// <list type="bullet">
    ///   <item><c>t_in  = normalize(P_i - P_{i-1})</c></item>
    ///   <item><c>t_out = normalize(P_{i+1} - P_i)</c></item>
    ///   <item><c>turn  = atan2(t_in × t_out, t_in · t_out)</c> —— 带符号转角，左转正、右转负</item>
    ///   <item><c>T     = R · |tan(turn/2)|</c> —— 切线长度（无缓和曲线）</item>
    ///   <item><c>TC    = P_i - t_in  · T</c> —— 圆弧起点（切入点）</item>
    ///   <item><c>CT    = P_i + t_out · T</c> —— 圆弧终点（切出点）</item>
    ///   <item><c>bulge = tan(turn/4)</c> —— AutoCAD 弧段凸度约定，左转为正</item>
    /// </list>
    ///
    /// 缓和曲线（回旋线 / Clothoid，不对称支持）：
    /// <list type="bullet">
    ///   <item><c>τ_in  = Ls_in / (2R)</c>，<c>τ_out = Ls_out / (2R)</c> —— 缓和段端点偏角</item>
    ///   <item><c>p_in  = Ls_in² / (24R)</c>，<c>q_in = Ls_in/2 - Ls_in³/(240R²)</c> —— 圆心相对 PI 切线的内移与切线偏移</item>
    ///   <item>等效切线总长 <c>T_total = (R+p)·tan(turn/2) + q</c>（p,q 取入/出加权近似，工程精度足够）</item>
    ///   <item>缓和曲线被离散为 <see cref="PiDesignOptions.SpiralSteps"/> 段小圆弧；每段以局部曲率 = 弧长 / (R·Ls) 折算 bulge</item>
    /// </list>
    /// </summary>
    public static class AlignmentPiDesigner
    {
        /// <summary>
        /// 兼容旧调用：所有内部 PI 共用统一半径，无缓和曲线。
        /// 内部转换为 <see cref="PiElement"/> 列表后调用主重载。
        /// </summary>
        /// <param name="piPoints">导线点序列，≥ 2 个点；相邻点距离需 &gt; <paramref name="tolerance"/>。</param>
        /// <param name="radius">统一圆曲线半径，单位 m；传 0 或负数表示全折线不加圆角。</param>
        /// <param name="elevation">Polyline3D 的 Z 值（AutoCAD 2D Polyline 以 Elevation 统一存储）。</param>
        /// <param name="minTurnDeg">小于此角度的 PI 视为共线，不加圆角。默认 0.5°。</param>
        /// <param name="tolerance">点重合判定容差，默认 1e-6。</param>
        public static PiDesignResult Build(
            IReadOnlyList<Point2D> piPoints,
            double radius,
            double elevation = 0,
            double minTurnDeg = 0.5,
            double tolerance = 1e-6)
        {
            if (piPoints == null) throw new ArgumentNullException(nameof(piPoints));

            var elements = new PiElement[piPoints.Count];
            for (int i = 0; i < piPoints.Count; i++)
            {
                elements[i] = new PiElement(piPoints[i], radius, 0, 0, null);
            }

            var options = new PiDesignOptions
            {
                Elevation = elevation,
                MinTurnDeg = minTurnDeg,
                Tolerance = tolerance,
                FallbackRadius = 0,
            };
            return Build(elements, options);
        }

        /// <summary>
        /// 主入口：按"逐 PI 参数（半径 + 缓和曲线）"生成含弧段的 <see cref="Polyline3D"/>。
        ///
        /// 处理规则（逐 PI 评估）：
        /// - 共线 / 反向 / 转角小于 <see cref="PiDesignOptions.MinTurnDeg"/> ⇒ 保留原 PI 顶点，bulge = 0；
        /// - 半径 &lt;= 0（取 <see cref="PiElement.Radius"/>，回退 <see cref="PiDesignOptions.FallbackRadius"/>）⇒ 该 PI 走折线；
        /// - 切线长 T_total &gt; 任一相邻段长 ⇒ 按"半径过大降级"：先尝试丢弃缓和曲线、仅保留圆曲线；仍超长则跳过圆角并记录警告。
        /// </summary>
        public static PiDesignResult Build(
            IReadOnlyList<PiElement> elements,
            PiDesignOptions options = null)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            if (elements.Count < 2)
                throw new ArgumentException("至少需要 2 个 PI 点才能构造平面线位。", nameof(elements));

            var opts = options ?? new PiDesignOptions();
            double tolerance = opts.Tolerance;

            // 预检：相邻 PI 点不得重合
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i - 1].P.DistanceTo(elements[i].P) < tolerance)
                    throw new ArgumentException(
                        $"PI[{i - 1}] 与 PI[{i}] 重合（距离 < {tolerance:E2} m）。请删除重复点后重试。",
                        nameof(elements));
            }

            var poly = new Polyline3D(isClosed: false);
            var warnings = new List<string>();
            var perPi = new List<PiDiagnostic>();
            int curved = 0;
            int spiraled = 0;
            int skipped = 0;
            double minTurnRad = opts.MinTurnDeg * Math.PI / 180.0;

            // 首点
            poly.AddVertex(new Point3D(elements[0].P.X, elements[0].P.Y, opts.Elevation), 0);

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
                    poly.AddVertex(new Point3D(curr.X, curr.Y, opts.Elevation), 0);
                    perPi.Add(PiDiagnostic.Straight(i, curr, 0, 0, "向量退化"));
                    continue;
                }

                double cross = tInU.Cross(tOutU);
                double dot = tInU.Dot(tOutU);
                double turn = Math.Atan2(cross, dot); // (-π, π]

                // 共线 / 小转角 / 未配置半径 → 不加圆角
                if (radius <= 0 ||
                    Math.Abs(turn) < minTurnRad ||
                    Math.Abs(Math.Abs(turn) - Math.PI) < minTurnRad)
                {
                    poly.AddVertex(new Point3D(curr.X, curr.Y, opts.Elevation), 0);
                    string reason = radius <= 0 ? "无半径" : (Math.Abs(turn) < minTurnRad ? "共线" : "反向");
                    perPi.Add(PiDiagnostic.Straight(i, curr, turn, radius, reason));
                    continue;
                }

                double tanHalf = Math.Abs(Math.Tan(turn / 2.0));

                // 圆曲线切线长（无缓和曲线）
                double tArc = radius * tanHalf;

                // 缓和曲线参数（不对称：入侧用 lsIn、出侧用 lsOut）
                double tInTotal = tArc; // 入侧切线长
                double tOutTotal = tArc; // 出侧切线长
                bool useSpiral = lsIn > 0 || lsOut > 0;
                if (useSpiral)
                {
                    // p, q 公式（standard clothoid 近似，p = Ls²/(24R)，q = Ls/2 - Ls³/(240R²)）
                    double pIn = (lsIn * lsIn) / (24.0 * radius);
                    double qIn = lsIn / 2.0 - (lsIn * lsIn * lsIn) / (240.0 * radius * radius);
                    double pOut = (lsOut * lsOut) / (24.0 * radius);
                    double qOut = lsOut / 2.0 - (lsOut * lsOut * lsOut) / (240.0 * radius * radius);
                    tInTotal = (radius + pIn) * tanHalf + qIn;
                    tOutTotal = (radius + pOut) * tanHalf + qOut;
                }

                // 切线长容纳校验：若超长则降级（先丢缓和、再跳过圆角）
                bool fitsWithSpiral = tInTotal <= segInLen - tolerance && tOutTotal <= segOutLen - tolerance;
                if (!fitsWithSpiral && useSpiral)
                {
                    bool fitsArcOnly = tArc <= segInLen - tolerance && tArc <= segOutLen - tolerance;
                    if (fitsArcOnly)
                    {
                        warnings.Add(
                            $"PI[{i}]（{curr.X:F3},{curr.Y:F3}）缓和曲线超长，已降级为纯圆曲线（Ls_in={lsIn:F2}, Ls_out={lsOut:F2}）。");
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
                    double maxAllowed = Math.Min(segInLen, segOutLen);
                    warnings.Add(
                        $"PI[{i}]（{curr.X:F3},{curr.Y:F3}）切线长 T={Math.Max(tInTotal, tOutTotal):F3} m 超过邻段长度 "
                        + $"(入={segInLen:F3}, 出={segOutLen:F3})，跳过圆角。"
                        + $"建议半径 R ≤ {maxAllowed / Math.Max(1e-9, tanHalf):F1} m。");
                    poly.AddVertex(new Point3D(curr.X, curr.Y, opts.Elevation), 0);
                    perPi.Add(PiDiagnostic.Skipped(i, curr, turn, radius, tInTotal));
                    skipped++;
                    continue;
                }

                // TS / SC / CS / ST 四个关键点（无缓和曲线时 TS=SC=TC、CS=ST=CT）
                double tsX = curr.X - tInU.X * tInTotal;
                double tsY = curr.Y - tInU.Y * tInTotal;
                double stX = curr.X + tOutU.X * tOutTotal;
                double stY = curr.Y + tOutU.Y * tOutTotal;

                if (useSpiral)
                {
                    // 写入 TS（缓和曲线起点）
                    poly.AddVertex(new Point3D(tsX, tsY, opts.Elevation), 0);

                    // 计算 SC（圆曲线起点）= TS + tIn * (Ls - q?) 简化：SC = curr - tIn * (R*tanHalf - 0) 然后再前置 q 修正
                    // 工程实现：用"局部弧长比例 → bulge 序列"逼近，端点直接落到 SC
                    double scX = curr.X - tInU.X * tArc;
                    double scY = curr.Y - tInU.Y * tArc;
                    double csX = curr.X + tOutU.X * tArc;
                    double csY = curr.Y + tOutU.Y * tArc;

                    // 入侧缓和曲线：从 TS 到 SC，按 SpiralSteps 段离散
                    AppendSpiralVertices(
                        poly,
                        startX: tsX, startY: tsY,
                        endX: scX, endY: scY,
                        radiusStart: double.PositiveInfinity, // 直线端
                        radiusEnd: radius,
                        turnSign: Math.Sign(turn),
                        steps: opts.SpiralSteps,
                        elevation: opts.Elevation);

                    // 圆曲线段（一个 bulge 顶点）
                    double bulgeArc = Math.Tan((Math.Abs(turn) - (lsIn + lsOut) / (2.0 * radius)) / 4.0) * Math.Sign(turn);
                    poly.AddVertex(new Point3D(scX, scY, opts.Elevation), bulgeArc);

                    // 出侧缓和曲线：从 CS 到 ST
                    AppendSpiralVertices(
                        poly,
                        startX: csX, startY: csY,
                        endX: stX, endY: stY,
                        radiusStart: radius,
                        radiusEnd: double.PositiveInfinity,
                        turnSign: Math.Sign(turn),
                        steps: opts.SpiralSteps,
                        elevation: opts.Elevation);

                    // ST 写为终点（直线段衔接）
                    poly.AddVertex(new Point3D(stX, stY, opts.Elevation), 0);

                    spiraled++;
                    perPi.Add(PiDiagnostic.Spiraled(i, curr, turn, radius, tInTotal, lsIn, lsOut));
                }
                else
                {
                    // 纯圆曲线：与历史行为一致
                    double bulge = Math.Tan(turn / 4.0); // 带符号
                    poly.AddVertex(new Point3D(tsX, tsY, opts.Elevation), bulge);
                    poly.AddVertex(new Point3D(stX, stY, opts.Elevation), 0);
                    curved++;
                    perPi.Add(PiDiagnostic.Curved(i, curr, turn, radius, tArc));
                }
            }

            // 末点
            var last = elements[elements.Count - 1].P;
            poly.AddVertex(new Point3D(last.X, last.Y, opts.Elevation), 0);

            int internalPi = elements.Count - 2;
            int straight = internalPi - curved - spiraled - skipped;
            if (straight < 0) straight = 0;

            return new PiDesignResult(poly, curved, straight, skipped, spiraled, warnings, perPi);
        }

        /// <summary>
        /// 缓和曲线离散：把"直线端 ↔ 圆曲线端"之间的回旋线分成 <paramref name="steps"/> 段，
        /// 每段以局部小圆弧的 bulge 表达；近似公式遵循"曲率沿弧长线性变化"基本性质。
        ///
        /// 简化策略（工程精度足够）：
        /// 1. 在端点连线（弦）上等距插入 steps - 1 个中间点；
        /// 2. 第 k 段（k = 1..steps）的"局部曲率半径"取 R / (k / steps)（线性插值）；
        ///    bulge_k = tan(arcAngle_k / 4)，其中 arcAngle_k ≈ chord_k / R_local，符号随 turnSign。
        ///
        /// 这是 v1 近似实现（非严格 Fresnel 积分）；P1.b 可替换为 Fresnel 精确解或 Clothoid Curve 拟合。
        /// </summary>
        private static void AppendSpiralVertices(
            Polyline3D poly,
            double startX, double startY,
            double endX, double endY,
            double radiusStart, double radiusEnd,
            int turnSign,
            int steps,
            double elevation)
        {
            if (steps < 1) steps = 1;

            // 端点弦
            double dx = endX - startX;
            double dy = endY - startY;
            double chordTotal = Math.Sqrt(dx * dx + dy * dy);
            if (chordTotal < 1e-9) return;

            for (int k = 1; k < steps; k++)
            {
                double t = (double)k / steps;
                double mx = startX + dx * t;
                double my = startY + dy * t;

                double rPrev = LerpRadius(radiusStart, radiusEnd, (k - 1) / (double)steps);
                double rCurr = LerpRadius(radiusStart, radiusEnd, t);
                double rAvg = 2.0 / (1.0 / rPrev + 1.0 / rCurr); // 调和平均，避免无穷大
                double chordSeg = chordTotal / steps;
                double arcAngle = chordSeg / Math.Max(1e-9, rAvg);
                double bulge = Math.Tan(arcAngle / 4.0) * turnSign;

                poly.AddVertex(new Point3D(mx, my, elevation), bulge);
            }
        }

        private static double LerpRadius(double r0, double r1, double t)
        {
            // 沿弧长线性变化曲率 κ；半径 R = 1/κ。
            double k0 = double.IsPositiveInfinity(r0) ? 0 : 1.0 / r0;
            double k1 = double.IsPositiveInfinity(r1) ? 0 : 1.0 / r1;
            double k = k0 + (k1 - k0) * t;
            return k <= 1e-12 ? double.PositiveInfinity : 1.0 / k;
        }
    }

    /// <summary>
    /// 单个 PI 点的输入参数：坐标 + 圆曲线半径 + 入/出侧缓和曲线长度（首尾点的参数被忽略）。
    /// </summary>
    public readonly struct PiElement
    {
        /// <summary>PI 平面坐标。</summary>
        public Point2D P { get; }

        /// <summary>圆曲线半径，米；&lt;=0 表示该 PI 走折线（再回退 <see cref="PiDesignOptions.FallbackRadius"/>）。</summary>
        public double Radius { get; }

        /// <summary>入侧缓和曲线长度 Ls_in，米；0 表示无缓和曲线。</summary>
        public double SpiralIn { get; }

        /// <summary>出侧缓和曲线长度 Ls_out，米；0 表示无缓和曲线。</summary>
        public double SpiralOut { get; }

        /// <summary>可选标签，仅用于日志/CSV 回显，不参与几何。</summary>
        public string Tag { get; }

        public PiElement(Point2D p, double radius = 0, double spiralIn = 0, double spiralOut = 0, string tag = null)
        {
            P = p;
            Radius = radius;
            SpiralIn = spiralIn;
            SpiralOut = spiralOut;
            Tag = tag;
        }
    }

    /// <summary>
    /// <see cref="AlignmentPiDesigner.Build(IReadOnlyList{PiElement}, PiDesignOptions)"/> 的全局选项。
    /// </summary>
    public sealed class PiDesignOptions
    {
        /// <summary>共线判定阈值（度）。</summary>
        public double MinTurnDeg { get; set; } = 0.5;

        /// <summary>点重合容差（米）。</summary>
        public double Tolerance { get; set; } = 1e-6;

        /// <summary>Polyline3D 的 Z 值。</summary>
        public double Elevation { get; set; } = 0;

        /// <summary>当 <see cref="PiElement.Radius"/> &lt;= 0 时的回退半径；&lt;=0 表示该 PI 走折线。</summary>
        public double FallbackRadius { get; set; } = 0;

        /// <summary>缓和曲线离散段数（默认 12，工程精度足够）。</summary>
        public int SpiralSteps { get; set; } = 12;
    }

    /// <summary>
    /// 单个内部 PI 的诊断条目，便于命令行/UI 表格回显。
    /// </summary>
    public readonly struct PiDiagnostic
    {
        public int Index { get; }
        public Point2D P { get; }
        public double TurnRad { get; }
        public double Radius { get; }
        public double TangentLen { get; }
        public double SpiralIn { get; }
        public double SpiralOut { get; }

        /// <summary>
        /// 状态：Straight（保留为折线）/ Curved（仅圆曲线）/ Spiraled（直-缓-圆-缓-直）/ Skipped（无法容纳被跳过）。
        /// </summary>
        public PiDiagnosticStatus Status { get; }

        /// <summary>状态补充文本（如"共线/反向/无半径/向量退化"）。</summary>
        public string Note { get; }

        private PiDiagnostic(
            int index, Point2D p, double turnRad, double radius,
            double tangentLen, double spiralIn, double spiralOut,
            PiDiagnosticStatus status, string note)
        {
            Index = index;
            P = p;
            TurnRad = turnRad;
            Radius = radius;
            TangentLen = tangentLen;
            SpiralIn = spiralIn;
            SpiralOut = spiralOut;
            Status = status;
            Note = note ?? string.Empty;
        }

        public static PiDiagnostic Straight(int i, Point2D p, double turn, double radius, string note)
            => new PiDiagnostic(i, p, turn, radius, 0, 0, 0, PiDiagnosticStatus.Straight, note);

        public static PiDiagnostic Curved(int i, Point2D p, double turn, double radius, double t)
            => new PiDiagnostic(i, p, turn, radius, t, 0, 0, PiDiagnosticStatus.Curved, null);

        public static PiDiagnostic Spiraled(int i, Point2D p, double turn, double radius, double t, double lsIn, double lsOut)
            => new PiDiagnostic(i, p, turn, radius, t, lsIn, lsOut, PiDiagnosticStatus.Spiraled, null);

        public static PiDiagnostic Skipped(int i, Point2D p, double turn, double radius, double t)
            => new PiDiagnostic(i, p, turn, radius, t, 0, 0, PiDiagnosticStatus.Skipped, "切线长超限");
    }

    public enum PiDiagnosticStatus
    {
        Straight = 0,
        Curved = 1,
        Spiraled = 2,
        Skipped = 3,
    }

    /// <summary>
    /// <see cref="AlignmentPiDesigner.Build(IReadOnlyList{PiElement}, PiDesignOptions)"/> 的结果与诊断。
    /// 命令层据此决定命令行反馈（例如"3 段圆角成功 / 1 段因半径过大被跳过"）。
    /// </summary>
    public readonly struct PiDesignResult
    {
        public Polyline3D Polyline { get; }

        /// <summary>成功加圆角的内部 PI 数（不含缓和曲线）。</summary>
        public int CurvedPiCount { get; }

        /// <summary>判定为共线或小转角、主动保留为折线的 PI 数（不算失败）。</summary>
        public int StraightPiCount { get; }

        /// <summary>因半径过大 / 段长不足、被迫跳过圆角的 PI 数（告警项）。</summary>
        public int SkippedCount { get; }

        /// <summary>成功加缓和曲线（直-缓-圆-缓-直）的内部 PI 数。</summary>
        public int SpiraledPiCount { get; }

        /// <summary>诊断信息（通常对应 <see cref="SkippedCount"/> 与缓和曲线降级条目）。</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>每个内部 PI 的诊断（顺序与输入 elements[1..n-2] 一致）。</summary>
        public IReadOnlyList<PiDiagnostic> PerPi { get; }

        public PiDesignResult(
            Polyline3D polyline,
            int curvedPiCount,
            int straightPiCount,
            int skippedCount,
            int spiraledPiCount,
            IReadOnlyList<string> warnings,
            IReadOnlyList<PiDiagnostic> perPi)
        {
            Polyline = polyline ?? throw new ArgumentNullException(nameof(polyline));
            CurvedPiCount = curvedPiCount;
            StraightPiCount = straightPiCount;
            SkippedCount = skippedCount;
            SpiraledPiCount = spiraledPiCount;
            Warnings = warnings ?? Array.Empty<string>();
            PerPi = perPi ?? Array.Empty<PiDiagnostic>();
        }
    }
}
