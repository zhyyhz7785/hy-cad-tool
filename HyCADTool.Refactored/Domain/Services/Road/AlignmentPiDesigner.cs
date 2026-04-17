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
    ///   每个内部 PI 加上一个"圆曲线半径"即可生成标准的"直-圆-直"序列。
    /// - 对齐 Civil 3D 的"Create Alignment by PIs"，以及鸿业/纬地的"逐点导线法"。
    ///
    /// 输出与现有基础设施兼容：
    /// - 返回 <see cref="Polyline3D"/>（带 bulge），可直接经 <c>RoadGeometryBridge.ToAutoCadPolyline</c>
    ///   生成 AutoCAD 2D <c>Polyline</c>，接入 <c>RoadAlignmentService.ImportFromPolyline</c> 主链路。
    /// - 不做样条拟合、不做缓和曲线（回旋线）—— P1 首版只处理圆曲线；缓和曲线 P1.b 再并入。
    ///
    /// 几何公式（对内部 PI 点 P_i，0 &lt; i &lt; n）：
    /// <list type="bullet">
    ///   <item><c>t_in  = normalize(P_i - P_{i-1})</c></item>
    ///   <item><c>t_out = normalize(P_{i+1} - P_i)</c></item>
    ///   <item><c>turn  = atan2(t_in × t_out, t_in · t_out)</c> —— 带符号转角，左转正、右转负</item>
    ///   <item><c>T     = R · |tan(turn/2)|</c> —— 切线长度</item>
    ///   <item><c>TC    = P_i - t_in  · T</c> —— 圆弧起点（切入点）</item>
    ///   <item><c>CT    = P_i + t_out · T</c> —— 圆弧终点（切出点）</item>
    ///   <item><c>bulge = tan(turn/4)</c> —— AutoCAD 弧段凸度约定，左转为正</item>
    /// </list>
    /// </summary>
    public static class AlignmentPiDesigner
    {
        /// <summary>
        /// 按"统一圆曲线半径"从 PI 点序列构造含弧段的 <see cref="Polyline3D"/>。
        ///
        /// 处理规则（逐 PI 评估）：
        /// - 共线 / 反向 / 转角小于 <paramref name="minTurnDeg"/> ⇒ 保留原 PI 顶点，bulge = 0（视为折线拐点）；
        /// - 半径 &lt;= 0 ⇒ 全程视为折线，每个 PI 顶点 bulge = 0；
        /// - 切线长 T &gt; 任一相邻段长的一半 ⇒ 跳过圆角（保留 PI 顶点），并记录警告。
        ///
        /// 返回的 <see cref="Polyline3D"/> 顶点数 = 起点 + Σ(每个 PI 贡献 1 或 2 顶点) + 终点。
        /// - 加圆角的 PI 贡献 2 个顶点（TC + CT），首个顶点挂 bulge；
        /// - 其他 PI 贡献 1 个顶点（原位），bulge = 0。
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
            if (piPoints.Count < 2)
                throw new ArgumentException("至少需要 2 个 PI 点才能构造平面线位。", nameof(piPoints));

            // 预检：相邻点不得重合
            for (int i = 1; i < piPoints.Count; i++)
            {
                if (piPoints[i - 1].DistanceTo(piPoints[i]) < tolerance)
                    throw new ArgumentException(
                        $"PI[{i - 1}] 与 PI[{i}] 重合（距离 < {tolerance:E2} m）。请删除重复点后重试。",
                        nameof(piPoints));
            }

            var poly = new Polyline3D(isClosed: false);
            var warnings = new List<string>();
            int curved = 0;
            int skipped = 0;
            double minTurnRad = minTurnDeg * Math.PI / 180.0;

            // 首点
            poly.AddVertex(new Point3D(piPoints[0].X, piPoints[0].Y, elevation), 0);

            for (int i = 1; i < piPoints.Count - 1; i++)
            {
                var prev = piPoints[i - 1];
                var curr = piPoints[i];
                var next = piPoints[i + 1];

                var tIn = new Vector2D(curr.X - prev.X, curr.Y - prev.Y);
                var tOut = new Vector2D(next.X - curr.X, next.Y - curr.Y);
                double segInLen = tIn.Length;
                double segOutLen = tOut.Length;

                if (!tIn.TryNormalize(out var tInU, tolerance) ||
                    !tOut.TryNormalize(out var tOutU, tolerance))
                {
                    // 理论上预检挡住了，这里是兜底
                    poly.AddVertex(new Point3D(curr.X, curr.Y, elevation), 0);
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
                    poly.AddVertex(new Point3D(curr.X, curr.Y, elevation), 0);
                    continue;
                }

                double tangentLen = radius * Math.Abs(Math.Tan(turn / 2.0));

                // 相邻段过短无法容纳切线长：跳过圆角
                if (tangentLen >= segInLen - tolerance || tangentLen >= segOutLen - tolerance)
                {
                    double maxAllowed = Math.Min(segInLen, segOutLen);
                    warnings.Add(
                        $"PI[{i}]（{curr.X:F3},{curr.Y:F3}）切线长 T={tangentLen:F3} m 超过邻段长度 "
                        + $"(入={segInLen:F3}, 出={segOutLen:F3})，跳过圆角。"
                        + $"建议半径 R ≤ {maxAllowed / Math.Abs(Math.Tan(turn / 2.0)):F1} m。");
                    poly.AddVertex(new Point3D(curr.X, curr.Y, elevation), 0);
                    skipped++;
                    continue;
                }

                // 圆弧起点 TC = PI - tIn * T
                double tcX = curr.X - tInU.X * tangentLen;
                double tcY = curr.Y - tInU.Y * tangentLen;
                // 圆弧终点 CT = PI + tOut * T
                double ctX = curr.X + tOutU.X * tangentLen;
                double ctY = curr.Y + tOutU.Y * tangentLen;

                double bulge = Math.Tan(turn / 4.0); // 带符号，左转为正

                poly.AddVertex(new Point3D(tcX, tcY, elevation), bulge);
                poly.AddVertex(new Point3D(ctX, ctY, elevation), 0);
                curved++;
            }

            // 末点
            var last = piPoints[piPoints.Count - 1];
            poly.AddVertex(new Point3D(last.X, last.Y, elevation), 0);

            int straight = piPoints.Count - 2 - curved - skipped; // 内部 PI 里未加圆角数（不含跳过的）
            if (straight < 0) straight = 0;

            return new PiDesignResult(poly, curved, straight, skipped, warnings);
        }
    }

    /// <summary>
    /// <see cref="AlignmentPiDesigner.Build"/> 的结果与诊断。
    /// 命令层据此决定命令行反馈（例如"3 段圆角成功 / 1 段因半径过大被跳过"）。
    /// </summary>
    public readonly struct PiDesignResult
    {
        public Polyline3D Polyline { get; }

        /// <summary>成功加圆角的内部 PI 数。</summary>
        public int CurvedPiCount { get; }

        /// <summary>判定为共线或小转角、主动保留为折线的 PI 数（不算失败）。</summary>
        public int StraightPiCount { get; }

        /// <summary>因半径过大 / 段长不足、被迫跳过圆角的 PI 数（告警项）。</summary>
        public int SkippedCount { get; }

        /// <summary>诊断信息（通常对应 <see cref="SkippedCount"/> 条目）。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public PiDesignResult(
            Polyline3D polyline,
            int curvedPiCount,
            int straightPiCount,
            int skippedCount,
            IReadOnlyList<string> warnings)
        {
            Polyline = polyline ?? throw new ArgumentNullException(nameof(polyline));
            CurvedPiCount = curvedPiCount;
            StraightPiCount = straightPiCount;
            SkippedCount = skippedCount;
            Warnings = warnings ?? Array.Empty<string>();
        }
    }
}
