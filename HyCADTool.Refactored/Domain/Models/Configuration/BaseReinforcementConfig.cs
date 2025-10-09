using static HyCADTool.BaseRein;

namespace HyCADTool.Refactored.Domain.Models.Configuration
{
    /// <summary>
    /// 基础钢筋配置参数
    /// 封装所有与基础钢筋绘制相关的配置项
    /// </summary>
    public class BaseReinforcementConfig
    {
        #region 基本设置

        /// <summary>
        /// 主图形比例
        /// </summary>
        public double Scale { get; set; } = 100;

        /// <summary>
        /// 板厚（mm）
        /// </summary>
        public double PlateThickness { get; set; } = 350;

        #endregion

        #region 钢筋参数

        /// <summary>
        /// 通长钢筋直径（mm）
        /// </summary>
        public double RebarDiameter { get; set; } = 12;

        /// <summary>
        /// 通长钢筋间距（mm）
        /// </summary>
        public double RebarSpacing { get; set; } = 200;

        /// <summary>
        /// 附加钢筋最小直径（mm）
        /// </summary>
        public double MinAdditionalDiameter { get; set; } = 8;

        /// <summary>
        /// 附加钢筋间距（mm）
        /// </summary>
        public double AdditionalSpacing { get; set; } = 200;

        /// <summary>
        /// 钢筋配筋安全系数/放大系数
        /// </summary>
        public double ReinforceSafety { get; set; } = 1;

        #endregion

        #region 文字偏移参数

        /// <summary>
        /// 配筋文字 X 方向平移距离
        /// </summary>
        public double ReinforceTextDistanceX { get; set; } = 0;

        /// <summary>
        /// 配筋文字 Y 方向平移距离
        /// </summary>
        public double ReinforceTextDistanceY { get; set; } = 0;

        #endregion

        #region 高级设置

        /// <summary>
        /// 锚固长度因子（钢筋直径倍数）
        /// </summary>
        public double AnchorFactor { get; set; } = 35;

        /// <summary>
        /// 配筋分区参数（空间聚类阈值）
        /// </summary>
        public double ProximityThreshold { get; set; } = 1000;

        /// <summary>
        /// 上下钢筋间距
        /// </summary>
        public double ReinforceDistance { get; set; } = 3;

        /// <summary>
        /// 文字到线的距离
        /// </summary>
        public double TextToLineDistance { get; set; } = 1;

        /// <summary>
        /// 弯钩长度
        /// </summary>
        public double HookLength { get; set; } = 1;

        /// <summary>
        /// 多段线宽度
        /// </summary>
        public double PolylineWidth { get; set; } = 0.4;

        /// <summary>
        /// 轴网延伸距离（标注最远距离）
        /// </summary>
        public double AxisExtend { get; set; } = 15000;

        /// <summary>
        /// 标注间距
        /// </summary>
        public double DimensionDistanceWithDim { get; set; } = 6;

        /// <summary>
        /// 标注尺寸取整间隔
        /// </summary>
        public double Interval { get; set; } = 100;

        #endregion

        #region 复选框状态

        /// <summary>
        /// 是否增加锚固长度
        /// </summary>
        public bool AddAnchorLength { get; set; } = true;

        /// <summary>
        /// 是否全方向配筋
        /// </summary>
        public bool DimAll { get; set; } = false;

        /// <summary>
        /// 是否存在通长钢筋
        /// </summary>
        public bool ExistingRebar { get; set; } = true;

        #endregion

        #region 枚举选择

        /// <summary>
        /// 钢筋方向
        /// </summary>
        public RebarDirection Direction { get; set; } = RebarDirection.TopX;

        /// <summary>
        /// 标注方向
        /// </summary>
        public IntersectionsDirection DimDirection { get; set; } = IntersectionsDirection.LeftRight;

        #endregion

        #region 特殊参数

        /// <summary>
        /// 过滤值（用于选择删除步骤）
        /// 格式示例: "&lt;5.65 7.5 9 12"
        /// </summary>
        public string FilterValues { get; set; } = "<5.65 7.5 9 12";

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static BaseReinforcementConfig CreateDefault()
        {
            return new BaseReinforcementConfig();
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public BaseReinforcementConfig Clone()
        {
            return (BaseReinforcementConfig)this.MemberwiseClone();
        }

        #endregion
    }
}

