using HyCADTool.Refactored.Domain.ValueObjects;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 钢筋服务接口
    /// 定义钢筋绘制、标注等核心功能
    /// </summary>
    public interface IReinService
    {
        /// <summary>
        /// 绘制钢筋
        /// </summary>
        /// <param name="parameters">钢筋参数</param>
        void DrawReinforcement(ReinParameters parameters);

        /// <summary>
        /// 应用样式设置
        /// </summary>
        /// <param name="parameters">钢筋参数（包含比例等样式相关参数）</param>
        void ApplyStyle(ReinParameters parameters);

        /// <summary>
        /// 标注钢筋
        /// </summary>
        /// <param name="mode">标注模式（1=三点, 2=单点, 3=六点）</param>
        /// <param name="parameters">钢筋参数（包含直径、间距等）</param>
        void DimensionRein(int mode, ReinParameters parameters);
    }
}

