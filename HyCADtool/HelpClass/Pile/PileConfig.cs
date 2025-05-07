using HyCADTool.Config;
using HyCADTool.HelpClass;
using System;
namespace HyCADTool
{
    public class PileConfig
    {
        private static PileConfig _instance;
        private static readonly object _lock = new object();
        private readonly ConfigManager<PileConfigData> _configManager;
        // 指定配置文件路径
        private static readonly string ConfigFilePath = @"E:\BaiduSyncdisk\Code\CSharp\Rebuild1framwork\HyCADToolGpt\HyCADTool\config.json";
        public static PileConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new PileConfig(ConfigFilePath);
                    }
                }
                return _instance;
            }
        }
        private PileConfig(string filePath)
        {
            _configManager = new ConfigManager<PileConfigData>(filePath);
            LoadFromConfig();
        }
        // 属性，与新枚举类型一致
        public PileSectionType Section { get; set; }
        public double DiameterOrEdge { get; set; }
        public PileArrangementType ArrangementType { get; set; }
        public double PileArrangeRate { get; set; }
        public (double up, double down, double left, double right) Margin { get; set; }
        public double MinPileCenterDistance { get; set; }
        public double InputDisplacementRate { get; set; }
        public double InputDistanceFromContour { get; set; }
        private void LoadFromConfig()
        {
            var config = _configManager.Config;
            // 使用新枚举类型 SectionType 和 PileArrangementType
            Section = Enum.TryParse(config.Section.Default, out PileSectionType section) ? section : PileSectionType.Circle;
            DiameterOrEdge = config.DiameterOrEdge.Default;
            ArrangementType = Enum.TryParse(config.ArrangementType.Default, out PileArrangementType arrangement)
                ? arrangement
                : PileArrangementType.Rectangle;
            PileArrangeRate = config.PileArrangeRate.Default;
            Margin = (
                config.Margin.Default.Up,
                config.Margin.Default.Down,
                config.Margin.Default.Left,
                config.Margin.Default.Right
            );
            MinPileCenterDistance = config.MinPileCenterDistance.Default;
            InputDisplacementRate = config.InputDisplacementRate.Default;
            InputDistanceFromContour = config.InputDistanceFromContour.Default;
        }
        public void SaveToConfig()
        {
            var config = _configManager.Config;
            config.Section.Default = Section.ToString();
            config.DiameterOrEdge.Default = DiameterOrEdge;
            config.ArrangementType.Default = ArrangementType.ToString();
            config.PileArrangeRate.Default = PileArrangeRate;
            config.Margin.Default.Up = Margin.up;
            config.Margin.Default.Down = Margin.down;
            config.Margin.Default.Left = Margin.left;
            config.Margin.Default.Right = Margin.right;
            config.MinPileCenterDistance.Default = MinPileCenterDistance;
            config.InputDisplacementRate.Default = InputDisplacementRate;
            config.InputDistanceFromContour.Default = InputDistanceFromContour;
            _configManager.SaveConfig();
        }
    }
}