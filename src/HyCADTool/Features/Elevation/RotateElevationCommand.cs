using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Features.Elevation.Services;

namespace HyCADTool.Features.Elevation
{
    /// <summary>
    /// 旋转标高符号命令（对应旧命令 bgR_RotateElevation）
    /// 流程：输入角度 → 选标高符号 → 批量旋转
    /// </summary>
    public class RotateElevationCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                // 输入旋转角度
                PromptDoubleResult pdr = ed.GetDouble("\n输入旋转角度 (度): ");
                if (pdr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未成功输入角度，命令中止。");
                    return;
                }
                double angleDegrees = pdr.Value;

                // 构建符号字典
                var symbolDict = ElevationService.BuildSymbolDictionaryFromSelection();
                if (symbolDict.Count == 0)
                {
                    ed.WriteMessage("\n未找到有效的标高符号，命令中止。");
                    return;
                }

                // 旋转
                ElevationService.RotateAllByAngle(angleDegrees, symbolDict);
                ed.WriteMessage("\n标高符号已旋转。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}
