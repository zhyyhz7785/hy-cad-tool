using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCADTool.Features.DCEL.Domain.Services;
using HyCADTool.Shared.AutoCAD.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// DCEL 管线执行器（提取 → 构建 → 渲染，供命令层复用）
    /// </summary>
    public static class DCELPipelineRunner
    {
        public sealed class RunOutput
        {
            public DCELRunMetrics Metrics { get; set; }
            public DCELGraph Graph { get; set; }
            public List<SimplifiedCurveMapping> Mappings { get; set; }
        }

        public static RunOutput Run(
            IReadOnlyList<ObjectId> curveIds,
            DCELSettings settings,
            ICurveSegmentExtractor extractor,
            IDCELBuilderService builder,
            IDCELRenderer renderer,
            bool render,
            bool validateTopology = false,
            string outerLayer = "dcelOuter",
            string innerLayer = "dcelInner")
        {
            var metrics = new DCELRunMetrics { Label = "HYDCEL" };
            metrics.EntityCount = curveIds.Count;

            var totalSw = Stopwatch.StartNew();

            var swCollect = Stopwatch.StartNew();
            var ids = new List<ObjectId>(curveIds);
            swCollect.Stop();
            metrics.CollectMs = swCollect.ElapsedMilliseconds;

            var tolerance = settings.Tolerance;

            var swExtract = Stopwatch.StartNew();
            var simplificationService = new HyCAD.Geometry.Algorithms.CurveSimplificationService();
            var (segments, mappings) = extractor.ExtractAndSimplify(
                ids,
                simplificationService,
                tolerance,
                arcSegmentCount: settings.ArcSegmentCount,
                ellipseSegmentCount: settings.EllipseSegmentCount,
                splineSegmentCount: settings.SplineSegmentCount);
            swExtract.Stop();
            metrics.ExtractMs = swExtract.ElapsedMilliseconds;
            metrics.SegmentCount = segments.Count;
            metrics.MappingCount = mappings.Count;

            var swBuild = Stopwatch.StartNew();
            var graph = builder.BuildFromSegments(segments, new Tolerance(tolerance));
            swBuild.Stop();
            metrics.BuildMs = swBuild.ElapsedMilliseconds;

            var swStats = Stopwatch.StartNew();
            FillGraphStats(metrics, graph);
            swStats.Stop();
            metrics.StatsMs = swStats.ElapsedMilliseconds;

            var swValidate = Stopwatch.StartNew();
            if (validateTopology && graph != null)
            {
                if (!graph.Validate(out var errors))
                {
                    metrics.TopologyValid = false;
                    metrics.TopologyErrorCount = errors.Count;
                }
            }
            swValidate.Stop();
            metrics.ValidateMs = swValidate.ElapsedMilliseconds;

            if (render && graph != null && graph.Faces.Count > 0)
            {
                var swRender = Stopwatch.StartNew();
                if (mappings.Count > 0)
                {
                    renderer.RenderWithMappings(
                        graph, mappings, outerLayer, innerLayer,
                        restoreOriginal: settings.RestoreOriginalCurves,
                        tolerance: tolerance);
                }
                else
                {
                    renderer.Render(graph, outerLayer, innerLayer);
                }
                swRender.Stop();
                metrics.RenderMs = swRender.ElapsedMilliseconds;
            }

            totalSw.Stop();
            metrics.TotalMs = totalSw.ElapsedMilliseconds;

            return new RunOutput
            {
                Metrics = metrics,
                Graph = graph,
                Mappings = mappings,
            };
        }

        private static void FillGraphStats(DCELRunMetrics metrics, DCELGraph graph)
        {
            if (graph == null)
                return;

            var stats = graph.GetStatistics();
            metrics.VertexCount = stats.VertexCount;
            metrics.EdgeCount = stats.EdgeCount;
            metrics.FaceCount = stats.FaceCount;
            metrics.OuterFaceCount = graph.Faces.Count(f => f.IsOuter);
            metrics.InnerFaceCount = metrics.FaceCount - metrics.OuterFaceCount;
        }
    }
}
