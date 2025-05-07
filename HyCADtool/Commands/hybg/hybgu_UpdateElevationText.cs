using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.ElevationSymbol;
using System;
using System.Collections.Generic;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("bgu_UpdateElevationText")]
        public static void UpdateElevationText()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            try
            {
                // 提示用户选择新原点
                PromptPointResult ppr = ed.GetPoint("\n选择新的原点: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未成功选择原点，命令中止。");
                    return;
                }
                Point3d newBasePoint = ppr.Value;
                // 生成 SymbolDictionary 基于用户选择
                Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> symbolDictionary = ElevationSymbol.BuildSymbolDictionaryFromSelection();
                if (symbolDictionary.Count == 0)
                {
                    ed.WriteMessage("\n未找到有效的标高符号，命令中止。");
                    return;
                }
                // 更新标高文字
                ElevationSymbol.UpdateElevationsByPoint(newBasePoint, symbolDictionary);
                ed.WriteMessage("\n标高文字已更新。");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}\n");
            }
        }
        // 移除 SelectElevationObjects 方法，因为 BuildSymbolDictionaryFromSelection 已包含选择逻辑
        // 移除 UpdateElevationTextsFromSelection 和 FindNearestPolylineThirdPoint 方法
    }
}