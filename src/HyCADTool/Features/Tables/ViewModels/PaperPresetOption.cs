using HyCAD.Tables.Layout;

namespace HyCADTool.Features.Tables.ViewModels
{
    public sealed class PaperPresetOption
    {
        public PaperPresetOption(PaperPreset preset, string displayName)
        {
            Preset = preset;
            DisplayName = displayName;
        }

        public PaperPreset Preset { get; }

        public string DisplayName { get; }
    }
}
