using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>M9.1 IntersectionBandMergeRuleTable 测试。</summary>
    public class IntersectionBandMergeRuleTableTests
    {
        [Fact]
        public void DefaultRule_SameType_GreenStrip_UsesArcConnection()
        {
            var r = IntersectionBandMergeRuleTable.DefaultRule(
                TemplateComponentKind.GreenStrip, TemplateComponentKind.GreenStrip);
            r.Strategy.Should().Be(BandMergeStrategy.ConnectWithArc);
        }

        [Fact]
        public void DefaultRule_SameType_Pavement_UsesBlendByLength()
        {
            var r = IntersectionBandMergeRuleTable.DefaultRule(
                TemplateComponentKind.Pavement, TemplateComponentKind.Pavement);
            r.Strategy.Should().Be(BandMergeStrategy.BlendByLength);
        }

        [Fact]
        public void DefaultRule_HigherPriority_KeepsSelf()
        {
            var r = IntersectionBandMergeRuleTable.DefaultRule(
                TemplateComponentKind.Pavement, TemplateComponentKind.GreenStrip);
            r.Strategy.Should().Be(BandMergeStrategy.KeepSelf);
        }

        [Fact]
        public void DefaultRule_LowerPriority_KeepsOpposite()
        {
            var r = IntersectionBandMergeRuleTable.DefaultRule(
                TemplateComponentKind.GreenStrip, TemplateComponentKind.Pavement);
            r.Strategy.Should().Be(BandMergeStrategy.KeepOpposite);
        }

        [Fact]
        public void DefaultFiveByFive_Produces25Rules()
        {
            var all = IntersectionBandMergeRuleTable.DefaultFiveByFive();
            all.Should().HaveCount(25);
        }

        [Fact]
        public void Override_WinsOverDefault()
        {
            var table = new IntersectionBandMergeRuleTable();
            table.AddOrUpdate(new IntersectionBandMergeRule
            {
                SelfKind = (int)TemplateComponentKind.Pavement,
                OppositeKind = (int)TemplateComponentKind.Sidewalk,
                Strategy = BandMergeStrategy.ConnectWithArc,
            });
            var r = table.Resolve(TemplateComponentKind.Pavement, TemplateComponentKind.Sidewalk);
            r.Strategy.Should().Be(BandMergeStrategy.ConnectWithArc);
        }

        [Fact]
        public void Remove_ReturnsFalseWhenMissing()
        {
            var t = new IntersectionBandMergeRuleTable();
            t.Remove(TemplateComponentKind.GreenStrip, TemplateComponentKind.Sidewalk).Should().BeFalse();
        }

        [Fact]
        public void CtorWithSeed_HasCountMatching()
        {
            var rules = new[]
            {
                new IntersectionBandMergeRule { SelfKind = (int)TemplateComponentKind.Pavement, OppositeKind = (int)TemplateComponentKind.GreenStrip, Strategy = BandMergeStrategy.KeepSelf },
                new IntersectionBandMergeRule { SelfKind = (int)TemplateComponentKind.Sidewalk, OppositeKind = (int)TemplateComponentKind.Pavement, Strategy = BandMergeStrategy.KeepOpposite },
            };
            var t = new IntersectionBandMergeRuleTable(rules);
            t.Count.Should().Be(2);
        }

        [Fact]
        public void Clear_EmptiesOverrides()
        {
            var t = new IntersectionBandMergeRuleTable();
            t.AddOrUpdate(new IntersectionBandMergeRule
            {
                SelfKind = (int)TemplateComponentKind.Pavement,
                OppositeKind = (int)TemplateComponentKind.Pavement,
            });
            t.Clear();
            t.Count.Should().Be(0);
        }

        [Fact]
        public void DefaultRule_EquivalentPriority_DifferentType_UsesArc()
        {
            // Sidewalk (2) × Shoulder (2) → 同优先级不同类型
            var r = IntersectionBandMergeRuleTable.DefaultRule(
                TemplateComponentKind.Sidewalk, TemplateComponentKind.Shoulder);
            r.Strategy.Should().Be(BandMergeStrategy.ConnectWithArc);
        }

        [Fact]
        public void ResolvePairs_4Legs_Produces4PairsCyclic()
        {
            var kinds = new[]
            {
                TemplateComponentKind.Pavement,
                TemplateComponentKind.Sidewalk,
                TemplateComponentKind.Pavement,
                TemplateComponentKind.Sidewalk,
            };
            var t = new IntersectionBandMergeRuleTable();
            var pairs = t.ResolvePairs(kinds);
            pairs.Should().HaveCount(4);
            // 0 → 1: Pavement × Sidewalk → KeepSelf (priority 5 > 2)
            pairs[0].Strategy.Should().Be(BandMergeStrategy.KeepSelf);
            // 1 → 2: Sidewalk × Pavement → KeepOpposite
            pairs[1].Strategy.Should().Be(BandMergeStrategy.KeepOpposite);
        }

        [Fact]
        public void ResolvePairs_TooShort_ReturnsEmpty()
        {
            var t = new IntersectionBandMergeRuleTable();
            t.ResolvePairs(new[] { TemplateComponentKind.Pavement }).Should().BeEmpty();
            t.ResolvePairs(null).Should().BeEmpty();
        }

        [Fact]
        public void PriorityMap_5x5()
        {
            IntersectionBandMergeRuleTable.DefaultPriority[TemplateComponentKind.GreenStrip].Should().Be(1);
            IntersectionBandMergeRuleTable.DefaultPriority[TemplateComponentKind.Sidewalk].Should().Be(2);
            IntersectionBandMergeRuleTable.DefaultPriority[TemplateComponentKind.NonMotorized].Should().Be(3);
            IntersectionBandMergeRuleTable.DefaultPriority[TemplateComponentKind.MedianStrip].Should().Be(4);
            IntersectionBandMergeRuleTable.DefaultPriority[TemplateComponentKind.Pavement].Should().Be(5);
        }
    }
}
