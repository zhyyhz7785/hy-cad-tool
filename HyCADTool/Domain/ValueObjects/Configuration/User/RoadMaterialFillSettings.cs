using System.Collections.Generic;

namespace HyCADTool.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 持久化在 <c>hy-settings.json</c> 中的横断面填料候选自定义项。
    /// 仅记录相对系统默认的增量：用户新增项 + 被用户删除的默认项。
    /// </summary>
    public sealed class RoadMaterialFillSettings
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
