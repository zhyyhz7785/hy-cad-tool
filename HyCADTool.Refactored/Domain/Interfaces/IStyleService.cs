using HyCADTool.Refactored.Domain.ValueObjects.Configuration;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 样式服务接口（平台无关）
    /// 定义文本样式、标注样式等的管理操作
    /// </summary>
    public interface IStyleService
    {
        /// <summary>
        /// 创建或更新文本样式
        /// </summary>
        /// <param name="config">文本样式配置</param>
        void CreateOrUpdateTextStyle(TextStyleConfig config);

        /// <summary>
        /// 创建或更新标注样式
        /// </summary>
        /// <param name="config">标注样式配置</param>
        void CreateOrUpdateDimensionStyle(DimensionStyleConfig config);

        /// <summary>
        /// 设置当前文本样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        void SetCurrentTextStyle(string styleName);

        /// <summary>
        /// 设置当前标注样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        void SetCurrentDimensionStyle(string styleName);

        /// <summary>
        /// 检查文本样式是否存在
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <returns>如果样式存在返回 true</returns>
        bool TextStyleExists(string styleName);

        /// <summary>
        /// 检查标注样式是否存在
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <returns>如果样式存在返回 true</returns>
        bool DimensionStyleExists(string styleName);

        /// <summary>
        /// 创建或更新多重引线样式
        /// </summary>
        /// <param name="config">多重引线样式配置</param>
        void CreateOrUpdateMLeaderStyle(MLeaderStyleConfig config);

        /// <summary>
        /// 检查样式是否存在（通用方法）
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <returns>如果样式存在返回 true</returns>
        bool StyleExists(string styleName);
    }
}

