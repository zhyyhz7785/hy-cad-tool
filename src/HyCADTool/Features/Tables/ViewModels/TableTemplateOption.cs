namespace HyCADTool.Features.Tables.ViewModels
{
    public enum TableTemplateKind
    {
        Blank,
        Personnel,
        Family,
    }

    public sealed class TableTemplateOption
    {
        public TableTemplateOption(TableTemplateKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName;
        }

        public TableTemplateKind Kind { get; }

        public string DisplayName { get; }
    }
}
