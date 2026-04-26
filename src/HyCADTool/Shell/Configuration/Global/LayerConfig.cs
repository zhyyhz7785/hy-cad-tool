namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>CSV/旧格式中的单层描述。</summary>
    public class LayerConfig
    {
        public string Name { get; set; }
        public short ColorIndex { get; set; }
    }
}
