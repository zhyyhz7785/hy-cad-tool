using HyCADTool.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters
{
    /// <summary>
    /// 配置服务适配器实现
    /// 桥接原项目的 IConfigService 接口
    /// </summary>
    public class ConfigServiceAdapter : IConfigService
    {
        // 内部存储配置值
        private double _scale = 40.0;
        private double _diameterOrEdge = 400.0;
        private double _minPileCenterDistance = 1200.0;
        private double _inputDisplacementRate = 0.02;
        private double _pileArrangeRate = 0.5;
        private double _inputDistanceFromContour = 400.0;
        private (double up, double down, double left, double right) _margin = (400.0, 400.0, 400.0, 400.0);

        public double Scale
        {
            get => _scale;
            set => _scale = value;
        }

        public double DiameterOrEdge
        {
            get => _diameterOrEdge;
            set => _diameterOrEdge = value;
        }

        public double MinPileCenterDistance
        {
            get => _minPileCenterDistance;
            set => _minPileCenterDistance = value;
        }

        public double InputDisplacementRate
        {
            get => _inputDisplacementRate;
            set => _inputDisplacementRate = value;
        }

        public double PileArrangeRate
        {
            get => _pileArrangeRate;
            set => _pileArrangeRate = value;
        }

        public double InputDistanceFromContour
        {
            get => _inputDistanceFromContour;
            set => _inputDistanceFromContour = value;
        }

        public (double up, double down, double left, double right) Margin
        {
            get => _margin;
            set => _margin = value;
        }

        public void InitializeStyle()
        {
            // 初始化样式的简化实现
            // 实际使用时可以调用原项目的样式初始化逻辑
        }
    }
}

