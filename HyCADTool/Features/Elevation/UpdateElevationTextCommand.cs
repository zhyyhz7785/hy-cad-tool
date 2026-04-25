using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shared.AutoCAD.Services;

namespace HyCADTool.Features.Elevation
{
    /// <summary>
    /// 更新标高文字命令（对应旧命令 bgu_UpdateElevationText）
    /// 流程：选新原点 → 选标高符号 → 批量更新文字值
    /// </summary>
    public class UpdateElevationTextCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // 选择新原点
                PromptPointResult ppr = ed.GetPoint("\n选择新的原点: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未成功选择原点，命令中止。");
                    return;
                }
                var newBasePoint = ppr.Value;

                // 构建符号字典
                var symbolDict = ElevationService.BuildSymbolDictionaryFromSelection();
                if (symbolDict.Count == 0)
                {
                    ed.WriteMessage("\n未找到有效的标高符号，命令中止。");
                    return;
                }

                // 更新标高文字
                ElevationService.UpdateElevationsByPoint(newBasePoint, symbolDict);
                ed.WriteMessage("\n标高文字已更新。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
