using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>ddaa：对齐尺寸标注。完整逻辑待从旧版迁移，此处保证命令表可解析。</summary>
    public sealed class DimensionAlignCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[HyCAD] ddaa：对齐尺寸标注 尚未实现；仅占位避免 commands.json 解析失败。");
        }
    }
}
