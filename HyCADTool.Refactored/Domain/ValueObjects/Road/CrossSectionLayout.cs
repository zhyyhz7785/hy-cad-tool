using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 横断面布置（完整一张标准横断面图的参数集合）。
    ///
    /// 与 <see cref="Models.Road.Template"/> 的关系：本 VO 是"用户编辑态"，
    /// <see cref="Services.Road.CrossSectionLayoutBuilder"/> 负责双向转换：
    /// <c>CrossSectionLayout ↔ Template</c>。
    ///
    /// 不可变：通过 <see cref="Create"/> / <see cref="WithLeftBands"/> / <see cref="WithRightBands"/>
    /// 等工厂方法产出新实例；字段一旦赋值，外部无法再通过 LeftBands / RightBands 修改（返回只读列表）。
    ///
    /// 顺序约定：<see cref="LeftBands"/>、<see cref="RightBands"/> 均 <b>从中心向外</b>排列，
    /// 这让"向左加一条最外围"和"向右加一条最外围"操作都是 <c>.Add</c>（符合直觉）。
    ///
    /// <para>
    /// v2 扩展（中心线位置 / 高程偏移 / 空装配 / 起止桩号）：
    /// 旧 <see cref="Create"/> 签名上以可选参数引入，旧调用方仅传必填字段即可，新字段使用安全默认值。
    /// </para>
    /// </summary>
    public sealed class CrossSectionLayout
    {
        /// <summary>左半路幅条带（从中心向外）。</summary>
        public IReadOnlyList<CrossSectionBand> LeftBands { get; }

        /// <summary>右半路幅条带（从中心向外）。</summary>
        public IReadOnlyList<CrossSectionBand> RightBands { get; }

        /// <summary>中央分隔带宽度（m）。为 0 时不画中分带。</summary>
        public double CenterMedianWidth { get; }

        /// <summary>设计速度（km/h），供 <see cref="Services.Road.CrossSectionCodeChecker"/> 查表。</summary>
        public int DesignSpeed { get; }

        /// <summary>比例尺分母：1:100 → 100；1:200 → 200。影响绘图层字高、尺寸线偏移。</summary>
        public int ScaleDenominator { get; }

        /// <summary>显示标题（用于标题栏文字，例如"标准横断面图  1:100"）。</summary>
        public string Title { get; }

        /// <summary>
        /// 标准横断面图顶部“平面带”沿道路方向的长度（m）。
        /// 用于 rCs 标准横断面图的区域 1；默认 6.5 m。
        /// </summary>
        public double PlanStripLength { get; }

        // ==================================== v2 新增字段 ====================================

        /// <summary>
        /// 道路中心线在断面图上的水平位置（m），相对左红线（leftmost = 0）的距离。
        /// 默认为 <c>LeftHalfWidth + CenterMedianWidth / 2</c>，即左半幅 + 中分带半宽。
        /// 用户可显式覆盖（例如非对称设计时把中心线偏向一侧）。
        /// </summary>
        public double CenterlinePosition { get; }

        /// <summary>
        /// 路面纵断面高程偏移（m）。在断面图上把整体几何沿 y 方向上移此值，
        /// 用于把"设计标高"对齐到地形断面（v1 仅作为字段保留，绘图未启用）。
        /// </summary>
        public double ProfileElevationOffset { get; }

        /// <summary>
        /// 是否为"空装配"占位（对应 Civil 3D 的 Empty Assembly）。
        /// true 时 UI 仅显示一条中心线参考，不生成几何。供未来 Corridor 仅做参考线用。
        /// </summary>
        public bool IsEmptyAssembly { get; }

        /// <summary>
        /// 起始桩号（m）。例如 K0+000 → 0；K1+200 → 1200。
        /// 用于在标题栏与图签上标注，几何不参与计算。
        /// </summary>
        public double StationStart { get; }

        /// <summary>
        /// 终止桩号（m）。<see cref="StationEnd"/> ≥ <see cref="StationStart"/>。
        /// </summary>
        public double StationEnd { get; }

        /// <summary>除 <see cref="StationStart"/>/<see cref="StationEnd"/> 外的附加桩号区间（与主段并列，多段时用于绑定多套断面）。</summary>
        public IReadOnlyList<StationRangeSpan> AdditionalStationRanges { get; }

        /// <summary>中分带左半幅宽度（m）。为 0 时几何上按 <c>CenterMedianWidth / 2</c> 等分；须 ≤ <see cref="CenterMedianWidth"/>。</summary>
        public double MedianLeftSubWidth { get; }

        /// <summary>中分带左半（靠左半幅—中心缝）的横坡（%）。</summary>
        public double MedianLeftCrossSlopePct { get; }

        /// <summary>中分带右半（中心缝—右半幅）的横坡（%）。</summary>
        public double MedianRightCrossSlopePct { get; }

        /// <summary>中分左半在靠左半幅侧边缘的高差跳变（m）。</summary>
        public double MedianLeftOuterElevationDiff { get; }

        /// <summary>中分左半在中心缝侧（与左半内缘重合）的附加高差（m），与 <see cref="MedianRightInnerElevationDiff"/> 在缝处叠加。</summary>
        public double MedianLeftInnerElevationDiff { get; }

        /// <summary>中分右半在中心缝侧的附加高差（m），与 <see cref="MedianLeftInnerElevationDiff"/> 在缝处叠加。</summary>
        public double MedianRightInnerElevationDiff { get; }

        /// <summary>中分右半在靠右半幅侧边缘的跳变（m）。</summary>
        public double MedianRightOuterElevationDiff { get; }

        /// <summary>UI 高差锁定：为 true 时仅人行道/条带中分带 等例外类型可编辑 内/外 端高差（见设计器规则）。</summary>
        public bool ElevationDiffLocked { get; }

        private CrossSectionLayout(
            IReadOnlyList<CrossSectionBand> leftBands,
            IReadOnlyList<CrossSectionBand> rightBands,
            double centerMedianWidth,
            int designSpeed,
            int scaleDenominator,
            string title,
            double planStripLength,
            double centerlinePosition,
            double profileElevationOffset,
            bool isEmptyAssembly,
            double stationStart,
            double stationEnd,
            IReadOnlyList<StationRangeSpan> additionalStationRanges,
            double medianLeftSubWidth,
            double medianLeftCrossSlopePct,
            double medianRightCrossSlopePct,
            double medianLeftOuterElevationDiff,
            double medianLeftInnerElevationDiff,
            double medianRightInnerElevationDiff,
            double medianRightOuterElevationDiff,
            bool elevationDiffLocked)
        {
            LeftBands = leftBands;
            RightBands = rightBands;
            CenterMedianWidth = centerMedianWidth;
            DesignSpeed = designSpeed;
            ScaleDenominator = scaleDenominator;
            Title = title ?? string.Empty;
            PlanStripLength = planStripLength;
            CenterlinePosition = centerlinePosition;
            ProfileElevationOffset = profileElevationOffset;
            IsEmptyAssembly = isEmptyAssembly;
            StationStart = stationStart;
            StationEnd = stationEnd;
            AdditionalStationRanges = additionalStationRanges ?? Array.Empty<StationRangeSpan>();
            MedianLeftSubWidth = medianLeftSubWidth;
            MedianLeftCrossSlopePct = medianLeftCrossSlopePct;
            MedianRightCrossSlopePct = medianRightCrossSlopePct;
            MedianLeftOuterElevationDiff = medianLeftOuterElevationDiff;
            MedianLeftInnerElevationDiff = medianLeftInnerElevationDiff;
            MedianRightInnerElevationDiff = medianRightInnerElevationDiff;
            MedianRightOuterElevationDiff = medianRightOuterElevationDiff;
            ElevationDiffLocked = elevationDiffLocked;
        }

        /// <summary>
        /// 常规构造：从可变列表拷贝为只读视图，同时做合法性检查。
        ///
        /// <para>
        /// 旧调用方仅需传前 6 个参数；v2 新字段用可选参数追加，所有旧 JSON 与旧测试均兼容。
        /// </para>
        /// </summary>
        /// <param name="centerlinePosition">
        /// 中心线水平位置（m）。传 <see cref="double.NaN"/>（默认）将自动解算为
        /// <c>LeftHalfWidth + CenterMedianWidth / 2</c>。
        /// </param>
        public static CrossSectionLayout Create(
            IReadOnlyList<CrossSectionBand> leftBands,
            IReadOnlyList<CrossSectionBand> rightBands,
            double centerMedianWidth,
            int designSpeed,
            int scaleDenominator = 100,
            string title = "标准横断面图",
            double planStripLength = 6.5,
            double centerlinePosition = double.NaN,
            double profileElevationOffset = 0,
            bool isEmptyAssembly = false,
            double stationStart = 0,
            double stationEnd = 0,
            IReadOnlyList<StationRangeSpan> additionalStationRanges = null,
            double medianLeftSubWidth = 0,
            double medianLeftCrossSlopePct = 0,
            double medianRightCrossSlopePct = 0,
            double medianLeftOuterElevationDiff = 0,
            double medianLeftInnerElevationDiff = 0,
            double medianRightInnerElevationDiff = 0,
            double medianRightOuterElevationDiff = 0,
            bool elevationDiffLocked = true)
        {
            if (leftBands == null) throw new ArgumentNullException(nameof(leftBands));
            if (rightBands == null) throw new ArgumentNullException(nameof(rightBands));
            if (double.IsNaN(centerMedianWidth) || double.IsInfinity(centerMedianWidth) || centerMedianWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(centerMedianWidth), $"中央分隔带宽度必须 ≥ 0，当前 {centerMedianWidth}。");
            if (designSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(designSpeed), $"设计速度必须 > 0，当前 {designSpeed}。");
            if (scaleDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(scaleDenominator), $"比例分母必须 > 0，当前 {scaleDenominator}。");
            if (double.IsNaN(planStripLength) || double.IsInfinity(planStripLength) || planStripLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(planStripLength), $"平面带长度必须 > 0，当前 {planStripLength}。");
            if (double.IsInfinity(profileElevationOffset))
                throw new ArgumentOutOfRangeException(nameof(profileElevationOffset), $"高程偏移必须为有限值，当前 {profileElevationOffset}。");
            if (double.IsInfinity(stationStart))
                throw new ArgumentOutOfRangeException(nameof(stationStart), $"起始桩号必须为有限值，当前 {stationStart}。");
            if (double.IsInfinity(stationEnd))
                throw new ArgumentOutOfRangeException(nameof(stationEnd), $"终止桩号必须为有限值，当前 {stationEnd}。");

            double wSub = medianLeftSubWidth;
            if (centerMedianWidth > 1e-9 && wSub > 0)
            {
                if (wSub > centerMedianWidth) wSub = centerMedianWidth;
            }
            else if (centerMedianWidth > 1e-9)
            {
                wSub = 0; // 0 → 几何解析为半宽
            }

            var left = new List<CrossSectionBand>(leftBands.Count);
            foreach (var b in leftBands)
            {
                // 自动修正 Side：左半路幅所有条带 Side = Left（哪怕用户传了错的，我们不报错，静默修正）
                left.Add(b.Side == BandSide.Left ? b : b.WithSide(BandSide.Left));
            }

            var right = new List<CrossSectionBand>(rightBands.Count);
            foreach (var b in rightBands)
            {
                right.Add(b.Side == BandSide.Right ? b : b.WithSide(BandSide.Right));
            }

            // 默认中心线位置 = 左半宽 + 中分带半宽（与对称布置等效）
            double leftWidth = 0;
            foreach (var b in left) leftWidth += b.Width;
            double resolvedCenterline = double.IsNaN(centerlinePosition)
                ? leftWidth + centerMedianWidth / 2.0
                : centerlinePosition;

            return new CrossSectionLayout(
                left.AsReadOnly(), right.AsReadOnly(),
                centerMedianWidth, designSpeed, scaleDenominator, title, planStripLength,
                resolvedCenterline, profileElevationOffset, isEmptyAssembly, stationStart, stationEnd,
                additionalStationRanges,
                wSub,
                medianLeftCrossSlopePct,
                medianRightCrossSlopePct,
                medianLeftOuterElevationDiff,
                medianLeftInnerElevationDiff,
                medianRightInnerElevationDiff,
                medianRightOuterElevationDiff,
                elevationDiffLocked);
        }

        // ==================================== 计算属性 ====================================

        /// <summary>左半路幅总宽（m）。</summary>
        public double LeftHalfWidth
        {
            get
            {
                double w = 0;
                foreach (var b in LeftBands) w += b.Width;
                return w;
            }
        }

        /// <summary>右半路幅总宽（m）。</summary>
        public double RightHalfWidth
        {
            get
            {
                double w = 0;
                foreach (var b in RightBands) w += b.Width;
                return w;
            }
        }

        /// <summary>红线总宽 = 左半 + 中分带 + 右半（m）。</summary>
        public double TotalWidth => LeftHalfWidth + CenterMedianWidth + RightHalfWidth;

        /// <summary>左右是否对称（以 0.01 m 为容差比较半幅宽）。</summary>
        public bool IsSymmetric => Math.Abs(LeftHalfWidth - RightHalfWidth) <= 0.01;

        /// <summary>桩号长度（m），可能为 0（仅作为占位的横断面图，未指定起止桩号）。</summary>
        public double StationLength => Math.Max(0, StationEnd - StationStart);

        // ==================================== 不变式改写 ====================================

        public CrossSectionLayout WithLeftBands(IReadOnlyList<CrossSectionBand> leftBands)
            => Create(leftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      double.NaN /* 让中心线随新左半宽自动重算 */,
                      ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithRightBands(IReadOnlyList<CrossSectionBand> rightBands)
            => Create(LeftBands, rightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithCenterMedianWidth(double w)
            => Create(LeftBands, RightBands, w, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      double.NaN /* 中分带宽变了，中心线重算 */,
                      ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithDesignSpeed(int speed)
            => Create(LeftBands, RightBands, CenterMedianWidth, speed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithScale(int denom)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, denom, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithTitle(string title)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithPlanStripLength(double planStripLength)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, planStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithCenterlinePosition(double centerlinePosition)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      centerlinePosition, ProfileElevationOffset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithProfileElevationOffset(double offset)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, offset, IsEmptyAssembly, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithIsEmptyAssembly(bool isEmpty)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, isEmpty, StationStart, StationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithStations(double stationStart, double stationEnd)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, stationStart, stationEnd, AdditionalStationRanges,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public CrossSectionLayout WithStationRanges(double stationStart, double stationEnd, IReadOnlyList<StationRangeSpan> additional)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title, PlanStripLength,
                      CenterlinePosition, ProfileElevationOffset, IsEmptyAssembly, stationStart, stationEnd, additional,
                      MedianLeftSubWidth, MedianLeftCrossSlopePct, MedianRightCrossSlopePct,
                      MedianLeftOuterElevationDiff, MedianLeftInnerElevationDiff, MedianRightInnerElevationDiff, MedianRightOuterElevationDiff,
                      ElevationDiffLocked);

        public override string ToString()
            => $"CrossSectionLayout[TotalWidth={TotalWidth:F3}m, Speed={DesignSpeed}km/h, Scale=1:{ScaleDenominator}]";
    }
}
