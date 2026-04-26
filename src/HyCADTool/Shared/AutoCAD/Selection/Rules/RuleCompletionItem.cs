namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public sealed class RuleCompletionItem
    {
        public string Kind { get; set; }

        public string DisplayText { get; set; }

        public string InsertText { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
