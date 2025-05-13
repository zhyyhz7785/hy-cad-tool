using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Commands.DrawBaseReinforcement))]
namespace HyCADTool.Commands
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb2")]
        public static void BR02()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var fixedValues = GetUserInputValues(ed);
                char[] delimiters = new char[] { ' ', ',', '，' };
                BaseRein.FixedValues = fixedValues;
                BaseRein.SelectNoUseText();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
        private static List<string> storedValues = new List<string>();
        private static List<string> GetUserInputValues(Editor ed)
        {
            PromptStringOptions promptOptions = new PromptStringOptions("\n请输入要筛选的固定数值或区间（如 5.3 < x < 12 或使用空格、逗号或空格+逗号分隔）: ");
            promptOptions.AllowSpaces = true;
            PromptResult promptResult = ed.GetString(promptOptions);
            if (promptResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了操作.");
                return null;
            }
            string inputValues = promptResult.StringResult;
            if (string.IsNullOrWhiteSpace(inputValues))
            {
                if (storedValues.Count == 0)
                {
                    ed.WriteMessage("\n没有存储的值，请输入固定数值.");
                    return null;
                }
                return storedValues;
            }
            char[] delimiters = new char[] { ' ', ',', '，' };
            List<string> fixedValues = inputValues.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();
            return fixedValues;
        }
    }
}
