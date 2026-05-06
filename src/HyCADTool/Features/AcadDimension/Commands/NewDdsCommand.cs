using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.AcadDimension.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>
    /// ndds：智能断面标注（NewDDS）。
    /// 范式：S1a 解析式直接派生（割线扫描 → 拓扑特征 → 直接派生标注），见 06 §5。
    /// 实施计划：07-NewDDS新命令实施计划-不动老dds的优化重构-2026-05-07-021100.md。
    /// 当前阶段：Phase 1 链路骨架——Domain 5 接口 + 6 数据类 + 编排链路打通；
    ///           各阶段为 NoOp 实现，命令行打印阶段计数证明流转无误。
    /// 与老 dds（DimensionForReinforcementCommand）并存；老命令在整个迁移期保持不变。
    /// </summary>
    public sealed class NewDdsCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var opts = new PromptEntityOptions("\n[NewDDS] 选择多段线:");
            opts.SetRejectMessage("\n请选择多段线。");
            opts.AddAllowedClass(typeof(Polyline), exactMatch: true);

            var result = ed.GetEntity(opts);
            if (result.Status != PromptStatus.OK) return;

            new NewDdsService().Execute(result.ObjectId);
        }
    }
}
