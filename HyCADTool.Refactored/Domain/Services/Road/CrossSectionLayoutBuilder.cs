using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// <see cref="CrossSectionLayout"/>（条带模型）↔ <see cref="Template"/>（点+段模型）
    /// ↔ <see cref="CrossSectionFigure"/>（绘图指令）三者互转。
    ///
    /// 设计要点：
    /// <list type="bullet">
    ///   <item><see cref="ToTemplate"/>：写入 <c>RoadDesign.Templates</c> 的 Domain 形态；v1 仍按"每条带 1 个外缘点"
    ///       的简化结构持久化，路牙等扩展信息保存在 Layout VO 中（Template 仅承载主轮廓拓扑）。</item>
    ///   <item><see cref="ToFigure"/>：v2 委托给 <see cref="CrossSectionGeometryGenerator"/>，支持路牙凸起、
    ///       抛物线/折线路拱、坡型等扩展几何。</item>
    ///   <item><see cref="FromTemplate"/>：反向拼 Band。对非单调横偏移或零点缺失的旧 Template 返回 null。</item>
    /// </list>
    ///
    /// 横坡符号约定（内高外低）：
    /// <list type="bullet">
    ///   <item>机动车道 / 非机动车道 / 人行道：<c>CrossSlopePct &gt; 0</c> 表示"向外下降 CrossSlopePct %"。</item>
    ///   <item>缘石 / 中央分隔带 / 绿化带：视为水平段，<c>CrossSlopePct</c> 被忽略（即使传了也不推 y）。</item>
    /// </list>
    /// </summary>
    public static class CrossSectionLayoutBuilder
    {
        private const string ElevationDiffKey = "ElevationDiff";
        private const string InnerElevationDiffKey = "InnerElevationDiff";
        /// <summary>
        /// 比较两条带"从中心向外扫描"时 y 偏移时的符号：
        /// 返回 -1 表示外侧 y 比内侧 y 低（路面类）；返回 0 表示水平。
        ///
        /// 直接转发给 <see cref="CrossSectionGeometryGenerator.SurfaceSlopeSign"/>，
        /// 保持本类内部 (ToTemplate/FromTemplate) 与 Generator 行为一致。
        /// </summary>
        private static int SlopeSign(TemplateComponentKind kind)
            => CrossSectionGeometryGenerator.SurfaceSlopeSign(kind);

        // ============================================================================
        //  Layout → Template（持久化）
        // ============================================================================

        /// <summary>
        /// 把条带布置转换为 Domain <see cref="Template"/>。
        ///
        /// 点序：从最左 → 中分带左边缘 → 中分带右边缘 → 最右，
        /// 每段的 <see cref="TemplateComponent.Kind"/> 记录功能类型。
        ///
        /// <paramref name="templateId"/>：沿用旧 Id 做"增量保存"；null 则由 <see cref="Template"/>
        /// 默认构造随机一个 Guid。
        ///
        /// <para>
        /// v2 备注：路牙、抛物线路拱等扩展信息暂不写入 Template（Template 仅承载主轮廓拓扑），
        /// 完整数据由上层 <see cref="CrossSectionLayout"/> 保存为独立 JSON。
        /// </para>
        /// <para>
        /// M7+ 备注：<see cref="CrossSectionBand.StructureScheme"/>（面/基/垫结构层方案）在 rCs
        /// 面板 v2 中作为内存字段存在，**本 Phase 1 刻意不落盘到 Template / Template JSON**，
        /// 以保证旧 Template 序列化内容 bit-identical；结构层持久化由 Phase 2 的 JSON Schema
        /// 扩展统一落地（参见 042 索引与 0XX 规划文档）。
        /// </para>
        /// </summary>
        public static Template ToTemplate(CrossSectionLayout layout, Guid? templateId = null, string name = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            var tpl = new Template
            {
                Name = string.IsNullOrWhiteSpace(name) ? (string.IsNullOrEmpty(layout.Title) ? "Template" : layout.Title) : name
            };
            if (templateId.HasValue) tpl.Id = templateId.Value;

            // 1) 收集"左→右"顺序的点，按公式 y' = y + w·(i/100)·slopeSign 逐段推进
            //    同时顺序发射 Components
            var components = new List<TemplateComponent>();

            double xLeftCenter = -layout.CenterMedianWidth / 2.0;
            double xRightCenter = +layout.CenterMedianWidth / 2.0;

            // ---- 左半：从中心向外扫描 ----
            double xL = xLeftCenter;
            double yL = 0;
            // 先收集左半所有点（顺序"最外 … 中心"），最后反转
            var leftOuterToInner = new List<TemplatePoint>();
            // 中心点（中分带左边缘）先记下，留到后面统一合并
            var leftInnerAnchor = MakePoint(xL, yL, "中心左");
            int leftKindCount = layout.LeftBands.Count;
            var leftSegKinds = new List<(TemplateComponentKind Kind, string Name)>(leftKindCount);

            // 从内向外遍历，逐步更新 (xL, yL)；每段对应一个"外缘点"。
            // 备注：InnerElevationDiff / ElevationDiff 刻意不并入 yL，
            //      Template 保持"无跳变的 slope 积分"持久化形态，
            //      高差跳变仅通过 ExtendedData 承载，
            //      几何消费端（GenerateStrips）再加回来。这样：
            //      a) ToTemplate ↔ FromTemplate 能稳定 roundtrip（slope 由 dy/w 反推，不被跳变干扰）；
            //      b) 旧 Template JSON（无新键）加载出 InnerElevationDiff = 0，行为与旧版 bit-identical。
            foreach (var band in layout.LeftBands)
            {
                double sign = SlopeSign(band.Kind);
                double dx = -band.Width;
                double dy = band.Width * (band.CrossSlopePct / 100.0) * sign;
                xL += dx;
                yL += dy;
                leftOuterToInner.Add(MakePoint(xL, yL, band.Name + "外缘",
                                               band.ElevationDiff, band.InnerElevationDiff));
                leftSegKinds.Add((band.Kind, band.Name));
            }
            // 反转成"最外 → 中心"顺序
            leftOuterToInner.Reverse();
            leftSegKinds.Reverse();

            // ---- 右半：从中心向外扫描 ----
            double xR = xRightCenter;
            double yR = 0;
            var rightInnerAnchor = MakePoint(xR, yR, "中心右");
            var rightOuterList = new List<TemplatePoint>(layout.RightBands.Count);
            var rightSegKinds = new List<(TemplateComponentKind Kind, string Name)>(layout.RightBands.Count);
            foreach (var band in layout.RightBands)
            {
                double sign = SlopeSign(band.Kind);
                double dx = +band.Width;
                double dy = band.Width * (band.CrossSlopePct / 100.0) * sign;
                xR += dx;
                yR += dy;
                rightOuterList.Add(MakePoint(xR, yR, band.Name + "外缘",
                                             band.ElevationDiff, band.InnerElevationDiff));
                rightSegKinds.Add((band.Kind, band.Name));
            }

            // 2) 合并点序：左外缘 … 左中心 [中分带] 右中心 … 右外缘
            tpl.Points.AddRange(leftOuterToInner);
            tpl.Points.Add(leftInnerAnchor);
            if (layout.CenterMedianWidth > 0)
            {
                tpl.Points.Add(rightInnerAnchor);
            }
            else
            {
                // 无中分带时，左中心和右中心重合 (0,0)；为避免重复点，仅保留一个（leftInnerAnchor 已在）
                // 但后续 Components 仍需指向 rightInnerAnchor 的"身份"，这里把 rightInnerAnchor
                // 的 Id 替换为 leftInnerAnchor 的 Id。
                rightInnerAnchor.Id = leftInnerAnchor.Id;
                rightInnerAnchor.HorizontalOffset = leftInnerAnchor.HorizontalOffset;
                rightInnerAnchor.VerticalOffset = leftInnerAnchor.VerticalOffset;
            }
            tpl.Points.AddRange(rightOuterList);

            // 3) 发射 Components
            //    左半段：每对相邻点 (左侧外) → (下一个更靠中心的点)，Kind 对应 leftSegKinds 索引
            //    顺序：最外 → 中心，段 i 连接 tpl.Points[i] 和 tpl.Points[i+1]
            int leftBandCount = layout.LeftBands.Count;
            for (int i = 0; i < leftBandCount; i++)
            {
                var start = tpl.Points[i];
                var end = tpl.Points[i + 1];
                var sk = leftSegKinds[i];
                components.Add(new TemplateComponent
                {
                    StartPointId = start.Id,
                    EndPointId = end.Id,
                    Kind = sk.Kind,
                });
            }

            //    中分带段（仅当存在）
            if (layout.CenterMedianWidth > 0)
            {
                var leftC = tpl.Points[leftBandCount];
                var rightC = tpl.Points[leftBandCount + 1];
                components.Add(new TemplateComponent
                {
                    StartPointId = leftC.Id,
                    EndPointId = rightC.Id,
                    Kind = TemplateComponentKind.MedianStrip,
                });
            }

            //    右半段：顺序（中心 → 最外），段 i 连接 tpl.Points[centerRightIdx + i] 和 [centerRightIdx + i + 1]
            int centerRightIdx = layout.CenterMedianWidth > 0
                ? leftBandCount + 1
                : leftBandCount;
            for (int i = 0; i < layout.RightBands.Count; i++)
            {
                var start = tpl.Points[centerRightIdx + i];
                var end = tpl.Points[centerRightIdx + i + 1];
                var sk = rightSegKinds[i];
                components.Add(new TemplateComponent
                {
                    StartPointId = start.Id,
                    EndPointId = end.Id,
                    Kind = sk.Kind,
                });
            }

            tpl.Components.AddRange(components);
            return tpl;
        }

        private static TemplatePoint MakePoint(double x, double y, string name,
            double elevationDiff = 0, double innerElevationDiff = 0)
        {
            var point = new TemplatePoint
            {
                Name = name,
                HorizontalOffset = x,
                VerticalOffset = y,
            };
            if (Math.Abs(elevationDiff) > 1e-9)
            {
                point.ExtendedData[ElevationDiffKey] = elevationDiff.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (Math.Abs(innerElevationDiff) > 1e-9)
            {
                point.ExtendedData[InnerElevationDiffKey] = innerElevationDiff.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            return point;
        }

        // ============================================================================
        //  Template → Layout（反向，加载已有模板到编辑器）
        // ============================================================================

        /// <summary>
        /// 把 <see cref="Template"/> 尽力还原为 <see cref="CrossSectionLayout"/>。
        ///
        /// <b>约束</b>（不满足则返回 null）：
        /// <list type="bullet">
        ///   <item>至少 2 个点；点横偏移从左到右单调不减。</item>
        ///   <item>若存在"中心 0 偏移点（左/右）"则中分带宽 = (xRightCenter − xLeftCenter)；否则视为 0。</item>
        ///   <item>段数 = 点数 − 1。</item>
        /// </list>
        ///
        /// 设计速度 / 比例 / 标题来自 <paramref name="defaultSpeed"/> / <paramref name="defaultScale"/> / <paramref name="defaultTitle"/>，
        /// 因为 <see cref="Template"/> 不保存这些字段。
        /// </summary>
        public static CrossSectionLayout FromTemplate(
            Template template,
            int defaultSpeed = 60,
            int defaultScale = 100,
            string defaultTitle = "标准横断面图")
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (template.Points.Count < 2) return null;
            if (template.Components.Count != template.Points.Count - 1) return null;

            // 按 HorizontalOffset 升序整理点索引
            var orderedPoints = new List<TemplatePoint>(template.Points);
            orderedPoints.Sort((a, b) => a.HorizontalOffset.CompareTo(b.HorizontalOffset));
            // 要求原始即有序（避免拓扑错位的旧数据）
            for (int i = 0; i < template.Points.Count; i++)
            {
                if (!ReferenceEquals(orderedPoints[i], template.Points[i])) return null;
            }

            // 识别中分带：若某一对相邻点以 x=0 为中心且段 Kind=MedianStrip 则视为中分带
            int medianLeftIdx = -1;
            for (int i = 0; i < template.Components.Count; i++)
            {
                var c = template.Components[i];
                if (c.Kind == TemplateComponentKind.MedianStrip)
                {
                    double xL = template.Points[i].HorizontalOffset;
                    double xR = template.Points[i + 1].HorizontalOffset;
                    if (Math.Abs(xL + xR) < 1e-6 && xR - xL > 1e-9)
                    {
                        medianLeftIdx = i;
                        break;
                    }
                }
            }

            int leftInnerIdx, rightInnerIdx;
            double centerMedianWidth;
            if (medianLeftIdx >= 0)
            {
                leftInnerIdx = medianLeftIdx;
                rightInnerIdx = medianLeftIdx + 1;
                centerMedianWidth = template.Points[rightInnerIdx].HorizontalOffset
                                  - template.Points[leftInnerIdx].HorizontalOffset;
            }
            else
            {
                // 无中分带：找 |x| 最小的点当作"中心"
                int cIdx = 0;
                for (int i = 1; i < template.Points.Count; i++)
                {
                    if (Math.Abs(template.Points[i].HorizontalOffset)
                        < Math.Abs(template.Points[cIdx].HorizontalOffset))
                    {
                        cIdx = i;
                    }
                }
                leftInnerIdx = cIdx;
                rightInnerIdx = cIdx;
                centerMedianWidth = 0;
            }

            // 左半：从最外(0) → 中心(leftInnerIdx)，反推条带（索引 0 → leftInnerIdx-1 对应段 0 → leftInnerIdx-1）
            var leftBandsFromOuter = new List<CrossSectionBand>();
            for (int i = 0; i < leftInnerIdx; i++)
            {
                var p0 = template.Points[i];
                var p1 = template.Points[i + 1];
                double w = p1.HorizontalOffset - p0.HorizontalOffset;
                if (w <= 0) return null;
                double dy = p1.VerticalOffset - p0.VerticalOffset;
                // 从"最外→中心"方向：p0 是外，p1 是内。"内高外低"时 p1.y > p0.y
                // 回推 slopePct：CrossSlopePct = (dy / w) * 100 * -slopeSign(kind) 的绝对值
                var kind = template.Components[i].Kind;
                int sign = SlopeSign(kind);
                double slopePct;
                if (sign == 0)
                    slopePct = 0;
                else
                {
                    // 沿"最外→内"方向 dy 与 Pct 的关系：
                    //   Pct > 0 ⇒ 从内向外 y 下降（外低内高）
                    //     即 dy(外→内) > 0 ⇒ Pct = dy / w * 100
                    slopePct = dy / w * 100;
                }
                leftBandsFromOuter.Add(new CrossSectionBand(
                    CleanBandName(p1.Name ?? p0.Name, prefix: ""),
                    kind, w, slopePct, BandSide.Left)
                    .WithElevationDiff(ReadElevationDiff(p0))
                    .WithInnerElevationDiff(ReadInnerElevationDiff(p0)));
            }
            // 反转成"从中心向外"
            leftBandsFromOuter.Reverse();

            // 右半：从中心(rightInnerIdx) → 最外，段索引 rightInnerIdx → count-2
            var rightBandsFromInner = new List<CrossSectionBand>();
            for (int i = rightInnerIdx; i < template.Components.Count; i++)
            {
                var p0 = template.Points[i];
                var p1 = template.Points[i + 1];
                double w = p1.HorizontalOffset - p0.HorizontalOffset;
                if (w <= 0) return null;
                double dy = p1.VerticalOffset - p0.VerticalOffset;
                var kind = template.Components[i].Kind;
                int sign = SlopeSign(kind);
                double slopePct;
                if (sign == 0)
                    slopePct = 0;
                else
                {
                    // 从"内→外"方向：dy 为负（外低） ⇒ Pct = -dy / w * 100
                    slopePct = -dy / w * 100;
                }
                rightBandsFromInner.Add(new CrossSectionBand(
                    CleanBandName(p1.Name ?? p0.Name, prefix: ""),
                    kind, w, slopePct, BandSide.Right)
                    .WithElevationDiff(ReadElevationDiff(p1))
                    .WithInnerElevationDiff(ReadInnerElevationDiff(p1)));
            }

            try
            {
                return CrossSectionLayout.Create(
                    leftBandsFromOuter,
                    rightBandsFromInner,
                    centerMedianWidth,
                    defaultSpeed,
                    defaultScale,
                    string.IsNullOrEmpty(template.Name) ? defaultTitle : template.Name);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string CleanBandName(string raw, string prefix)
        {
            if (string.IsNullOrEmpty(raw)) return "条带";
            var s = raw.Replace("外缘", "").Trim();
            if (s.Length == 0) return "条带";
            return prefix + s;
        }

        // ============================================================================
        //  Layout → Figure（WPF 预览 + AutoCAD 出图共用，v2 委托给 Generator）
        // ============================================================================

        /// <summary>
        /// 单个板块在最终 <see cref="CrossSectionFigure.Vertices"/> 列表中的索引落点。
        /// 用于派生 Panel / 横坡 label / 顶部 label 的位置。
        /// </summary>
        private struct StripPlacement
        {
            /// <summary>板块内端在 vertices 中的索引（即上一板块的 OuterVertexIndex 或中心）。</summary>
            public int InnerVertexIndex;

            /// <summary>路面外缘点在 vertices 中的索引（与板块本身 Panel 的边界）。</summary>
            public int SurfaceOuterVertexIndex;

            /// <summary>板块最外端（NextInner）在 vertices 中的索引。无路牙时 = SurfaceOuterVertexIndex。</summary>
            public int OuterVertexIndex;

            /// <summary>是否包含外侧路牙凸起。</summary>
            public bool HasOuterKerb;
        }

        /// <summary>
        /// 把 <see cref="CrossSectionLayout"/> 铺平为一组绘图指令。
        ///
        /// v2 几何由 <see cref="CrossSectionGeometryGenerator"/> 生成，
        /// 支持路牙凸起、抛物线/折线路拱等扩展形态。
        ///
        /// 产出：
        /// <list type="bullet">
        ///   <item>Vertices：从最左 → 最右 的外轮廓折线（含中分带两端点 + 路牙顶 + 路拱插值点）。</item>
        ///   <item>Panels：每条带 1 个（板块本身）+ 每路牙 1 个（如有）+ 中分带 1 个（如有）。</item>
        ///   <item>DimensionSegments：底部 Tier=0 总长 / Tier=1 分段 + 顶部 Tier=2 总宽。</item>
        ///   <item>SlopeLabels：有横坡的条带在路面中点；相邻条带若连接处 y 平齐且横坡相同则合并为一条。</item>
        ///   <item>HeightLabels：各顶点；竖向以道路中心线 x=0 处高程为 ±0.000（与「中心线/设计起点」一致，非最内车道缝）。有中分带时中分上只标一个 ±0.000（x=0），不标中心左/中分缝/中心右三处。相邻板 y 平齐、横坡同处不标（免矛盾）。</item>
        ///   <item>TopLabels：每条带中央一个（名称）+ 中分带 1 个（"中央分隔带"）。</item>
        ///   <item>Orientation / Title：固定左"北"右"南"、底部居中标题。</item>
        /// </list>
        /// </summary>
        public static CrossSectionFigure ToFigure(CrossSectionLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            double xLeftInner = -layout.CenterMedianWidth / 2.0;
            double xRightInner = +layout.CenterMedianWidth / 2.0;
            double Wm = layout.CenterMedianWidth;
            double wL = ResolveMedianLeftWidthMeters(layout, Wm);
            double wR = Wm > 1e-9 ? (Wm - wL) : 0;
            double xSplit = xLeftInner + wL;

            // 1) 左条带自 (xL,0) 起。存在中分带时：右条带起点 = 中分带右缘标高 与 最内条 内端高差 反推；无中分带时沿用 y=0 起点
            var leftStrips = GenerateStrips(layout.LeftBands, xLeftInner, 0, BandSide.Left);
            double y0 = 0, y1 = 0, y2 = 0;
            double rStartY = 0;
            if (Wm > 1e-9)
            {
                y0 = (leftStrips.Count > 0 ? leftStrips[0].StartY : 0) + layout.MedianLeftOuterElevationDiff;
                double yAfterL = y0 + wL * (layout.MedianLeftCrossSlopePct / 100.0);
                y1 = yAfterL + layout.MedianLeftInnerElevationDiff + layout.MedianRightInnerElevationDiff;
                y2 = y1 - wR * (layout.MedianRightCrossSlopePct / 100.0);
                double yConnectRight = y2 + layout.MedianRightOuterElevationDiff;
                if (layout.RightBands != null && layout.RightBands.Count > 0)
                    rStartY = yConnectRight - layout.RightBands[0].InnerElevationDiff;
            }

            var rightStrips = GenerateStrips(layout.RightBands, xRightInner, rStartY, BandSide.Right);

            // 2) 拼接 vertices（最左 → 中心 → 最右）+ 同步建立 StripPlacement 索引映射
            var vertices = new List<FigureVertex>();
            var leftPlacements = new StripPlacement[leftStrips.Count];
            var rightPlacements = new StripPlacement[rightStrips.Count];

            // 左半反向：strip[N-1] (最外) 先放，strip[0] (最内) 最后放。
            // 汇编后的 vertex 方向 = 外 → 内。
            // 当两条相邻带之间存在高差跳变（前一带 ElevationDiff + 本带 InnerElevationDiff ≠ 0）时，
            // 需要在"本带（更外）outer-写完之后、下一带（更内）outer-开始写之前"插一个 **"本带内缘顶点"**
            //（位置 = geo.StartX/StartY，已含跳变的抬/落后 y），否则折线会把两端直接斜连成三角形。
            for (int s = leftStrips.Count - 1; s >= 0; s--)
            {
                var geo = leftStrips[s];
                int placementOuterIdx = vertices.Count;             // 反向后第一个写入 = 该 strip 的"NextInner"端
                int placementSurfaceIdx = placementOuterIdx + (geo.Vertices.Count - 1 - geo.SurfaceOuterIndex);

                // 反向写入 strip vertices
                for (int v = geo.Vertices.Count - 1; v >= 0; v--)
                {
                    var bv = geo.Vertices[v];
                    vertices.Add(new FigureVertex(bv.X, bv.Y, bv.Name));
                }

                // 内缘跳变顶点：与下一段（更内）的"外缘落点"对比
                // s > 0：下一段是 leftStrips[s-1]，其"外缘"落点 = geo_(s-1).NextInnerX/Y
                // s = 0：下一"段"是中心，落点 (xLeftInner, 0)
                double nextX, nextY;
                if (s > 0)
                {
                    var innerGeo = leftStrips[s - 1];
                    nextX = innerGeo.NextInnerX;
                    nextY = innerGeo.NextInnerY;
                }
                else
                {
                    nextX = xLeftInner;
                    nextY = Wm > 1e-9 ? y0 : 0;
                }

                int placementInnerIdx;
                bool hasInnerJump = Math.Abs(nextX - geo.StartX) > 1e-9 || Math.Abs(nextY - geo.StartY) > 1e-9;
                if (hasInnerJump)
                {
                    vertices.Add(new FigureVertex(geo.StartX, geo.StartY, layout.LeftBands[s].Name + "内缘"));
                    placementInnerIdx = vertices.Count - 1;
                }
                else
                {
                    // 与下一段 outer（或中心）共点：占位 -1，循环结束后统一回填为那一侧的 index
                    placementInnerIdx = -1;
                }

                leftPlacements[s] = new StripPlacement
                {
                    OuterVertexIndex = placementOuterIdx,
                    SurfaceOuterVertexIndex = placementSurfaceIdx,
                    InnerVertexIndex = placementInnerIdx,
                    HasOuterKerb = geo.HasOuterKerb,
                };
            }

            // 中分带顶面折线：中心左 / [中分缝] / 中心右
            int centerLeftIndex = vertices.Count;
            int centerRightIndex = centerLeftIndex;
            int medianSeamIndex = -1;
            if (Wm > 1e-9)
            {
                vertices.Add(new FigureVertex(xLeftInner, y0, "中心左"));
                if (wL > 1e-6 && wR > 1e-6
                    && Math.Abs(xSplit - xLeftInner) > 1e-6
                    && Math.Abs(xRightInner - xSplit) > 1e-6)
                {
                    medianSeamIndex = vertices.Count;
                    vertices.Add(new FigureVertex(xSplit, y1, "中分缝"));
                }
                centerRightIndex = vertices.Count;
                vertices.Add(new FigureVertex(xRightInner, y2, "中心右"));
            }
            else
            {
                vertices.Add(new FigureVertex(xLeftInner, 0, "中心左"));
                centerRightIndex = centerLeftIndex;
            }

            // 回填左半 InnerVertexIndex 占位（-1）：strip[0] → 中心左；strip[s>0] → strip[s-1] 的 OuterVertexIndex
            if (leftStrips.Count > 0)
            {
                if (leftPlacements[0].InnerVertexIndex < 0)
                    leftPlacements[0].InnerVertexIndex = centerLeftIndex;
                for (int s = 1; s < leftStrips.Count; s++)
                {
                    if (leftPlacements[s].InnerVertexIndex < 0)
                        leftPlacements[s].InnerVertexIndex = leftPlacements[s - 1].OuterVertexIndex;
                }
            }

            // 右半正向：strip[0] 先放；汇编方向 = 内 → 外。
            // 与左半对称：在每段"outer 顶点写入前"，如果与上一段外缘（或中心）存在 X/Y 跳变，
            // 就先吐一个"本段内缘顶点"使跳变表现为真正的竖直段。
            for (int s = 0; s < rightStrips.Count; s++)
            {
                var geo = rightStrips[s];
                int prevRefIdx = s == 0 ? centerRightIndex : rightPlacements[s - 1].OuterVertexIndex;
                var prevRef = vertices[prevRefIdx];
                bool hasInnerJump = Math.Abs(prevRef.X - geo.StartX) > 1e-9 || Math.Abs(prevRef.Y - geo.StartY) > 1e-9;

                int placementInnerIdx;
                if (hasInnerJump)
                {
                    vertices.Add(new FigureVertex(geo.StartX, geo.StartY, layout.RightBands[s].Name + "内缘"));
                    placementInnerIdx = vertices.Count - 1;
                }
                else
                {
                    placementInnerIdx = prevRefIdx;
                }

                int firstWrittenIdx = vertices.Count;
                int placementSurfaceIdx = firstWrittenIdx + geo.SurfaceOuterIndex;

                foreach (var bv in geo.Vertices)
                {
                    vertices.Add(new FigureVertex(bv.X, bv.Y, bv.Name));
                }

                int placementOuterIdx = vertices.Count - 1;
                rightPlacements[s] = new StripPlacement
                {
                    InnerVertexIndex = placementInnerIdx,
                    SurfaceOuterVertexIndex = placementSurfaceIdx,
                    OuterVertexIndex = placementOuterIdx,
                    HasOuterKerb = geo.HasOuterKerb,
                };
            }

            // 2b) 设计竖向基准：以道路中心线 x=0 处顶面为 ±0.000（与中心线一致；非最内车道与缘石接点）
            double yDatum = InterpolateYOnProfileAtX(vertices, 0);
            for (int vi = 0; vi < vertices.Count; vi++)
            {
                var p = vertices[vi];
                vertices[vi] = new FigureVertex(p.X, p.Y - yDatum, p.Name);
            }

            // 3) Panels：板块本身 + 路牙（如有）+ 中分带
            var panels = new List<FigurePanel>();

            // 左半板块（按 vertices 从左到右顺序）：先输出"最外"板块，最后输出"最内"板块
            for (int s = leftStrips.Count - 1; s >= 0; s--)
            {
                var band = layout.LeftBands[s];
                var pl = leftPlacements[s];
                int a = Math.Min(pl.InnerVertexIndex, pl.SurfaceOuterVertexIndex);
                int b = Math.Max(pl.InnerVertexIndex, pl.SurfaceOuterVertexIndex);
                panels.Add(new FigurePanel(band.Kind, a, b, band.Name));

                if (pl.HasOuterKerb)
                {
                    int ka = Math.Min(pl.SurfaceOuterVertexIndex, pl.OuterVertexIndex);
                    int kb = Math.Max(pl.SurfaceOuterVertexIndex, pl.OuterVertexIndex);
                    panels.Add(new FigurePanel(TemplateComponentKind.Kerb, ka, kb, band.Name + "路牙"));
                }
            }

            // 中分带
            if (layout.CenterMedianWidth > 0)
            {
                panels.Add(new FigurePanel(TemplateComponentKind.MedianStrip,
                    centerLeftIndex, centerRightIndex, "中央分隔带"));
            }

            // 右半板块
            for (int s = 0; s < rightStrips.Count; s++)
            {
                var band = layout.RightBands[s];
                var pl = rightPlacements[s];
                int a = Math.Min(pl.InnerVertexIndex, pl.SurfaceOuterVertexIndex);
                int b = Math.Max(pl.InnerVertexIndex, pl.SurfaceOuterVertexIndex);
                panels.Add(new FigurePanel(band.Kind, a, b, band.Name));

                if (pl.HasOuterKerb)
                {
                    int ka = Math.Min(pl.SurfaceOuterVertexIndex, pl.OuterVertexIndex);
                    int kb = Math.Max(pl.SurfaceOuterVertexIndex, pl.OuterVertexIndex);
                    panels.Add(new FigurePanel(TemplateComponentKind.Kerb, ka, kb, band.Name + "路牙"));
                }
            }

            // 4) Dimension Segments
            var dimSegs = BuildDimensionSegments(layout, leftStrips, rightStrips, xLeftInner, xRightInner);

            // 5) 横坡 Labels（顶面 y 已按中心线归 0；与条带几何同减 yDatum）
            var slopes = new List<FigureSlopeLabel>();
            AppendSlopeLabelsMergingFlushJoints(slopes, layout.LeftBands, leftStrips, yDatum);
            AppendSlopeLabelsMergingFlushJoints(slopes, layout.RightBands, rightStrips, yDatum);
            if (Wm > 1e-9)
            {
                double mY0 = vertices[centerLeftIndex].Y;
                double mY2 = vertices[centerRightIndex].Y;
                double mY1 = medianSeamIndex >= 0
                    ? vertices[medianSeamIndex].Y
                    : mY0 + (mY2 - mY0) * (xSplit - xLeftInner) / (xRightInner - xLeftInner + 1e-12);
                if (Math.Abs(layout.MedianLeftCrossSlopePct) > 1e-6)
                {
                    double midX = (xLeftInner + xSplit) * 0.5;
                    double midY = (mY0 + mY1) * 0.5 + 0.25;
                    slopes.Add(new FigureSlopeLabel(midX, midY, $"{layout.MedianLeftCrossSlopePct:F1}%"));
                }
                if (Math.Abs(layout.MedianRightCrossSlopePct) > 1e-6)
                {
                    double midX2 = (xSplit + xRightInner) * 0.5;
                    double midY2 = (mY1 + mY2) * 0.5 + 0.25;
                    slopes.Add(new FigureSlopeLabel(midX2, midY2, $"{layout.MedianRightCrossSlopePct:F1}%"));
                }
            }

            // 6) 标高 Labels（去重 + 同坡平齐条缝不标；有中分带时中分只标 x=0 一处 ±0.000）
            var noHeightJoints = new List<(double x, double y)>();
            CollectNoHeightJointsOnFlushSameSlopePair(noHeightJoints, layout.LeftBands, leftStrips);
            CollectNoHeightJointsOnFlushSameSlopePair(noHeightJoints, layout.RightBands, rightStrips);
            for (int j = 0; j < noHeightJoints.Count; j++)
            {
                var t = noHeightJoints[j];
                noHeightJoints[j] = (t.x, t.y - yDatum);
            }

            var heights = new List<FigureHeightLabel>();
            var skipMedianVertex = new HashSet<int>();
            if (Wm > 1e-9)
            {
                skipMedianVertex.Add(centerLeftIndex);
                skipMedianVertex.Add(centerRightIndex);
                if (medianSeamIndex >= 0) skipMedianVertex.Add(medianSeamIndex);
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                if (skipMedianVertex.Contains(i)) continue;
                var v = vertices[i];
                if (i > 0)
                {
                    var prev = vertices[i - 1];
                    if (Math.Abs(v.X - prev.X) < 1e-9 && Math.Abs(v.Y - prev.Y) < 1e-9) continue;
                }
                if (IsNearAnyJoint(v.X, v.Y, noHeightJoints, tol: 1e-3)) continue;
                heights.Add(new FigureHeightLabel(v.X, v.Y, FormatHeight(v.Y)));
            }
            if (Wm > 1e-9)
            {
                heights.Add(new FigureHeightLabel(0, 0, FormatHeight(0)));
            }

            // 7) 顶部 Labels：板块中点（X 取板块内端与路面外缘 X 的中点）+ 中分带
            double topY = MaxY(vertices) + 1.5;
            var topLabels = new List<FigureTopLabel>();
            for (int s = 0; s < leftStrips.Count; s++)
            {
                var band = layout.LeftBands[s];
                var geo = leftStrips[s];
                double centerX = (geo.StartX + geo.SurfaceOuterX) * 0.5;
                topLabels.Add(new FigureTopLabel(centerX, topY, band.Name));
            }
            if (layout.CenterMedianWidth > 0)
            {
                topLabels.Add(new FigureTopLabel(0, topY, "中央分隔带"));
            }
            for (int s = 0; s < rightStrips.Count; s++)
            {
                var band = layout.RightBands[s];
                var geo = rightStrips[s];
                double centerX = (geo.StartX + geo.SurfaceOuterX) * 0.5;
                topLabels.Add(new FigureTopLabel(centerX, topY, band.Name));
            }

            // 8) Orientation + Title
            double leftmostX = vertices.Count > 0 ? vertices[0].X : -layout.LeftHalfWidth;
            double rightmostX = vertices.Count > 0 ? vertices[vertices.Count - 1].X : +layout.RightHalfWidth;
            double orientationY = topY + 2.0;
            var orientation = new FigureOrientation(leftmostX, rightmostX, orientationY, "北", "南");
            double titleY = MinY(vertices) - 3.0;
            // 图名不含比例；比例由 DrawingSheetTitleDrawer 按 Figure.ScaleDenominator 单独注写（避免与行内重复）。
            var title = new FigureTitle(0, titleY,
                string.IsNullOrWhiteSpace(layout.Title) ? "标准横断面图" : layout.Title.Trim());

            return new CrossSectionFigure(
                vertices, panels, dimSegs, slopes, heights, topLabels,
                orientation, title, layout.TotalWidth, layout.ScaleDenominator);
        }

        // =============== Helpers ===============

        private static double ResolveMedianLeftWidthMeters(CrossSectionLayout layout, double centerWidth)
        {
            if (centerWidth <= 1e-9) return 0;
            double t = layout.MedianLeftSubWidth;
            if (t > 1e-9 && t <= centerWidth) return t;
            return centerWidth * 0.5;
        }

        private static List<BandGeometry> GenerateStrips(IReadOnlyList<CrossSectionBand> bands, double startX, double startY, BandSide side)
        {
            var list = new List<BandGeometry>(bands.Count);
            double sx = startX, sy = startY;
            foreach (var band in bands)
            {
                // 本条带"内端"跳变：在生成该板块前先叠加 InnerElevationDiff，
                // 让 GenerateBand 从抬/落后的内缘起步；外端跳变仍由下一轮的 sy += ElevationDiff 承担。
                sy += band.InnerElevationDiff;
                var geo = CrossSectionGeometryGenerator.GenerateBand(sx, sy, band, side);
                list.Add(geo);
                sx = geo.NextInnerX;
                sy = geo.NextInnerY + band.ElevationDiff;
            }
            return list;
        }

        private static double ReadElevationDiff(TemplatePoint point)
            => ReadExtendedDouble(point, ElevationDiffKey);

        private static double ReadInnerElevationDiff(TemplatePoint point)
            => ReadExtendedDouble(point, InnerElevationDiffKey);

        private static double ReadExtendedDouble(TemplatePoint point, string key)
        {
            if (point?.ExtendedData == null) return 0;
            if (!point.ExtendedData.TryGetValue(key, out var raw)) return 0;
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        private static List<FigureDimensionSegment> BuildDimensionSegments(
            CrossSectionLayout layout,
            IReadOnlyList<BandGeometry> leftStrips,
            IReadOnlyList<BandGeometry> rightStrips,
            double xLeftInner,
            double xRightInner)
        {
            var dimSegs = new List<FigureDimensionSegment>();

            double leftmostX = -layout.LeftHalfWidth - layout.CenterMedianWidth / 2.0;
            double rightmostX = +layout.RightHalfWidth + layout.CenterMedianWidth / 2.0;
            double totalWidth = layout.TotalWidth;

            // Tier=1 分段链：每条带一格（左半从最外→中心；中分带；右半从中心→最外）
            double currX = leftmostX;
            for (int i = layout.LeftBands.Count - 1; i >= 0; i--)
            {
                var b = layout.LeftBands[i];
                double next = currX + b.Width;
                dimSegs.Add(new FigureDimensionSegment(currX, next, FormatWidthMeters(b.Width), tier: 1));
                currX = next;
            }
            if (layout.CenterMedianWidth > 0)
            {
                double next = currX + layout.CenterMedianWidth;
                dimSegs.Add(new FigureDimensionSegment(currX, next, FormatWidthMeters(layout.CenterMedianWidth), tier: 1));
                currX = next;
            }
            foreach (var b in layout.RightBands)
            {
                double next = currX + b.Width;
                dimSegs.Add(new FigureDimensionSegment(currX, next, FormatWidthMeters(b.Width), tier: 1));
                currX = next;
            }

            // Tier=0 总长链：左半 / [中分带] / 右半
            if (layout.LeftHalfWidth > 0)
                dimSegs.Add(new FigureDimensionSegment(leftmostX, xLeftInner,
                    FormatWidthMeters(layout.LeftHalfWidth), tier: 0));
            if (layout.CenterMedianWidth > 0)
                dimSegs.Add(new FigureDimensionSegment(xLeftInner, xRightInner,
                    FormatWidthMeters(layout.CenterMedianWidth), tier: 0));
            if (layout.RightHalfWidth > 0)
                dimSegs.Add(new FigureDimensionSegment(xRightInner, rightmostX,
                    FormatWidthMeters(layout.RightHalfWidth), tier: 0));

            // Tier=2 顶部总宽
            if (totalWidth > 0)
                dimSegs.Add(new FigureDimensionSegment(leftmostX, rightmostX,
                    FormatWidthMeters(totalWidth), tier: 2));

            return dimSegs;
        }

        /// <summary>同一侧上：下标 k 为靠中心(内条)，k+1 为更外条。连接 = 内条外缘(NextInner) 与 外条内端(Start)。</summary>
        private static bool AreFlushBandJoint(BandGeometry innerStrip, BandGeometry outerStrip, double tol = 1e-4)
        {
            return Math.Abs(innerStrip.NextInnerX - outerStrip.StartX) < tol
                   && Math.Abs(innerStrip.NextInnerY - outerStrip.StartY) < tol;
        }

        private static bool SlopesEqualForBandPair(CrossSectionBand innerBand, CrossSectionBand outerBand) =>
            Math.Abs(innerBand.CrossSlopePct - outerBand.CrossSlopePct) < 1e-6;

        private static void CollectNoHeightJointsOnFlushSameSlopePair(
            List<(double x, double y)> sink,
            IReadOnlyList<CrossSectionBand> bands,
            IReadOnlyList<BandGeometry> strips)
        {
            if (strips == null || bands == null || strips.Count < 2) return;
            for (int k = 0; k < strips.Count - 1; k++)
            {
                if (!SlopesEqualForBandPair(bands[k], bands[k + 1])) continue;
                if (!AreFlushBandJoint(strips[k], strips[k + 1])) continue;
                sink.Add((strips[k + 1].StartX, strips[k + 1].StartY));
            }
        }

        private static bool IsNearAnyJoint(double x, double y, List<(double x, double y)> joints, double tol)
        {
            for (int i = 0; i < joints.Count; i++)
            {
                var j = joints[i];
                if (Math.Abs(x - j.x) < tol && Math.Abs(y - j.y) < tol) return true;
            }
            return false;
        }

        /// <summary>若有横坡的相邻条 y 平齐且横坡%相同，将连续段只标一条横坡（位于该段有坡条中点之平均位置）。</summary>
        private static void AppendSlopeLabelsMergingFlushJoints(
            List<FigureSlopeLabel> slopes,
            IReadOnlyList<CrossSectionBand> bands,
            IReadOnlyList<BandGeometry> strips,
            double yDatum = 0)
        {
            if (strips == null || bands == null || strips.Count == 0) return;
            int s0 = 0;
            while (s0 < strips.Count)
            {
                int s1 = s0;
                while (s1 + 1 < strips.Count
                      && SlopesEqualForBandPair(bands[s1], bands[s1 + 1])
                      && AreFlushBandJoint(strips[s1], strips[s1 + 1]))
                {
                    s1++;
                }
                // 段 [s0..s1] 内，逐条有坡的条各取原中点，再平均，合并成一条文字（同%）
                double sumMx = 0, sumMy = 0;
                int c = 0;
                double pct = bands[s0].CrossSlopePct;
                for (int i = s0; i <= s1; i++)
                {
                    var band = bands[i];
                    var geo = strips[i];
                    int sign = CrossSectionGeometryGenerator.SurfaceSlopeSign(band.Kind);
                    if (Math.Abs(band.CrossSlopePct) < 1e-9 || sign == 0) continue;
                    sumMx += (geo.StartX + geo.SurfaceOuterX) * 0.5;
                    sumMy += (geo.StartY + geo.SurfaceOuterY) * 0.5 + 0.25;
                    c++;
                    pct = band.CrossSlopePct;
                }
                if (c > 0) slopes.Add(new FigureSlopeLabel(sumMx / c, sumMy / c - yDatum, $"{pct:F1}%"));
                s0 = s1 + 1;
            }
        }

        /// <summary>沿最左→最外轮廓折线，对给定 x 线性插值顶面 y；无交则取与 x 最近顶点。用于 x=0 路中心高程作 ±0.000 基准。</summary>
        private static double InterpolateYOnProfileAtX(IReadOnlyList<FigureVertex> path, double xTarget)
        {
            if (path == null || path.Count == 0) return 0;
            if (path.Count == 1) return path[0].Y;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                double x1 = a.X, x2 = b.X;
                if (Math.Abs(x2 - x1) < 1e-12) continue;
                double lo = Math.Min(x1, x2);
                double hi = Math.Max(x1, x2);
                if (xTarget + 1e-9 < lo || xTarget - 1e-9 > hi) continue;
                double t = (xTarget - x1) / (x2 - x1);
                return a.Y + t * (b.Y - a.Y);
            }
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                double d = Math.Abs(path[i].X - xTarget);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return path[best].Y;
        }

        // 宽度文本：v1 统一用 m，保留 2 位小数（0 自动省略末尾 0）
        private static string FormatWidthMeters(double w)
        {
            // 保留 3 位小数再按需裁剪末尾 0，避免"3.500"看起来像毫米
            string s = w.ToString("F3");
            if (s.Contains("."))
            {
                s = s.TrimEnd('0');
                if (s.EndsWith(".")) s = s.TrimEnd('.');
            }
            return s;
        }

        private static string FormatHeight(double y)
        {
            if (Math.Abs(y) < 1e-4) return "±0.000";
            return y > 0 ? $"+{y:F3}" : y.ToString("F3");
        }

        private static double MaxY(IReadOnlyList<FigureVertex> vertices)
        {
            double max = 0;
            foreach (var v in vertices) if (v.Y > max) max = v.Y;
            return max;
        }

        private static double MinY(IReadOnlyList<FigureVertex> vertices)
        {
            double min = 0;
            foreach (var v in vertices) if (v.Y < min) min = v.Y;
            return min;
        }
    }
}
