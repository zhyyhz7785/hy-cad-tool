using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Tests
{
    /// <summary>
    /// Phase 7：纯 Domain 金标 fixture。
    /// 顶点单位均为「实际 mm」（与 AutoCAD ModelSpace 一致），不依赖 AutoCAD API。
    /// 用 <see cref="DefaultConfig"/> 提供与运行时 AdapterFromSettingsPanel 等价的配置（Scale=40 默认）。
    /// </summary>
    public static class NewDdsTestFixtures
    {
        public sealed class Fixture
        {
            public string Name { get; set; }
            public Polyline2D Polyline { get; set; }
            public NewDdsConfig Config { get; set; }
        }

        public static NewDdsConfig DefaultConfig() => new NewDdsConfig
        {
            DimensionDistanceInside = 6 * 40,
            DimensionDistanceOutside = 14 * 40,
            DimensionDistanceWithDim = 6 * 40,
            DimDistanceTolerance = 30 * 40,
            GenerateOutsideTotalDimension = true,
            StepStrategy = StepStrategy.Adaptive,
            FixedStepValue = 20.0,
            BulgeTessellatePrecision = 10.0,
            SafetyIterationLimit = 1000
        };

        public static IReadOnlyList<Fixture> All => new[]
        {
            Rect(),
            LShape(),
            TShape(),
            DoubleNotch(),
            Asymmetric()
        };

        // 01：6000 × 4000 矩形
        private static Fixture Rect() => new Fixture
        {
            Name = "01-rect",
            Polyline = new Polyline2D(new[]
            {
                new Point2D(0, 0), new Point2D(6000, 0),
                new Point2D(6000, 4000), new Point2D(0, 4000)
            }, isClosed: true),
            Config = DefaultConfig()
        };

        // 02：L 形（外凸右上）
        private static Fixture LShape() => new Fixture
        {
            Name = "02-l-shape",
            Polyline = new Polyline2D(new[]
            {
                new Point2D(0, 0), new Point2D(8000, 0),
                new Point2D(8000, 3000), new Point2D(5000, 3000),
                new Point2D(5000, 6000), new Point2D(0, 6000)
            }, isClosed: true),
            Config = DefaultConfig()
        };

        // 03：T 形（中部 1500 凸起，宽 2000）
        private static Fixture TShape() => new Fixture
        {
            Name = "03-t-shape",
            Polyline = new Polyline2D(new[]
            {
                new Point2D(0, 0), new Point2D(8000, 0),
                new Point2D(8000, 2000),
                new Point2D(5000, 2000), new Point2D(5000, 3500),
                new Point2D(3000, 3500), new Point2D(3000, 2000),
                new Point2D(0, 2000)
            }, isClosed: true),
            Config = DefaultConfig()
        };

        // 04：底部双凹槽（用户图1简化版：两 1700×950 凹槽）
        private static Fixture DoubleNotch() => new Fixture
        {
            Name = "04-double-notch",
            Polyline = new Polyline2D(new[]
            {
                new Point2D(0, 0), new Point2D(10000, 0),
                new Point2D(10000, 5000), new Point2D(0, 5000),
                new Point2D(0, 1700),
                // 凹槽 1
                new Point2D(2000, 1700), new Point2D(2000, 0),
                new Point2D(2950, 0), new Point2D(2950, 1700),
                // 凹槽 2
                new Point2D(5000, 1700), new Point2D(5000, 0),
                new Point2D(5950, 0), new Point2D(5950, 1700)
            }, isClosed: true),
            Config = DefaultConfig()
        };

        // 05：不对称 L（一侧凸起一侧斜变台阶）
        private static Fixture Asymmetric() => new Fixture
        {
            Name = "05-asymmetric",
            Polyline = new Polyline2D(new[]
            {
                new Point2D(0, 0), new Point2D(7000, 0),
                new Point2D(7000, 1500), new Point2D(5500, 1500),
                new Point2D(5500, 3000), new Point2D(4000, 3000),
                new Point2D(4000, 4500), new Point2D(0, 4500)
            }, isClosed: true),
            Config = DefaultConfig()
        };
    }
}
