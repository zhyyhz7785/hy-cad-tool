using HyCADTool.Features.Tables.Commands;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// AC2 开发联调入口；AC4 后委托 InsertSampleTableCommand。
    /// </summary>
    public sealed class TableRenderDevCommand
    {
        public void Execute()
        {
            new InsertSampleTableCommand().ExecutePersonnelAtOrigin();
        }
    }
}
