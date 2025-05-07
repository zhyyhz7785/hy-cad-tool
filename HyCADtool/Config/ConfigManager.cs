using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace HyCADTool.Config
{
    // 根配置类，包含现有配置和预留扩展空间
    public class RootConfig
    {
        public PileConfigData PileConfigData { get; set; } = new PileConfigData();
        public BaseConfigData BaseConfigData { get; set; } = new BaseConfigData(); // BaseConfigData 和 PileConfigData 平级
    }
    // PileConfigData 配置类
    public class PileConfigData
    {
        public SectionConfig Section { get; set; } = new SectionConfig();
        public DiameterOrEdgeConfig DiameterOrEdge { get; set; } = new DiameterOrEdgeConfig();
        public ArrangementTypeConfig ArrangementType { get; set; } = new ArrangementTypeConfig();
        public PileArrangeRateConfig PileArrangeRate { get; set; } = new PileArrangeRateConfig();
        public MarginConfig Margin { get; set; } = new MarginConfig();
        public MinPileCenterDistanceConfig MinPileCenterDistance { get; set; } = new MinPileCenterDistanceConfig();
        public InputDisplacementRateConfig InputDisplacementRate { get; set; } = new InputDisplacementRateConfig();
        public InputDistanceFromContourConfig InputDistanceFromContour { get; set; } = new InputDistanceFromContourConfig();
    }
    public class SectionConfig
    {
        [JsonProperty("default")]
        public string Default { get; set; } = "Circle";
        public List<string> Options { get; set; } = new List<string>(); // 空列表，依赖初始化
    }
    public class DiameterOrEdgeConfig
    {
        [JsonProperty("default")]
        public double Default { get; set; } = 400.0;
        public List<double> Range { get; set; } = new List<double>(); // 空列表，依赖初始化
    }
    public class ArrangementTypeConfig
    {
        [JsonProperty("default")]
        public string Default { get; set; } = "Rectangle";
        public List<string> Options { get; set; } = new List<string>(); // 空列表，依赖初始化
    }
    public class PileArrangeRateConfig
    {
        [JsonProperty("default")]
        public double Default { get; set; } = 0.5;
        public List<double> Range { get; set; } = new List<double>(); // 空列表，依赖初始化
    }
    public class MarginConfig
    {
        [JsonProperty("default")]
        public MarginDefault Default { get; set; } = new MarginDefault();
        public MarginRange Range { get; set; } = new MarginRange();
    }
    public class MarginDefault
    {
        public double Up { get; set; } = 400.0;
        public double Down { get; set; } = 400.0;
        public double Left { get; set; } = 400.0;
        public double Right { get; set; } = 400.0;
    }
    public class MarginRange
    {
        public double Min { get; set; } = 100.0;
        public double Max { get; set; } = 1000.0;
    }
    public class MinPileCenterDistanceConfig
    {
        [JsonProperty("default")]
        public double Default { get; set; } = 1200.0;
        public List<double> Range { get; set; } = new List<double>(); // 空列表，依赖初始化
    }
    public class InputDisplacementRateConfig
    {
        [JsonProperty("default")]
        public double Default { get; set; } = 0.02;
        public List<double> Range { get; set; } = new List<double>(); // 空列表，依赖初始化
    }
    public class InputDistanceFromContourConfig
    {
        [JsonProperty("default")]
        public double Default { get; set; } = 400.0;
        public List<double> Range { get; set; } = new List<double>(); // 空列表，依赖初始化
    }
    // 优化后的 ConfigManager，支持动态类型处理
    public class ConfigManager<T> where T : class, new()
    {
        private readonly string _filePath;
        private T _config;
        private static readonly PropertyInfo[] RootConfigProperties = typeof(RootConfig).GetProperties(); // 缓存反射结果
        public ConfigManager(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            LoadConfig();
        }
        public T Config => _config;
        public void LoadConfig()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var root = JsonConvert.DeserializeObject<RootConfig>(json);
                    _config = ExtractConfigFromRoot(root) ?? InitializeDefaultConfig();
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to load config: {ex.Message}");
                    _config = InitializeDefaultConfig();
                }
            }
            else
            {
                _config = InitializeDefaultConfig();
                SaveConfig();
            }
        }
        public void SaveConfig()
        {
            var root = new RootConfig();
            AssignConfigToRoot(root, _config);
            var settings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace // 替换现有集合，而不是追加
            };
            string json = JsonConvert.SerializeObject(root, Formatting.Indented, settings);
            File.WriteAllText(_filePath, json);
        }
        private T ExtractConfigFromRoot(RootConfig root)
        {
            if (root == null) return null;
            // 使用缓存的反射结果动态提取与 T 匹配的属性
            foreach (var prop in RootConfigProperties)
            {
                if (prop.PropertyType == typeof(T))
                {
                    return prop.GetValue(root) as T;
                }
            }
            throw new InvalidOperationException($"No property in RootConfig matches type {typeof(T).Name}");
        }
        private void AssignConfigToRoot(RootConfig root, T config)
        {
            // 使用缓存的反射结果动态赋值与 T 匹配的属性
            foreach (var prop in RootConfigProperties)
            {
                if (prop.PropertyType == typeof(T))
                {
                    prop.SetValue(root, config);
                    return;
                }
            }
            throw new InvalidOperationException($"No property in RootConfig matches type {typeof(T).Name}");
        }
        private T InitializeDefaultConfig()
        {
            var config = new T();
            if (config is PileConfigData pileConfig)
            {
                pileConfig.Section.Options.AddRange(new[] { "Circle", "Square" });
                pileConfig.DiameterOrEdge.Range.AddRange(new[] { 200.0, 1200.0 });
                pileConfig.ArrangementType.Options.AddRange(new[] { "Rectangle", "Circular" });
                pileConfig.PileArrangeRate.Range.AddRange(new[] { 0.0, 1.0 });
                pileConfig.MinPileCenterDistance.Range.AddRange(new[] { 500.0, 5000.0 });
                pileConfig.InputDisplacementRate.Range.AddRange(new[] { 0.01, 0.1 });
                pileConfig.InputDistanceFromContour.Range.AddRange(new[] { 200.0, 1200.0 });
            }
            else if (config is BaseConfigData baseConfig)
            {
                baseConfig.TextStyle.Name = $"0_Hy_{BaseConfigData.Scale}";
                baseConfig.TextStyle.BigFontFileName = "hztxt.shx";
                baseConfig.TextStyle.FontFileName = "tssdeng.shx";
                baseConfig.TextStyle.TextSize = 2.5;
                baseConfig.TextStyle.TextXScale = 0.7;
                baseConfig.DimStyle.Name = $"0_Hy_{BaseConfigData.Scale}_Dim";
                baseConfig.DimStyle.TextStyleName = $"0_Hy_{BaseConfigData.Scale}";
                baseConfig.DimStyle.Dimtdec = 0;
                baseConfig.DimStyle.Dimexo = 1.0;
                baseConfig.DimStyle.Dimexe = 1.0;
                baseConfig.DimStyle.Dimdle = 0.5;
                baseConfig.DimStyle.Dimtxt = 2.5;
                baseConfig.DimStyle.Dimgap = 1.0;
                baseConfig.DimStyle.Dimasz = 1.0;
                baseConfig.DimStyle.Dimdec = 0;
                baseConfig.MLeaderStyle.Name = $"0_Hy_{BaseConfigData.Scale}_Mleader";
                baseConfig.MLeaderStyle.TextStyleName = $"0_Hy_{BaseConfigData.Scale}";
            }
            return config;
        }
    }
}