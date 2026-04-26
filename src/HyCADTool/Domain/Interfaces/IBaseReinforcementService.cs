using HyCADTool.Domain.Enums;
using HyCADTool.Domain.Models.Configuration;
using System.Collections.Generic;

namespace HyCADTool.Domain.Interfaces
{
    /// <summary>
    /// 基础钢筋配置服务接口
    /// 定义与基础钢筋绘制相关的所有业务操作
    /// </summary>
    public interface IBaseReinforcementService
    {
        #region 样式设置

        /// <summary>
        /// 应用样式设置（图层、文字样式、标注样式等）
        /// </summary>
        /// <param name="scale">主图形比例</param>
        void ApplyStyles(double scale);

        #endregion

        #region 步骤操作

        /// <summary>
        /// 步骤 1：整理底图
        /// 选择并优化底图中的线条和图形元素
        /// </summary>
        void OptimizeBasemap();

        /// <summary>
        /// 步骤 2：选择并删除不需要的文字
        /// </summary>
        /// <param name="fixedValues">固定值列表，用于过滤文字</param>
        void SelectAndDeleteUnusedText(List<string> fixedValues);

        /// <summary>
        /// 步骤 4：生成配筋面积
        /// 选择有限元网格，绘制包络线，按空间邻近性分组，生成优化的包络多边形
        /// </summary>
        /// <param name="config">配置参数（包含 ProximityThreshold）</param>
        void GenerateReinforcementArea(BaseReinforcementConfig config);

        /// <summary>
        /// 步骤 5：绘制钢筋
        /// 根据配筋区域的面积和统计数据生成钢筋
        /// </summary>
        /// <param name="config">配置参数（包含钢筋直径、间距、安全系数等）</param>
        /// <param name="dimAll">是否全方向配筋</param>
        void DrawReinforcement(BaseReinforcementConfig config, bool dimAll);

        /// <summary>
        /// 步骤 6：标注配筋区域
        /// 对配筋区域进行尺寸标注
        /// </summary>
        /// <param name="direction">标注方向</param>
        void DimensionReinforcementArea(IntersectionsDirection direction);

        #endregion

        #region 配置管理

        /// <summary>
        /// 获取当前配置
        /// </summary>
        BaseReinforcementConfig GetCurrentConfig();

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="config">要保存的配置</param>
        void SaveConfig(BaseReinforcementConfig config);

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        BaseReinforcementConfig ResetToDefault();

        #endregion
    }
}

