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
    ///   <item><see cref="ToTemplate"/>：写入 <c>RoadDesign.Templates</c> 的 Domain 形态；v1 忽略缘石凸起，
    ///       每条带 1 个外缘点，中央分隔带额外 2 个点（左/右边缘）。</item>
    ///   <item><see cref="ToFigure"/>：直接从条带构造图纸指令，避免"条带→Template→图纸"的二次转换
    ///       丢失 Band 名称 / 顶部标签等元数据。</item>
    ///   <item><see cref="FromTemplate"/>：反向拼 Band。对非单调横偏移或零点缺失的旧 Template 返回 null，
    ///       让窗口提示"此模板无法编辑，请新建"。</item>
    /// </list>
    ///
    /// 横坡符号约定（内高外低）：
    /// <list type="bullet">
    ///   <item>机动车道 / 非机动车道 / 人行道：<c>CrossSlopePct &gt; 0</c> 表示"向外下降 CrossSlopePct %"。</item>
    ///   <item>缘石 / 中央分隔带 / 绿化带：v1 视为水平段，<c>CrossSlopePct</c> 被忽略（即使传了也不推 y）。</item>
    /// </list>
    /// </summary>
    public static class CrossSectionLayoutBuilder
    {
        /// <summary>
        /// 比较两条带"从中心向外扫描"时 y 偏移时的符号：
        /// 返回 -1 表示外侧 y 比内侧 y 低（路面类）；返回 0 表示水平。
        /// </summary>
        private static int SlopeSign(TemplateComponentKind kind)
        {
            switch (kind)
            {
                case TemplateComponentKind.Pavement:
                case TemplateComponentKind.NonMotorized:
                case TemplateComponentKind.Sidewalk:
                case TemplateComponentKind.Shoulder:
                    return -1;

                // 缘石 / 中央分隔带 / 绿化带 / 边坡（v1 不算斜坡）/ 未知类型 → 水平
                default:
                    return 0;
            }
        }

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
            var leftPoints = new List<TemplatePoint>();
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
            foreach (var band in layout.LeftBands)
            {
                double sign = SlopeSign(band.Kind);
                double dx = -band.Width;
                double dy = band.Width * (band.CrossSlopePct / 100.0) * sign;
                xL += dx;
                yL += dy;
                leftOuterToInner.Add(MakePoint(xL, yL, band.Name + "外缘"));
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
                rightOuterList.Add(MakePoint(xR, yR, band.Name + "外缘"));
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

        private static TemplatePoint MakePoint(double x, double y, string name) => new TemplatePoint
        {
            Name = name,
            HorizontalOffset = x,
            VerticalOffset = y,
        };

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
                    kind, w, slopePct, BandSide.Left));
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
                    kind, w, slopePct, BandSide.Right));
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
        //  Layout → Figure（WPF 预览 + AutoCAD 出图共用）
        // ============================================================================

        /// <summary>
        /// 把 <see cref="CrossSectionLayout"/> 铺平为一组绘图指令。
        ///
        /// 产出：
        /// <list type="bullet">
        ///   <item>Vertices：从最左 → 最右 的外轮廓折线（含中分带两端点）。</item>
        ///   <item>Panels：每条带 1 个（中分带 +1，如果存在）。</item>
        ///   <item>DimensionSegments：
        ///     <list type="bullet">
        ///       <item>Tier=0 底部总长链：左半 / 中分带 / 右半 三段。</item>
        ///       <item>Tier=1 底部分段链：每条带 + 中分带各一段。</item>
        ///       <item>Tier=2 顶部总红线宽链：一段。</item>
        ///     </list>
        ///   </item>
        ///   <item>SlopeLabels：仅当条带 CrossSlopePct ≠ 0 才输出。</item>
        ///   <item>HeightLabels：左右最外点、中心、中分带两端（y = 0 时也输出"±0"）。</item>
        ///   <item>TopLabels：每条带中央一个（名称）+ 中分带 1 个（"中央分隔带"）。</item>
        ///   <item>Orientation / Title：固定左"北"右"南"、底部居中标题。</item>
        /// </list>
        /// </summary>
        public static CrossSectionFigure ToFigure(CrossSectionLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            var vertices = new List<FigureVertex>();
            var panels = new List<FigurePanel>();
            var dimSegs = new List<FigureDimensionSegment>();
            var slopes = new List<FigureSlopeLabel>();
            var heights = new List<FigureHeightLabel>();
            var topLabels = new List<FigureTopLabel>();

            // ---- 1) 顶点序列（从最左到最右）----
            // 先正向算"左外→左内→右内→右外"，过程中同步记录各条带的"段"索引范围
            double xLeftInner = -layout.CenterMedianWidth / 2.0;
            double xRightInner = +layout.CenterMedianWidth / 2.0;

            // 左半顶点（从内向外先算 outerPoints[0..leftCount-1]，最后反转）
            var leftPointsInnerToOuter = new List<(double X, double Y, string Name)>
            {
                (xLeftInner, 0, "中心左")
            };
            double xL = xLeftInner, yL = 0;
            foreach (var band in layout.LeftBands)
            {
                double sign = SlopeSign(band.Kind);
                double dx = -band.Width;
                double dy = band.Width * (band.CrossSlopePct / 100.0) * sign;
                xL += dx;
                yL += dy;
                leftPointsInnerToOuter.Add((xL, yL, band.Name + "外缘"));
            }

            // 右半顶点（从内向外）
            var rightPointsInnerToOuter = new List<(double X, double Y, string Name)>
            {
                (xRightInner, 0, "中心右")
            };
            double xR = xRightInner, yR = 0;
            foreach (var band in layout.RightBands)
            {
                double sign = SlopeSign(band.Kind);
                double dx = +band.Width;
                double dy = band.Width * (band.CrossSlopePct / 100.0) * sign;
                xR += dx;
                yR += dy;
                rightPointsInnerToOuter.Add((xR, yR, band.Name + "外缘"));
            }

            // 组合 vertices：左外 → 左内 → 右内 → 右外
            // 左：反转后从"最外 → 中心左"
            for (int i = leftPointsInnerToOuter.Count - 1; i >= 0; i--)
            {
                var p = leftPointsInnerToOuter[i];
                vertices.Add(new FigureVertex(p.X, p.Y, p.Name));
            }
            if (layout.CenterMedianWidth > 0)
            {
                // 中分带右端
                vertices.Add(new FigureVertex(xRightInner, 0, "中心右"));
                for (int i = 1; i < rightPointsInnerToOuter.Count; i++)
                {
                    var p = rightPointsInnerToOuter[i];
                    vertices.Add(new FigureVertex(p.X, p.Y, p.Name));
                }
            }
            else
            {
                // 无中分带：中心左 == 中心右，跳过右的 [0]
                for (int i = 1; i < rightPointsInnerToOuter.Count; i++)
                {
                    var p = rightPointsInnerToOuter[i];
                    vertices.Add(new FigureVertex(p.X, p.Y, p.Name));
                }
            }

            // ---- 2) Panels（按 vertices 索引对）----
            // 左半：vertices[0] … vertices[leftCount]（leftCount 段）
            // 中分带（可选）：vertices[leftCount] 到 vertices[leftCount+1]
            // 右半：vertices[centerRightIdx] … vertices[centerRightIdx + rightCount]
            int leftCount = layout.LeftBands.Count;
            int rightCount = layout.RightBands.Count;

            // 左半 Panels：vertices[i] → vertices[i+1]，对应 layout.LeftBands[leftCount - 1 - i]
            for (int i = 0; i < leftCount; i++)
            {
                var b = layout.LeftBands[leftCount - 1 - i];
                panels.Add(new FigurePanel(b.Kind, i, i + 1, b.Name));
            }
            int centerLeftIdx = leftCount;
            int centerRightIdx = layout.CenterMedianWidth > 0 ? centerLeftIdx + 1 : centerLeftIdx;
            if (layout.CenterMedianWidth > 0)
            {
                panels.Add(new FigurePanel(TemplateComponentKind.MedianStrip,
                    centerLeftIdx, centerRightIdx, "中央分隔带"));
            }
            for (int i = 0; i < rightCount; i++)
            {
                var b = layout.RightBands[i];
                panels.Add(new FigurePanel(b.Kind, centerRightIdx + i, centerRightIdx + i + 1, b.Name));
            }

            // ---- 3) Dimension Segments ----
            double totalWidth = layout.TotalWidth;
            double leftmostX = -layout.LeftHalfWidth - layout.CenterMedianWidth / 2.0;
            double rightmostX = +layout.RightHalfWidth + layout.CenterMedianWidth / 2.0;

            // Tier=1 分段链：每条带一格（左半从最外到中心；然后中分带；然后右半从中心到最外）
            double currX = leftmostX;
            for (int i = leftCount - 1; i >= 0; i--)
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

            // ---- 4) Slope labels ----
            // 左半：每条带中点一个（位置 y 取该条带两端的中值再加 0.25m 抬升）
            double leftRunX = xLeftInner;
            double leftRunY = 0;
            for (int i = 0; i < leftCount; i++)
            {
                var band = layout.LeftBands[i];
                int sign = SlopeSign(band.Kind);
                double outerX = leftRunX - band.Width;
                double outerY = leftRunY + band.Width * (band.CrossSlopePct / 100.0) * sign;
                if (band.CrossSlopePct != 0 && sign != 0)
                {
                    double midX = (leftRunX + outerX) * 0.5;
                    double midY = (leftRunY + outerY) * 0.5 + 0.25;
                    slopes.Add(new FigureSlopeLabel(midX, midY, $"{band.CrossSlopePct:F1}%"));
                }
                leftRunX = outerX;
                leftRunY = outerY;
            }
            // 右半：同上但方向相反
            double rightRunX = xRightInner;
            double rightRunY = 0;
            foreach (var band in layout.RightBands)
            {
                int sign = SlopeSign(band.Kind);
                double outerX = rightRunX + band.Width;
                double outerY = rightRunY + band.Width * (band.CrossSlopePct / 100.0) * sign;
                if (band.CrossSlopePct != 0 && sign != 0)
                {
                    double midX = (rightRunX + outerX) * 0.5;
                    double midY = (rightRunY + outerY) * 0.5 + 0.25;
                    slopes.Add(new FigureSlopeLabel(midX, midY, $"{band.CrossSlopePct:F1}%"));
                }
                rightRunX = outerX;
                rightRunY = outerY;
            }

            // ---- 5) Height labels（关键断点的 y 值）----
            // 从 vertices 直接取（除去几乎重复的中心点）
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (i > 0)
                {
                    var prev = vertices[i - 1];
                    if (Math.Abs(v.X - prev.X) < 1e-9 && Math.Abs(v.Y - prev.Y) < 1e-9) continue;
                }
                heights.Add(new FigureHeightLabel(v.X, v.Y, FormatHeight(v.Y)));
            }

            // ---- 6) Top labels：每条带中点 + 中分带（如果存在）----
            double topY = GuessTopY(vertices) + 1.5; // 顶部文字的基准高度（图面"上方"）
            // 左半条带 TopLabel
            double runX = xLeftInner;
            for (int i = 0; i < leftCount; i++)
            {
                var band = layout.LeftBands[i];
                double outerX = runX - band.Width;
                double centerX = (runX + outerX) * 0.5;
                topLabels.Add(new FigureTopLabel(centerX, topY, band.Name));
                runX = outerX;
            }
            // 中分带（仅当宽度 > 0）
            if (layout.CenterMedianWidth > 0)
            {
                topLabels.Add(new FigureTopLabel(0, topY, "中央分隔带"));
            }
            // 右半条带 TopLabel
            runX = xRightInner;
            foreach (var band in layout.RightBands)
            {
                double outerX = runX + band.Width;
                double centerX = (runX + outerX) * 0.5;
                topLabels.Add(new FigureTopLabel(centerX, topY, band.Name));
                runX = outerX;
            }

            // ---- 7) Orientation + Title ----
            double orientationY = topY + 2.0;
            var orientation = new FigureOrientation(leftmostX, rightmostX, orientationY, "北", "南");
            double titleY = MinY(vertices) - 3.0;
            var title = new FigureTitle(0, titleY,
                string.IsNullOrWhiteSpace(layout.Title)
                    ? $"标准横断面图  1:{layout.ScaleDenominator}"
                    : $"{layout.Title}  1:{layout.ScaleDenominator}");

            return new CrossSectionFigure(
                vertices, panels, dimSegs, slopes, heights, topLabels,
                orientation, title, totalWidth, layout.ScaleDenominator);
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

        private static double GuessTopY(IReadOnlyList<FigureVertex> vertices)
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
