using HyCADTool.Features.Reinforcement.Domain.Enums;
using HyCADTool.Features.DCEL.Domain.Enums;
using System.Collections.Generic;

namespace HyCADTool.Features.BaseRein.Domain
{
    /// <summary>
    /// 基础底板配筋配置
    /// 包含基础配筋流程所需的所有参数
    /// 对应旧项目 BaseRein 静态类的配置部分
    /// </summary>
    public class BaseReinforcementConfig
    {
        #region 基础参数

        /// <summary>
        /// 主图形比例 — 运行时由 SettingsPanelViewModel.Scale 同步
        /// 不要手动设置此值，由 BaseReinPanelViewModel.SendCommand 自动填充
        /// </summary>
        public double Scale { get; set; }

        /// <summary>
        /// 板厚 (mm)
        /// </summary>
        public double PlateThickness { get; set; }

        /// <summary>
        /// 通筋直径
        /// </summary>
        public double RebarDiameter { get; set; }

        /// <summary>
        /// 通筋间距
        /// </summary>
        public double RebarSpacing { get; set; }

        #endregion

        #region 附加钢筋参数

        /// <summary>
        /// 最小附加钢筋直径
        /// </summary>
        public double MinAdditionalDiameter { get; set; }

        /// <summary>
        /// 附加钢筋间距
        /// </summary>
        public double AdditionalSpacing { get; set; }

        /// <summary>
        /// 配筋安全系数
        /// </summary>
        public double ReinforceSafety { get; set; }

        #endregion

        #region 标注与文字参数

        /// <summary>
        /// 钢筋标注文字 X 向距离
        /// </summary>
        public double ReinforceTextDistanceX { get; set; }

        /// <summary>
        /// 钢筋标注文字 Y 向距离
        /// </summary>
        public double ReinforceTextDistanceY { get; set; }

        /// <summary>
        /// 锚固系数
        /// </summary>
        public double AnchorFactor { get; set; }

        /// <summary>
        /// 文字到线距离
        /// </summary>
        public double TextToLineDistance { get; set; }

        #endregion

        #region 空间分组参数

        /// <summary>
        /// 分组距离阈值 (mm)，间距小于此值则分为一组
        /// </summary>
        public double ProximityThreshold { get; set; }

        /// <summary>
        /// 上下皮钢筋间距
        /// </summary>
        public double ReinforceDistance { get; set; }

        #endregion

        #region 绘制参数

        /// <summary>
        /// 弯钩长度
        /// </summary>
        public double HookLength { get; set; }

        /// <summary>
        /// 多段线线宽
        /// </summary>
        public double PolylineWidth { get; set; }

        /// <summary>
        /// 轴线延伸长度
        /// </summary>
        public double AxisExtend { get; set; }

        /// <summary>
        /// 标注线间距
        /// </summary>
        public double DimensionDistanceWithDim { get; set; }

        /// <summary>
        /// 标注尺寸取整间隔
        /// </summary>
        public double Interval { get; set; }

        #endregion

        #region 布尔标志

        /// <summary>
        /// 是否添加锚固长度
        /// </summary>
        public bool AddAnchorLength { get; set; }

        /// <summary>
        /// 是否存在通筋
        /// </summary>
        public bool ExistingRebar { get; set; }

        /// <summary>
        /// 是否全方向配筋
        /// </summary>
        public bool DimAll { get; set; }

        #endregion

        #region 方向设置

        /// <summary>
        /// 钢筋绘制方向（上皮X/Y、下皮X/Y）
        /// </summary>
        public RebarDirection Direction { get; set; }

        /// <summary>
        /// 标注方向（左右/上下）
        /// </summary>
        public IntersectionsDirection DimDirection { get; set; }

        #endregion

        #region 过滤值

        /// <summary>
        /// 文本过滤值（逗号分隔字符串）
        /// </summary>
        public string FilterValues { get; set; }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static BaseReinforcementConfig CreateDefault()
        {
            return new BaseReinforcementConfig
            {
                // 基础参数（Scale 运行时从设置面板同步，此处仅为安全默认值）
                Scale = 40.0,
                PlateThickness = 350.0,
                RebarDiameter = 12.0,
                RebarSpacing = 200.0,

                // 附加钢筋参数
                MinAdditionalDiameter = 8.0,
                AdditionalSpacing = 200.0,
                ReinforceSafety = 1.0,

                // 标注与文字参数
                ReinforceTextDistanceX = 0.0,
                ReinforceTextDistanceY = 0.0,
                AnchorFactor = 35.0,
                TextToLineDistance = 1.5,

                // 空间分组参数
                ProximityThreshold = 2000.0,
                ReinforceDistance = 3.0,

                // 绘制参数
                HookLength = 1.5,
                PolylineWidth = 0.4,
                AxisExtend = 15000.0,
                DimensionDistanceWithDim = 6.0,
                Interval = 100.0,

                // 布尔标志
                AddAnchorLength = true,
                ExistingRebar = true,
                DimAll = true,

                // 方向设置
                Direction = RebarDirection.TopX,
                DimDirection = IntersectionsDirection.LeftRight,

                // 过滤值
                FilterValues = "<5.65 7.5 9 12"
            };
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (Scale <= 0)
            {
                errorMessage = "比例必须大于 0";
                return false;
            }

            if (PlateThickness <= 0)
            {
                errorMessage = "板厚必须大于 0";
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

            if (AdditionalSpacing <= 0)
            {
                errorMessage = "附加钢筋间距必须大于 0";
                return false;
            }

            if (ProximityThreshold <= 0)
            {
                errorMessage = "分组距离阈值必须大于 0";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public BaseReinforcementConfig Clone()
        {
            return new BaseReinforcementConfig
            {
                Scale = Scale,
                PlateThickness = PlateThickness,
                RebarDiameter = RebarDiameter,
                RebarSpacing = RebarSpacing,
                MinAdditionalDiameter = MinAdditionalDiameter,
                AdditionalSpacing = AdditionalSpacing,
                ReinforceSafety = ReinforceSafety,
                ReinforceTextDistanceX = ReinforceTextDistanceX,
                ReinforceTextDistanceY = ReinforceTextDistanceY,
                AnchorFactor = AnchorFactor,
                TextToLineDistance = TextToLineDistance,
                ProximityThreshold = ProximityThreshold,
                ReinforceDistance = ReinforceDistance,
                HookLength = HookLength,
                PolylineWidth = PolylineWidth,
                AxisExtend = AxisExtend,
                DimensionDistanceWithDim = DimensionDistanceWithDim,
                Interval = Interval,
                AddAnchorLength = AddAnchorLength,
                ExistingRebar = ExistingRebar,
                DimAll = DimAll,
                Direction = Direction,
                DimDirection = DimDirection,
                FilterValues = FilterValues
            };
        }

        public override string ToString()
        {
            return $"BaseReinforcementConfig[Scale={Scale}, PlateThickness={PlateThickness}, RebarDiameter={RebarDiameter}]";
        }

        #endregion
    }
}
