using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 把钢筋首选项（SettingsPanelViewModel）映射成 NewDDS 平台无关配置（07 §11 第 6 条）。
    /// 第一阶段：与老 dds 完全一致——四项面板参数同时为两者服务（07 §1.2）。
    /// 第二阶段（迁移期之后）：可选地为 NewDDS 独立 ViewModel；本 Adapter 仍为兜底。
    /// 设计要点：
    ///  - Domain 层不感知 ViewModel；Adapter 是仅有的桥；
    ///  - 输出已是"实际 mm（× Scale）"，Domain 内部不再二次缩放；
    ///  - SettingsPanelViewModel.Current 为 null 时（无面板时）回落工厂默认。
    /// </summary>
    public static class NewDdsConfigAdapter
    {
        public static NewDdsConfig FromSettingsPanel()
        {
            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;

            double distInside = (vm?.DimensionDistanceInside ?? 6.0) * scale;
            double distOutside = (vm?.DimensionDistanceOutside ?? 14.0) * scale;
            double distWithDim = (vm?.DimensionDistanceWithDim ?? 6.0) * scale;
            double distTolerance = (vm?.DimDistanceTolerance ?? 30.0) * scale;

            return new NewDdsConfig
            {
                DimensionDistanceInside = distInside,
                DimensionDistanceOutside = distOutside,
                DimensionDistanceWithDim = distWithDim,
                DimDistanceTolerance = distTolerance,
                GenerateOutsideTotalDimension = true,
                StepStrategy = StepStrategy.Adaptive,
                FixedStepValue = 20.0,
                BulgeTessellatePrecision = 10.0,
                SafetyIterationLimit = 1000
            };
        }
    }
}
