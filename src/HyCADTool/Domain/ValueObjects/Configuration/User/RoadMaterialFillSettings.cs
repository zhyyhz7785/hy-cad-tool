using System.Collections.Generic;

namespace HyCADTool.Domain.ValueObjects.Configuration.User
{
    public class RoadMaterialFillSettings
    {
        public int Version { get; set; } = 1;
        public List<string> SurfaceAdded { get; set; } = new List<string>();
        public List<string> SurfaceRemoved { get; set; } = new List<string>();
        public List<string> BaseAdded { get; set; } = new List<string>();
        public List<string> BaseRemoved { get; set; } = new List<string>();
        public List<string> SubbaseAdded { get; set; } = new List<string>();
        public List<string> SubbaseRemoved { get; set; } = new List<string>();
    }
}
