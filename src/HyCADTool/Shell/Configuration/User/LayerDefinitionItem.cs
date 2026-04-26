namespace HyCADTool.Shell.Configuration.User
{
    public class LayerDefinitionItem
    {
        public string SemanticId { get; set; }
        public string Name { get; set; }
        public short AciColor { get; set; }
        public string LinetypeName { get; set; } = "Continuous";
        public int LineWeightRaw { get; set; } = -1;
        public bool IsPlottable { get; set; } = true;
        public bool IsLocked { get; set; }

        public LayerDefinitionItem Clone()
        {
            return new LayerDefinitionItem
            {
                SemanticId = SemanticId,
                Name = Name,
                AciColor = AciColor,
                LinetypeName = LinetypeName,
                LineWeightRaw = LineWeightRaw,
                IsPlottable = IsPlottable,
                IsLocked = IsLocked
            };
        }
    }
}
