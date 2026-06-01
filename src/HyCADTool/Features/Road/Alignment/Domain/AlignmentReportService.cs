using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Features.Road.PlanAlignment.Domain;

namespace HyCADTool.Features.Road.PlanAlignment.Domain
{
    /// <summary>
    /// Alignment 报表生成服务（纯 Domain，无 I/O）。
    ///
    /// 职责：为一条 <see cref="Alignment"/> 产出两种工程通用的平面线位报表：
    /// <list type="bullet">
    ///   <item><b>交点表 / PI 表</b>（<see cref="BuildPiTableCsv"/>）：按 PI 聚合，每行一个交点，
    ///     列出 坐标 / 半径 / 缓和曲线长 / 切线长 / 曲线长 / 转向 / 桩号。对标鸿业"交点坐标表"。</item>
    ///   <item><b>复测表 / 几何框架表</b>（<see cref="BuildFrameTableCsv"/>）：按桩号递增把 BP / BC / EC / TS / SC /
    ///     CS / ST / EP 依次列出，配坐标 / 段长 / 类型 / 半径。对标 Civil 3D "Alignment Entities" 与
    ///     鸿业"中桩坐标复测表"。</item>
    /// </list>
    ///
    /// 设计约束：
    /// <list type="bullet">
    ///   <item>不碰 I/O：输出字符串即可，文件写盘由命令层负责（UTF-8 BOM + CSV 扩展，让 Excel 中文不乱码）；</item>
    ///   <item>不依赖 AutoCAD / WPF：便于单元测试；</item>
    ///   <item>数字格式全部使用 <see cref="CultureInfo.InvariantCulture"/>：避免 "," 与千分位在中文系统里把 CSV 撕碎；</item>
    ///   <item>桩号文字走 <see cref="AlignmentStationBreakdown.FormatStation"/>，与图面标注一致。</item>
    /// </list>
    /// </summary>
    public static class AlignmentReportService
    {
        /// <summary>CSV 规定：字段含 `,` `"` 或换行时整段用双引号包起来，内部的 `"` 双写。</summary>
        private static string Csv(string s)
        {
            if (s == null) return string.Empty;
            bool needQuote = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string F(double v, int decimals)
            => v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        /// <summary>
        /// 生成"交点表 / PI 表"CSV 字符串。每个内部 PI 一行 + 首尾 BP / EP 各一行。
        ///
        /// 列：
        /// <code>PI#, Tag, X, Y, Radius(m), Ls_in(m), Ls_out(m), Turn(°)(带+/-), Tangent T(m), Arc Ly(m), Station</code>
        /// 其中：
        /// <list type="bullet">
        ///   <item>Turn：正 = 右转（turn&lt;0 即 Math.Atan2 约定下的顺时针），负 = 左转；首尾 PI 留空；</item>
        ///   <item>Tangent T：切线长（含缓和曲线时 = (R+p)·|tan(Δ/2)| + q）；从 breakdown 里反推更稳，这里用
        ///     "该 PI 所在的缓和 / 圆弧首段 startPoint → PI 点"的距离，兜底为 <see cref="double.NaN"/>；</item>
        ///   <item>Arc Ly：仅圆弧部分长度；只在"圆 / 缓圆缓"PI 上填。</item>
        ///   <item>Station：该 PI 对应的桩号（BP → 起桩、EP → 止桩、内部 PI → 取 TS/BC 对应桩号，若 PI 被保留为直线折点则取 PI 点自身桩号）。</item>
        /// </list>
        /// </summary>
        public static string BuildPiTableCsv(Alignment alignment, PiDesignOptions options = null)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
                throw new InvalidOperationException("Alignment 缺少 PI 快照（Source.PiElements），无法生成 PI 表。");

            var elements = alignment.Source.PiElements
                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                .ToList();

            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation, options, alignment.StationEquations);

            // 预索引：按 PiIndex 收集相关段与几何点
            var segsByPi = breakdown.Segments
                .Where(s => s.PiIndex >= 0)
                .GroupBy(s => s.PiIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            var pointsByPi = breakdown.GeometryPoints
                .GroupBy(p => p.PiIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sb = new StringBuilder();
            sb.AppendLine("PI#,Tag,X,Y,Radius(m),Ls_in(m),Ls_out(m),Turn(deg),Tangent T(m),Arc Ly(m),Station");

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                string tag = Csv(e.Tag ?? (i == 0 ? "BP" : (i == elements.Count - 1 ? "EP" : string.Empty)));

                string turn = string.Empty;
                string tangent = string.Empty;
                string arc = string.Empty;
                string station = string.Empty;

                if (i == 0)
                {
                    station = Csv(AlignmentStationBreakdown.FormatStation(breakdown.StartStationM));
                }
                else if (i == elements.Count - 1)
                {
                    station = Csv(AlignmentStationBreakdown.FormatStation(breakdown.EndStationM));
                }
                else
                {
                    // 计算转角（用相邻 PI 的切线向量），与 breakdown 判别一致
                    var prev = elements[i - 1].P;
                    var curr = elements[i].P;
                    var next = elements[i + 1].P;
                    var tIn = new Vector2D(curr.X - prev.X, curr.Y - prev.Y);
                    var tOut = new Vector2D(next.X - curr.X, next.Y - curr.Y);
                    if (tIn.TryNormalize(out var tInU) && tOut.TryNormalize(out var tOutU))
                    {
                        double turnRad = Math.Atan2(tInU.Cross(tOutU), tInU.Dot(tOutU));
                        turn = F(turnRad * 180.0 / Math.PI, 3);
                    }

                    // 该 PI 对应的段集合：最多 Spiral-Arc-Spiral 三段
                    if (segsByPi.TryGetValue(i, out var segs) && segs.Count > 0)
                    {
                        var arcSeg = segs.FirstOrDefault(s => s.Kind == SegmentKind.Arc);
                        if (arcSeg.Kind == SegmentKind.Arc)
                            arc = F(arcSeg.LengthM, 3);

                        // 切线长：第一段起点到 PI 点（沿入向）；用 startPoint 与 PI 的距离
                        var firstSeg = segs.First();
                        double dx = e.P.X - firstSeg.StartPoint.X;
                        double dy = e.P.Y - firstSeg.StartPoint.Y;
                        tangent = F(Math.Sqrt(dx * dx + dy * dy), 3);
                    }

                    // 桩号：该 PI 的第一个几何点（TS / BC / PI）
                    if (pointsByPi.TryGetValue(i, out var pts) && pts.Count > 0)
                    {
                        var firstPt = pts.First();
                        station = Csv(AlignmentStationBreakdown.FormatStation(firstPt.StationM));
                    }
                }

                sb.Append(i).Append(',')
                  .Append(tag).Append(',')
                  .Append(F(e.P.X, 4)).Append(',')
                  .Append(F(e.P.Y, 4)).Append(',')
                  .Append(e.Radius > 0 ? F(e.Radius, 3) : string.Empty).Append(',')
                  .Append(e.SpiralIn > 0 ? F(e.SpiralIn, 3) : string.Empty).Append(',')
                  .Append(e.SpiralOut > 0 ? F(e.SpiralOut, 3) : string.Empty).Append(',')
                  .Append(turn).Append(',')
                  .Append(tangent).Append(',')
                  .Append(arc).Append(',')
                  .Append(station);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成"复测表 / 几何框架表"CSV 字符串。按桩号递增列出每个几何点（BP, BC/EC 或 TS/SC/CS/ST, PI, EP）。
        ///
        /// 列：
        /// <code>Kind, PI#, Station, X, Y, ToNext Length(m), ToNext Kind, ToNext Radius(m)</code>
        /// 其中 ToNext 指"从本点到下一几何点之间的段"；最后一行（EP）的 ToNext 留空。
        /// </summary>
        public static string BuildFrameTableCsv(Alignment alignment, PiDesignOptions options = null)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
                throw new InvalidOperationException("Alignment 缺少 PI 快照（Source.PiElements），无法生成几何框架表。");

            var elements = alignment.Source.PiElements
                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                .ToList();

            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation, options, alignment.StationEquations);

            var sb = new StringBuilder();
            sb.AppendLine("Kind,PI#,Station,X,Y,ToNext Length(m),ToNext Kind,ToNext Radius(m)");

            var points = breakdown.GeometryPoints;
            var segs = breakdown.Segments;

            // 通过"段 startStation == gp.station"把 gp 与其后继段对齐
            const double sTol = 1e-6;
            for (int i = 0; i < points.Count; i++)
            {
                var gp = points[i];
                string toNextLen = string.Empty;
                string toNextKind = string.Empty;
                string toNextRadius = string.Empty;

                var following = segs.FirstOrDefault(s => Math.Abs(s.StationStartM - gp.StationM) < sTol);
                if (following.LengthM > 0)
                {
                    toNextLen = F(following.LengthM, 3);
                    toNextKind = following.KindLabel();
                    if (!double.IsNaN(following.Radius))
                        toNextRadius = F(following.Radius, 3);
                }

                sb.Append(gp.Kind.ToString()).Append(',')
                  .Append(gp.PiIndex).Append(',')
                  .Append(Csv(AlignmentStationBreakdown.FormatStation(gp.StationM))).Append(',')
                  .Append(F(gp.Point.X, 4)).Append(',')
                  .Append(F(gp.Point.Y, 4)).Append(',')
                  .Append(toNextLen).Append(',')
                  .Append(toNextKind).Append(',')
                  .Append(toNextRadius);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// UTF-8 with BOM 编码的 CSV 字节数组：Excel 打开中文不乱码。
        /// 使用方：文件流 <c>File.WriteAllBytes(path, ...)</c> 即可直接出盘。
        /// </summary>
        public static byte[] ToUtf8BomBytes(string csvText)
        {
            if (csvText == null) csvText = string.Empty;
            var encoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            // UTF8Encoding.GetBytes() 本身不会前置 BOM，必须手动拼接 Preamble。
            var preamble = encoder.GetPreamble();
            var payload = encoder.GetBytes(csvText);
            var result = new byte[preamble.Length + payload.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(payload, 0, result, preamble.Length, payload.Length);
            return result;
        }
    }
}
