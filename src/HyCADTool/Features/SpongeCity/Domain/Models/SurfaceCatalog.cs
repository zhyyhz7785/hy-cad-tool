using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.SpongeCity.Domain.Standards;

namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>
    /// 京津冀默认下垫面，供仍直接读目录的调用方。
    /// 图层名是短码 HY-海绵-S1…S10，不再用中文名当图层。
    /// 计算和画线以当前规范（<see cref="SpongeStandardRegistry"/>）为准。
    /// </summary>
    public sealed class SurfaceCatalogEntry
    {
        public string Code { get; }
        public string Name { get; }
        public string LayerName { get; }
        public double DefaultPsi { get; }
        public short AciColor { get; }
        public string Note { get; }

        public SurfaceCatalogEntry(string code, string name, double defaultPsi, short aciColor, string note)
            : this(code, name, defaultPsi, aciColor, note, SpongeLayerNames.Surface(code))
        {
        }

        public SurfaceCatalogEntry(string code, string name, double defaultPsi, short aciColor, string note, string layerName)
        {
            Code = code;
            Name = name;
            DefaultPsi = defaultPsi;
            AciColor = aciColor;
            Note = note;
            LayerName = string.IsNullOrEmpty(layerName) ? SpongeLayerNames.Surface(code) : layerName;
        }

        /// <summary>文字图层 = 短码图层 + 「-文字」。</summary>
        public string TextLayerName => SpongeLayerNames.Text(LayerName);
    }

    public static class SurfaceCatalog
    {
        public static IReadOnlyList<SurfaceCatalogEntry> Entries
        {
            get
            {
                return SpongeStandardRegistry.GetOrDefault(SpongeStandardIds.Db13).Surfaces
                    .Select(s => new SurfaceCatalogEntry(s.Code, s.Name, s.DefaultPsi, s.AciColor, s.Note, s.LayerName))
                    .ToList();
            }
        }
    }
}
