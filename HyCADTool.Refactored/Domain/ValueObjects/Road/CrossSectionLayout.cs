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

        private CrossSectionLayout(
            IReadOnlyList<CrossSectionBand> leftBands,
            IReadOnlyList<CrossSectionBand> rightBands,
            double centerMedianWidth,
            int designSpeed,
            int scaleDenominator,
            string title)
        {
            LeftBands = leftBands;
            RightBands = rightBands;
            CenterMedianWidth = centerMedianWidth;
            DesignSpeed = designSpeed;
            ScaleDenominator = scaleDenominator;
            Title = title ?? string.Empty;
        }

        /// <summary>
        /// 常规构造：从可变列表拷贝为只读视图，同时做合法性检查。
        /// </summary>
        public static CrossSectionLayout Create(
            IReadOnlyList<CrossSectionBand> leftBands,
            IReadOnlyList<CrossSectionBand> rightBands,
            double centerMedianWidth,
            int designSpeed,
            int scaleDenominator = 100,
            string title = "标准横断面图")
        {
            if (leftBands == null) throw new ArgumentNullException(nameof(leftBands));
            if (rightBands == null) throw new ArgumentNullException(nameof(rightBands));
            if (double.IsNaN(centerMedianWidth) || double.IsInfinity(centerMedianWidth) || centerMedianWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(centerMedianWidth), $"中央分隔带宽度必须 ≥ 0，当前 {centerMedianWidth}。");
            if (designSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(designSpeed), $"设计速度必须 > 0，当前 {designSpeed}。");
            if (scaleDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(scaleDenominator), $"比例分母必须 > 0，当前 {scaleDenominator}。");

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

            return new CrossSectionLayout(left.AsReadOnly(), right.AsReadOnly(),
                centerMedianWidth, designSpeed, scaleDenominator, title);
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

        // ==================================== 不变式改写 ====================================

        public CrossSectionLayout WithLeftBands(IReadOnlyList<CrossSectionBand> leftBands)
            => Create(leftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title);

        public CrossSectionLayout WithRightBands(IReadOnlyList<CrossSectionBand> rightBands)
            => Create(LeftBands, rightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, Title);

        public CrossSectionLayout WithCenterMedianWidth(double w)
            => Create(LeftBands, RightBands, w, DesignSpeed, ScaleDenominator, Title);

        public CrossSectionLayout WithDesignSpeed(int speed)
            => Create(LeftBands, RightBands, CenterMedianWidth, speed, ScaleDenominator, Title);

        public CrossSectionLayout WithScale(int denom)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, denom, Title);

        public CrossSectionLayout WithTitle(string title)
            => Create(LeftBands, RightBands, CenterMedianWidth, DesignSpeed, ScaleDenominator, title);

        public override string ToString()
            => $"CrossSectionLayout[TotalWidth={TotalWidth:F3}m, Speed={DesignSpeed}km/h, Scale=1:{ScaleDenominator}]";
    }
}
