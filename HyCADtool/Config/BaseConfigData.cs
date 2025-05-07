// BaseConfigData.cs
using Newtonsoft.Json;
namespace HyCADTool.Config
{
    // BaseConfigData 配置类，包含文字样式、标注样式和引线样式
    // (保持与 BaseConfigData.cs 中的定义一致，这里仅为引用)
    public class BaseConfigData
    {
        public TextStyleConfig TextStyle { get; set; } = new TextStyleConfig();
        public DimStyleConfig DimStyle { get; set; } = new DimStyleConfig();
        public MLeaderStyleConfig MLeaderStyle { get; set; } = new MLeaderStyleConfig();
        public static double Scale { get; set; } = 40; // 默认缩放比例
        // 文字样式配置
        public class TextStyleConfig
        {
            [JsonProperty("name")]
            public string Name { get; set; } = $"0_Hy_{Scale}"; // 默认名称，可根据缩放调整1
            [JsonProperty("bigFontFileName")]
            public string BigFontFileName { get; set; } = "hztxt.shx"; // 大字体文件
            [JsonProperty("fontFileName")]
            public string FontFileName { get; set; } = "tssdeng.shx"; // 普通字体文件            
            [JsonProperty("textSize")]
            public double TextSize { get; set; } = 2.5; // 默认文字高度，与 Reinforcement.TextSize 类似
            [JsonProperty("textXScale")]
            public double TextXScale { get; set; } = 0.7; // 默认 X 方向缩放，与 Reinforcement.TextXScale 类似
        }
        // 标注样式配置
        public class DimStyleConfig
        {
            [JsonProperty("name")]
            public string Name { get; set; } = $"0_Hy_{Scale}_Dim"; // 默认名称，可根据缩放调整
            [JsonProperty("textStyleName")]
            public string TextStyleName { get; set; } = $"0_Hy_{Scale}"; // 关联文字样式名称           
            [JsonProperty("dimtdec")]
            public int Dimtdec { get; set; } = 0; // 公差精度
            [JsonProperty("dimexo")]
            public double Dimexo { get; set; } = 1.0; // 尺寸界线偏移
            [JsonProperty("dimexe")]
            public double Dimexe { get; set; } = 1.0; // 尺寸界线超出量
            [JsonProperty("dimdle")]
            public double Dimdle { get; set; } = 0.5; // 尺寸线超出量
            [JsonProperty("dimtxt")]
            public double Dimtxt { get; set; } = 2.5; // 文字高度
            [JsonProperty("dimgap")]
            public double Dimgap { get; set; } = 1.0; // 文字偏移
            [JsonProperty("dimasz")]
            public double Dimasz { get; set; } = 1.0; // 箭头大小
            [JsonProperty("dimdec")]
            public int Dimdec { get; set; } = 0; // 精度
        }
        // 引线样式配置
        public class MLeaderStyleConfig
        {
            [JsonProperty("name")]
            public string Name { get; set; } = $"0_Hy_{Scale}_Mleader"; // 默认名称，可根据缩放调整
            [JsonProperty("textStyleName")]
            public string TextStyleName { get; set; } = $"0_Hy_{Scale}"; // 关联文字样式名称           
        }
    }
}