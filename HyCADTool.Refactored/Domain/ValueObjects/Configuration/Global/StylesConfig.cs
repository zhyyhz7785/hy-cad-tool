namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 样式配置集合
    /// 包含所有 CAD 样式的配置
    /// </summary>
    public class StylesConfig
    {
        /// <summary>
        /// 文本样式配置
        /// </summary>
        public TextStyleConfig TextStyle { get; set; }

        /// <summary>
        /// 标注样式配置
        /// </summary>
        public DimensionStyleConfig DimensionStyle { get; set; }

        /// <summary>
        /// 多重引线样式配置
        /// </summary>
        public MLeaderStyleConfig MLeaderStyle { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public StylesConfig()
        {
            TextStyle = TextStyleConfig.CreateDefault("0_Hy_40", 1.0);
            DimensionStyle = DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0);
            MLeaderStyle = MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40");
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (TextStyle == null)
            {
                error = "文本样式配置不能为空";
                return false;
            }

            if (DimensionStyle == null)
            {
                error = "标注样式配置不能为空";
                return false;
            }

            if (MLeaderStyle == null)
            {
                error = "多重引线样式配置不能为空";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置（基于比例）
        /// </summary>
        public static StylesConfig CreateDefault(double scale = 40.0)
        {
            string textStyleName = $"0_Hy_{scale:F0}";
            
            return new StylesConfig
            {
                TextStyle = TextStyleConfig.CreateDefault(textStyleName, 1.0),
                DimensionStyle = DimensionStyleConfig.CreateDefault($"{textStyleName}_Dim", textStyleName, 1.0),
                MLeaderStyle = MLeaderStyleConfig.CreateDefault($"{textStyleName}_Mleader", textStyleName)
            };
        }

        public override string ToString()
        {
            return $"StylesConfig[Text={TextStyle?.Name}, Dim={DimensionStyle?.Name}, MLeader={MLeaderStyle?.Name}]";
        }
    }
}

