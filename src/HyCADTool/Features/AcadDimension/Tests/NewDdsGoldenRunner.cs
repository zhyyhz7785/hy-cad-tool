using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Context;
using HyCADTool.Features.AcadDimension.Domain.Derivation;
using HyCADTool.Features.AcadDimension.Domain.PostProcessing;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Features.AcadDimension.Validation;
using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Tests
{
    /// <summary>
    /// Phase 7 纯 Domain 金标 Runner——不开 Transaction、不写 ModelSpace；
    /// 只跑 Extractor + Deriver + PostProcessor + QualityFunction，产出 <see cref="NewDdsGoldenSnapshot"/>。
    /// 用于 CI/回归保护：重构 Domain 后跑一遍，看 baseline 差异。
    /// </summary>
    public sealed class NewDdsGoldenRunner
    {
        private readonly IBoundaryFeatureExtractor _extractor = new SweepLineFeatureExtractor();
        private readonly IDimensionDeriver _deriver = new CompositeDimensionDeriver(
            new OutsideDimensionDeriver(),
            new InsideDimensionDeriver());
        private readonly IDimensionPostProcessor _postProcessor = new CompositeDimensionPostProcessor(
            new NearbyParallelMergePostProcessor(),
            new EqualExtensionLengthPostProcessor());
        private readonly IQualityFunction _quality = new FullQualityFunction();

        public NewDdsGoldenSnapshot Run(NewDdsTestFixtures.Fixture fixture)
        {
            var ctx = new NewDdsContext { Boundary = fixture.Polyline, Bounds = default(BoundingBox) };
            var feats = _extractor.Extract(fixture.Polyline, fixture.Config, ctx);
            var raw = _deriver.Derive(feats, fixture.Config);
            var clean = _postProcessor.Process(raw, fixture.Config);
            var result = new NewDdsResult { Dimensions = clean };
            result.ScoreCard = _quality.Evaluate(result, feats);

            var snap = new NewDdsGoldenSnapshot
            {
                Fixture = fixture.Name,
                VertexCount = fixture.Polyline.VertexCount,
                HorizontalColumns = feats.HorizontalSecantColumns?.Count ?? 0,
                HorizontalEdges = SumEdges(feats.HorizontalSecantColumns),
                VerticalColumns = feats.VerticalSecantColumns?.Count ?? 0,
                VerticalEdges = SumEdges(feats.VerticalSecantColumns),
                RawDimensions = raw.Count,
                CleanDimensions = clean.Count,
                Coverage = result.ScoreCard.Coverage,
                NoOverlap = result.ScoreCard.NoOverlap,
                WithinBounds = result.ScoreCard.WithinBounds,
                Consistency = result.ScoreCard.Consistency,
                DirectionalCoverage = result.ScoreCard.DirectionalCoverage,
                Overall = result.ScoreCard.Overall
            };
            foreach (var grp in clean.GroupBy(d => d.Source))
                snap.SourceDistribution[grp.Key] = grp.Count();
            return snap;
        }

        public IList<NewDdsGoldenSnapshot> RunAll()
            => NewDdsTestFixtures.All.Select(Run).ToList();

        private static int SumEdges(IReadOnlyList<IReadOnlyList<Line2D>> cols)
        {
            if (cols == null) return 0;
            int t = 0;
            for (int i = 0; i < cols.Count; i++) t += cols[i]?.Count ?? 0;
            return t;
        }
    }
}
