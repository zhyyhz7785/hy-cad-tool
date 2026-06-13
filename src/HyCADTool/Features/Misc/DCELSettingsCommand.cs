using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
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
                WriteSettings(ed, currentSettings);

                // 在当前配置基础上修改（旧实现从 Default 起步会重置 Ellipse/Spline 段数等未涉及项）
                var newSettings = currentSettings.Clone();

                // 1. 选择输出模式
                var restoreOptions = new PromptKeywordOptions("\n输出模式 [简化线段(S)/恢复原曲线(R)]", "S R");
                restoreOptions.AllowNone = false;
                restoreOptions.Keywords.Default = currentSettings.RestoreOriginalCurves ? "R" : "S";
                var restoreResult = ed.GetKeywords(restoreOptions);

                if (restoreResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消设置");
                    return;
                }

                newSettings.RestoreOriginalCurves = restoreResult.StringResult == "R";

                // 2. 可选：调整Arc分段数
                int arcDefault = currentSettings.ArcSegmentCount ?? 6;
                var arcPrompt = new PromptIntegerOptions($"\nArc分段数（4-16，回车={arcDefault}）");
                arcPrompt.AllowNegative = false;
                arcPrompt.AllowNone = true;
                arcPrompt.DefaultValue = arcDefault;
                var arcResult = ed.GetInteger(arcPrompt);

                if (arcResult.Status == PromptStatus.OK)
                {
                    newSettings.ArcSegmentCount = arcResult.Value;
                }
                else if (arcResult.Status != PromptStatus.None)
                {
                    ed.WriteMessage("\n取消设置");
                    return;
                }

                // 3. 可选：详细耗时输出开关
                var timingOptions = new PromptKeywordOptions("\n详细耗时输出 [开(Y)/关(N)]", "Y N");
                timingOptions.AllowNone = true;
                timingOptions.Keywords.Default = currentSettings.VerboseTiming ? "Y" : "N";
                var timingResult = ed.GetKeywords(timingOptions);

                if (timingResult.Status == PromptStatus.OK)
                {
                    newSettings.VerboseTiming = timingResult.StringResult == "Y";
                }
                else if (timingResult.Status != PromptStatus.None)
                {
                    ed.WriteMessage("\n取消设置");
                    return;
                }

                // 应用新配置
                DCELSettings.Current = newSettings;

                ed.WriteMessage("\n━━━━━━━━ 新配置 ━━━━━━━━");
                WriteSettings(ed, newSettings);
                ed.WriteMessage("\n配置已保存！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }

        private static void WriteSettings(Editor ed, DCELSettings settings)
        {
            ed.WriteMessage($"\n  容差          : {settings.Tolerance}");
            ed.WriteMessage($"\n  Arc分段数     : {settings.ArcSegmentCount?.ToString() ?? "自动"}");
            ed.WriteMessage($"\n  Ellipse分段数 : {settings.EllipseSegmentCount?.ToString() ?? "自动"}");
            ed.WriteMessage($"\n  Spline分段数  : {settings.SplineSegmentCount?.ToString() ?? "自动"}");
            ed.WriteMessage($"\n  恢复原曲线    : {(settings.RestoreOriginalCurves ? "是" : "否（使用简化线段）")}");
            ed.WriteMessage($"\n  详细耗时      : {(settings.VerboseTiming ? "开" : "关")}");
        }
    }
}
