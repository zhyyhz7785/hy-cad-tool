namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// 单次 DCEL 管线运行指标（用于对比报告）
    /// </summary>
    public sealed class DCELRunMetrics
    {
        public string Label { get; set; }

        public long CollectMs { get; set; }
        public long ExtractMs { get; set; }
        public long BuildMs { get; set; }
        public long StatsMs { get; set; }
        public long ValidateMs { get; set; }
        public long RenderMs { get; set; }
        public long TotalMs { get; set; }

        public int EntityCount { get; set; }
        public int SegmentCount { get; set; }
        public int MappingCount { get; set; }

        public int VertexCount { get; set; }
        public int EdgeCount { get; set; }
        public int FaceCount { get; set; }
        public int OuterFaceCount { get; set; }
        public int InnerFaceCount { get; set; }

        public bool TopologyValid { get; set; } = true;
        public int TopologyErrorCount { get; set; }

        public long ComputeMs => CollectMs + ExtractMs + BuildMs + StatsMs + ValidateMs;

        public long OtherMs
        {
            get
            {
                long accounted = CollectMs + ExtractMs + BuildMs + StatsMs + ValidateMs + RenderMs;
                long other = TotalMs - accounted;
                return other > 0 ? other : 0;
            }
        }
    }
}
