namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    public class MLeaderStyleConfig
    {
        public MLeaderStyleConfig()
        {
        }

        public MLeaderStyleConfig(string name, string textStyleName)
        {
            Name = name;
            TextStyleName = textStyleName;
        }

        public string Name { get; set; } = "0_Hy_40_Mleader";
        public string TextStyleName { get; set; } = "0_Hy_40";

        public static MLeaderStyleConfig CreateDefault(string name, string textStyleName)
        {
            return new MLeaderStyleConfig(name, textStyleName);
        }
    }
}
