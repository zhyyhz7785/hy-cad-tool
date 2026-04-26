using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>hydimA：在标注与其他实体交点处加顶点。完整逻辑待从旧版迁移，此处保证命令表可解析。</summary>
    public sealed class AddVertexAtIntersectionsCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[HyCAD] hydimA：交点加顶点 尚未实现；仅占位避免 commands.json 解析失败。");
        }
    }
}
