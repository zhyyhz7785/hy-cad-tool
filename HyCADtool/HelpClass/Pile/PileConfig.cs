using HyCADTool.Config;
using HyCADTool.HelpClass;
using System;

namespace HyCADTool.Config
{
    public class PileConfig
    {
        private static PileConfig _instance;
        private static readonly object _lock = new object(); 

        public static PileConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PileConfig();
                            ConfigManager.ImportConfigFromCsv("Pile");
                        }
                    }
                }
                return _instance;
            }
        }

        private PileConfig() { }

        // 属性
        public PileSectionType Section { get; set; } = PileSectionType.Circle;
        public double DiameterOrEdge { get; set; } = 400.0;
        public PileArrangementType ArrangementType { get; set; } = PileArrangementType.Rectangle;
        public double PileArrangeRate { get; set; } = 0.5;
        public (double up, double down, double left, double right) Margin { get; set; } = (400, 400, 400, 400);
        public double MinPileCenterDistance { get; set; } = 1200.0;
        public double InputDisplacementRate { get; set; } = 0.02;
        public double InputDistanceFromContour { get; set; } = 400.0;

        public void LoadFromCsv()
        {
            ConfigManager.ImportConfigFromCsv("Pile");
        }

        public void SaveToCsv()
        {
            ConfigManager.ExportConfigToCsv();
        }
    }
}
