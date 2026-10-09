using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DesignSpec.Services;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// HYMDCOMPARE / N39：在图纸上测 MText 字宽与折行，叠 HyMd JSON 块框。
    /// </summary>
    public class HymdCadCompareCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;

            string dir = HymdCadCompareService.DefaultFixtureDir;
            var strOpt = new PromptStringOptions("\n对照样张目录")
            {
                AllowSpaces = true,
                UseDefaultValue = true,
                DefaultValue = Directory.Exists(dir) ? dir : ""
            };
            var strRes = ed.GetString(strOpt);
            if (strRes.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(strRes.StringResult))
                dir = strRes.StringResult.Trim().Trim('"');
            if (!Directory.Exists(dir))
            {
                ed.WriteMessage($"\n[HYMDCOMPARE] 目录不存在：{dir}");
                return;
            }

            var ptRes = ed.GetPoint("\n探针网格左上角");
            if (ptRes.Status != PromptStatus.OK) return;

            var hymd = HymdCadCompareService.TryLoadHymd(dir);
            if (hymd == null)
                ed.WriteMessage("\n[HYMDCOMPARE] 未找到 hymd-compare.json / hymd-dump.json，只测 CAD 探针（无叠框）。");

            HymdCadCompareService.Run(dir, ptRes.Value, hymd);
        }
    }
}
