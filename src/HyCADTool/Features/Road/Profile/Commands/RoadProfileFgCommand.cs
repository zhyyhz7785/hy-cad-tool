using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.PlanProfile.Services;
using HyCADTool.Features.Road.PlanProfile.Domain;
using HyCADTool.Features.Road.PlanProfile.Views;
namespace HyCADTool.Features.Road.PlanProfile.Commands
{
    /// <summary>
    /// hyRoadProfFG —— 与 <see cref="RoadProfileCommand"/>（hyRoadP）等价的 hy道路命名别名。
    ///
    /// hy道路命令体系：hyRoadProfFG（设计线 / FG=Finished Grade）/ hyRoadProfEG（地面线 / EG=Existing Grade）。
    /// 本项目 v1 把"打开纵断面编辑器"统一在 <see cref="RoadProfileCommand"/>，本类仅作为命名直通：
    /// 不做参数差异，便于团队习惯切换。
    /// </summary>
    public sealed class RoadProfileFgCommand
    {
        public void Execute() => new RoadProfileCommand().Execute();
    }
}
