using System.Collections.Generic;
using HyCADTool.Features.Settlement;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class Gb50007SettlementDepthTests
    {
        [Theory]
        [InlineData(0, 0.3)]
        [InlineData(-1, 0.3)]
        [InlineData(2, 0.3)]
        [InlineData(2.01, 0.6)]
        [InlineData(4, 0.6)]
        [InlineData(4.01, 0.8)]
        [InlineData(8, 0.8)]
        [InlineData(8.01, 1.0)]
        public void DeltaZ_follows_table_5_3_7(double b, double expected)
        {
            Assert.Equal(expected, Gb50007SettlementDepth.DeltaZ(b), 9);
        }

        [Fact]
        public void Example_100_1_1_1_includes_first_two_layers_when_each_thickness_equals_deltaZ()
        {
            // b=2m → Δz=0.3m。每层厚度等于 Δz，条带正好盖住该层整层沉降。
            // n=1：100 ≰ 0.025×100；n=2：1 ≤ 0.025×101。
            double dz = Gb50007SettlementDepth.DeltaZ(2);
            var layers = new List<Gb50007SettlementDepth.LayerSlice>
            {
                new Gb50007SettlementDepth.LayerSlice(dz, 100),
                new Gb50007SettlementDepth.LayerSlice(dz, 1),
                new Gb50007SettlementDepth.LayerSlice(dz, 1),
                new Gb50007SettlementDepth.LayerSlice(dz, 1),
            };

            var decision = Gb50007SettlementDepth.Resolve(layers, foundationWidthB: 2);

            Assert.True(decision.Satisfied);
            Assert.Equal(2, decision.IncludedCount);
            Assert.Equal(1, decision.BandSettlement, 9);
            Assert.Equal(101, decision.CumulativeSum, 9);
            Assert.True(decision.Ratio <= 0.025 + 1e-9);
        }

        [Fact]
        public void Thin_layers_keep_the_100_mm_layer_inside_the_deltaZ_band()
        {
            // 每层 0.1m < Δz=0.3m，n=2 的条带回吃到 100mm 那层，四层都不满足，全部保留。
            var layers = new List<Gb50007SettlementDepth.LayerSlice>
            {
                new Gb50007SettlementDepth.LayerSlice(0.1, 100),
                new Gb50007SettlementDepth.LayerSlice(0.1, 1),
                new Gb50007SettlementDepth.LayerSlice(0.1, 1),
                new Gb50007SettlementDepth.LayerSlice(0.1, 1),
            };

            var decision = Gb50007SettlementDepth.Resolve(layers, foundationWidthB: 2);

            Assert.False(decision.Satisfied);
            Assert.Equal(4, decision.IncludedCount);
        }

        [Fact]
        public void Custom_ratio_is_used_by_the_rebound_path()
        {
            double dz = Gb50007SettlementDepth.DeltaZ(2);
            var layers = new List<Gb50007SettlementDepth.LayerSlice>
            {
                new Gb50007SettlementDepth.LayerSlice(dz, 100),
                new Gb50007SettlementDepth.LayerSlice(dz, 1),
            };

            var loose = Gb50007SettlementDepth.Resolve(layers, 2, ratio: 1.0);
            Assert.True(loose.Satisfied);
            Assert.Equal(1, loose.IncludedCount);
        }
    }
}
