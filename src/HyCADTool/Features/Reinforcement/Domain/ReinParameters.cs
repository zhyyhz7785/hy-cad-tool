namespace HyCADTool.Features.Reinforcement.Domain
{
    /// <summary>
    /// 钢筋参数值对象
    /// 包含钢筋绘制和标注所需的所有参数
    /// </summary>
    public class ReinParameters
    {
        #region 基础参数

        /// <summary>
        /// 主图形比例
        /// </summary>
        public double Scale { get; set; }

        /// <summary>
        /// 钢筋直径
        /// </summary>
        public double RebarDiameter { get; set; }

        /// <summary>
        /// 钢筋间距
        /// </summary>
        public double RebarSpacing { get; set; }

        #endregion

        #region 钢筋参数

        /// <summary>
        /// 钢筋锚固长度
        /// </summary>
        public double AnchorageLength { get; set; }

        /// <summary>
        /// 点钢筋间距
        /// </summary>
        public double DotSeparation { get; set; }

        /// <summary>
        /// 钢筋弯折最小长度
        /// </summary>
        public double BendingLineMinLength { get; set; }

        /// <summary>
        /// 锚固钢筋连接长度
        /// </summary>
        public double AnchorageJoinLength { get; set; }

        /// <summary>
        /// 弯钩长度
        /// </summary>
        public double HookLength { get; set; }

        /// <summary>
        /// 保护层厚度
        /// </summary>
        public double ProtectionThickness { get; set; }

        /// <summary>
        /// 点钢直径
        /// </summary>
        public double ReinforcementDiameter { get; set; }

        /// <summary>
        /// 点钢偏移
        /// </summary>
        public double DotReinOffset { get; set; }

        /// <summary>
        /// 钢筋绘制宽度（出图时需乘以 Scale）
        /// </summary>
        public double PolylineWidth { get; set; }

        /// <summary>
        /// 点钢起始距离
        /// </summary>
        public double DotStartDistance { get; set; }

        #endregion

        #region 尺寸参数

        /// <summary>
        /// 内侧标注距离
        /// </summary>
        public double DimensionDistanceInside { get; set; }

        /// <summary>
        /// 外标注距离
        /// </summary>
        public double DimensionDistanceOutside { get; set; }

        /// <summary>
        /// 标注间距
        /// </summary>
        public double DimensionDistanceWithDim { get; set; }

        /// <summary>
        /// 引线距离
        /// </summary>
        public double MleaderDistance { get; set; }

        /// <summary>
        /// 允许距离（平行且数值相同的标注，距离小于此值时删除）
        /// </summary>
        public double DimDistanceTolerance { get; set; }

        /// <summary>
        /// 文字比例
        /// </summary>
        public double TextXScale { get; set; }

        /// <summary>
        /// 文字高度
        /// </summary>
        public double TextSize { get; set; }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建默认参数
        /// </summary>
        public static ReinParameters CreateDefault()
        {
            return new ReinParameters
            {
                // 基础参数
                Scale = 50.0,
                RebarDiameter = 14.0,
                RebarSpacing = 200.0,

                // 钢筋参数
                AnchorageLength = 500.0,
                DotSeparation = 200.0,
                BendingLineMinLength = 150.0,
                AnchorageJoinLength = 1500.0,
                HookLength = 1.0,
                ProtectionThickness = 1.0,
                ReinforcementDiameter = 0.35,
                DotReinOffset = 1.35,
                PolylineWidth = 0.4,
                DotStartDistance = 0.0,

                // 尺寸参数
                DimensionDistanceInside = 6.0,
                DimensionDistanceOutside = 14.0,
                DimensionDistanceWithDim = 6.0,
                MleaderDistance = 6.0,
                DimDistanceTolerance = 30.0,
                TextXScale = 0.7,
                TextSize = 3.0
            };
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (Scale <= 0)
            {
                errorMessage = "比例必须大于 0";
                return false;
            }

            if (RebarDiameter <= 0)
            {
                errorMessage = "钢筋直径必须大于 0";
                return false;
            }

            if (RebarSpacing <= 0)
            {
                errorMessage = "钢筋间距必须大于 0";
                return false;
            }

            if (DotSeparation <= 0)
            {
                errorMessage = "点钢筋间距必须大于 0";
                return false;
            }

            if (AnchorageLength <= 0)
            {
                errorMessage = "锚固长度必须大于 0";
                return false;
            }

            if (HookLength <= 0)
            {
                errorMessage = "弯钩长度必须大于 0";
                return false;
            }

            if (PolylineWidth <= 0)
            {
                errorMessage = "多段线宽必须大于 0";
                return false;
            }

            if (ProtectionThickness < 0)
            {
                errorMessage = "保护层厚度不能为负数";
                return false;
            }

            if (ReinforcementDiameter <= 0)
            {
                errorMessage = "点钢绘制直径必须大于 0";
                return false;
            }

            if (DotReinOffset < 0)
            {
                errorMessage = "点钢偏移不能为负数";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        #endregion

        #region 对象方法

        /// <summary>
        /// 克隆参数对象
        /// </summary>
        public ReinParameters Clone()
        {
            return new ReinParameters
            {
                Scale = Scale,
                RebarDiameter = RebarDiameter,
                RebarSpacing = RebarSpacing,
                AnchorageLength = AnchorageLength,
                DotSeparation = DotSeparation,
                BendingLineMinLength = BendingLineMinLength,
                AnchorageJoinLength = AnchorageJoinLength,
                HookLength = HookLength,
                ProtectionThickness = ProtectionThickness,
                ReinforcementDiameter = ReinforcementDiameter,
                DotReinOffset = DotReinOffset,
                PolylineWidth = PolylineWidth,
                DotStartDistance = DotStartDistance,
                DimensionDistanceInside = DimensionDistanceInside,
                DimensionDistanceOutside = DimensionDistanceOutside,
                DimensionDistanceWithDim = DimensionDistanceWithDim,
                MleaderDistance = MleaderDistance,
                DimDistanceTolerance = DimDistanceTolerance,
                TextXScale = TextXScale,
                TextSize = TextSize
            };
        }

        public override string ToString()
        {
            return $"ReinParameters[Scale={Scale}, RebarDiameter={RebarDiameter}, RebarSpacing={RebarSpacing}]";
        }

        #endregion
    }
}

