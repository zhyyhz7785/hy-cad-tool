using HyCADTool.Domain.ValueObjects;
using HyCADTool.Domain.ValueObjects.Reinforcement;

namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// 钢筋服务接口
    /// 负责将 Domain 层的配筋结果写入 CAD 图纸
    /// </summary>
    public interface IReinService
    {
        /// <summary>
        /// 将配筋结果写入图纸（线钢筋、点钢筋、标注）
        /// </summary>
        void DrawReinforcement(ReinforcementResult result, ReinParameters parameters);

        /// <summary>
        /// 应用样式设置（图层、文字样式、标注样式等）
        /// </summary>
        void ApplyStyle(ReinParameters parameters);

        /// <summary>
        /// 标注钢筋
        /// </summary>
        void DimensionRein(int mode, ReinParameters parameters);
    }
}
