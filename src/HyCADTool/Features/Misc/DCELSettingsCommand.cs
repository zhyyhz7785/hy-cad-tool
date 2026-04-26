using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Features.DCEL.Domain.Services;
using System;

namespace HyCADTool.Features.Misc
{
    /// <summary>
    /// DCEL设置命令（类似HYOVSET）
    /// 允许用户配置曲线简化精度和输出模式
    /// </summary>
    public class DCELSettingsCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            var currentSettings = DCELSettings.Current;

            try
            {
                ed.WriteMessage("\n━━━━━━━━ DCEL设置 ━━━━━━━━");
                ed.WriteMessage($"\n当前配置：");
                ed.WriteMessage($"\n  Arc分段数     : {currentSettings.ArcSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  Ellipse分段数 : {currentSettings.EllipseSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  Spline分段数  : {currentSettings.SplineSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  恢复原曲线    : {(currentSettings.RestoreOriginalCurves ? "是" : "否（使用简化线段）")}");
                ed.WriteMessage("\n━━━━━━━━━━━━━━━━━━━━━━━━");

                // 1. 选择是否恢复原曲线（简化测试流程）
                var restoreOptions = new PromptKeywordOptions("\n输出模式 [简化线段(S)/恢复原曲线(R)]", "S R");
                restoreOptions.AllowNone = false;
                restoreOptions.Keywords.Default = "S"; // 默认：简化线段
                var restoreResult = ed.GetKeywords(restoreOptions);

                if (restoreResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消设置");
                    return;
                }

                DCELSettings newSettings = DCELSettings.Default;

                switch (restoreResult.StringResult)
                {
                    case "S":
                        newSettings.RestoreOriginalCurves = false;
                        ed.WriteMessage("\n已选择：输出简化线段（便于观察端点对齐）");
                        break;
                    case "R":
                        newSettings.RestoreOriginalCurves = true;
                        ed.WriteMessage("\n已选择：恢复原曲线（Arc恢复为bulge段）");
                        break;
                }
                
                // 2. 可选：调整Arc分段数
                var arcPrompt = new PromptIntegerOptions("\nArc分段数（4-16，回车=6）");
                arcPrompt.AllowNegative = false;
                arcPrompt.AllowNone = true;
                arcPrompt.DefaultValue = 6;
                var arcResult = ed.GetInteger(arcPrompt);
                
                if (arcResult.Status == PromptStatus.OK)
                {
                    newSettings.ArcSegmentCount = arcResult.Value;
                }
                else if (arcResult.Status == PromptStatus.None)
                {
                    newSettings.ArcSegmentCount = 6; // 默认6段
                }
                else
                {
                    ed.WriteMessage("\n取消设置");
                    return;
                }

                // 应用新配置
                DCELSettings.Current = newSettings;

                // 显示新配置
                ed.WriteMessage("\n━━━━━━━━ 新配置 ━━━━━━━━");
                ed.WriteMessage($"\n  Arc分段数     : {newSettings.ArcSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  Ellipse分段数 : {newSettings.EllipseSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  Spline分段数  : {newSettings.SplineSegmentCount?.ToString() ?? "自动"}");
                ed.WriteMessage($"\n  恢复原曲线    : {(newSettings.RestoreOriginalCurves ? "是" : "否（使用简化线段）")}");
                ed.WriteMessage("\n━━━━━━━━━━━━━━━━━━━━━━━━");
                ed.WriteMessage("\n配置已保存！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}

