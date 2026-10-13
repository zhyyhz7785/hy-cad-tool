using System.Collections.Generic;

namespace HyCADTool.Shared.Configuration.User
{
    public class UserLayerSettings
    {
        public int Version { get; set; }
        public List<LayerDefinitionItem> Items { get; set; } = new List<LayerDefinitionItem>();
    }
}
