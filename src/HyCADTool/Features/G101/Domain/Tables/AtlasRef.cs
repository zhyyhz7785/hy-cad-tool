namespace HyCADTool.Features.G101.Domain.Tables
{
    /// <summary>图集依据引用（图集号 + 页码）。</summary>
    public sealed class AtlasRef
    {
        public string AtlasId { get; }
        public string Page { get; }
        public string Description { get; }

        public AtlasRef(string atlasId, string page, string description = null)
        {
            AtlasId = atlasId;
            Page = page;
            Description = description;
        }

        public string Display => $"{AtlasId} P{Page}";

        public override string ToString() => Display;
    }
}
