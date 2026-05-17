using HyCADTool.Features.AcadDimension.Services;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>
    /// nddsR：刷新所有由 NewDDS 生成且仍带 HyCAD_NewDDS 扩展字典的标注。
    /// 按源 Polyline Handle 分组比对 FeatureSignature；变更的源会先擦旧标注再调用
    /// <see cref="NewDdsService.RunInTransaction"/> 在同一事务内重画；源消失则标孤儿（不删）。
    /// </summary>
    public sealed class NewDdsRefreshCommand
    {
        public void Execute() => new NewDdsRefreshService().Execute();
    }
}
