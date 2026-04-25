using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Shell
{
    /// <summary>
    /// 命令：hyCmdList
    /// 打印 ReCall/commands.json 当前按 Category 分组的结果，验证数据元数据是否正确。
    /// 三个入口（Blender 面板 / Ribbon / CUIX 菜单）都从同一分组结果构建 UI，
    /// 此命令是三入口 UI 上线前的核对工具。
    /// </summary>
    public class CommandListDumpCommand
    {
        public void Execute()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            try
            {
                var groups = HyCADTool.Infrastructure.Commands.CommandCatalog.GroupByCategory();
                int total = 0;

                ed.WriteMessage("\n========================================");
                ed.WriteMessage($"\ncommands.json 分组清单（共 {groups.Count} 个分类）");
                ed.WriteMessage("\n========================================");

                foreach (var g in groups)
                {
                    ed.WriteMessage($"\n\n[{g.Category}] {g.Items.Count} 项");
                    ed.WriteMessage("\n----------------------------------------");
                    foreach (var it in g.Items)
                    {
                        ed.WriteMessage($"\n  {it.Key,-32}  {it.DisplayName}");
                        total++;
                    }
                }

                ed.WriteMessage("\n----------------------------------------");
                ed.WriteMessage($"\n合计命令数：{total}");
                ed.WriteMessage("\n（以上 UI 实际可见条目，不含 _HyExec / N1-N50 占位符）");
                ed.WriteMessage("\n========================================\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[hyCmdList] 失败：{ex.Message}");
            }
        }
    }
}
