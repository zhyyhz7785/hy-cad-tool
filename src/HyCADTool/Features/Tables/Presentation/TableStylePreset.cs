using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>表样式预设（M2 / 015 S1 子集）。</summary>
    public sealed class TableStylePreset
    {
        public TableStylePreset(string id, string displayName, BorderSet defaultBorder, double defaultTextHeightMm)
        {
            Id = id;
            DisplayName = displayName;
            DefaultBorder = defaultBorder ?? BorderSet.None;
            DefaultTextHeightMm = defaultTextHeightMm <= 0 ? 3.5 : defaultTextHeightMm;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public BorderSet DefaultBorder { get; }

        public double DefaultTextHeightMm { get; }

        public static TableStylePreset EngineeringDefault { get; } =
            new TableStylePreset("engineering", "工程默认", BorderSet.Uniform(0.35), 3.5);

        public static TableStylePreset PersonnelOuterBold { get; } =
            new TableStylePreset("personnel-outer", "人员表外框加粗", BorderSet.Uniform(0.35), 3.5);

        public static TableStylePreset NoBorder { get; } =
            new TableStylePreset("none", "无边框", BorderSet.None, 3.5);
    }
}
