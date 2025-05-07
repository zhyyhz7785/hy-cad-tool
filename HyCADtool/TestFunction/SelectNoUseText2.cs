using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class TestFunction
    {
        private static List<string> storedValues = new List<string>();
        private static bool reverseMatch = false;  // 反转匹配结果的开关
        // 可控的图层名称变量，默认为 "筏板板元配筋标注"
        private static string targetLayerName = "筏板板元配筋标注";
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("teb")]
        public static void SelectNoUseText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 获取用户输入的固定数值
            List<string> fixedValues = GetUserInputValues(ed);
            if (fixedValues == null) return;
            // 存储用户输入的值
            storedValues.AddRange(fixedValues.Except(storedValues));
            // 获取用户选择的图形对象
            SelectionSet selSet = GetUserSelection(ed);
            if (selSet == null) return;
            // 筛选出匹配条件的文字对象
            List<ObjectId> matchingTextObjects = GetMatchingTextObjects(selSet, fixedValues);
            if (reverseMatch)
            {
                // 从选择集中减去匹配的对象
                var reversedSelection = selSet.GetObjectIds().Except(matchingTextObjects).ToArray();
                HighlightMatchingObjects(ed, reversedSelection);
            }
            else
            {
                HighlightMatchingObjects(ed, matchingTextObjects.ToArray());
            }
        }
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
        private static SelectionSet GetUserSelection(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions();
            selOpts.MessageForAdding = "\n请选择图形对象: ";
            PromptSelectionResult selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择.");
                return null;
            }
            return selRes.Value;
        }
        private static List<ObjectId> GetMatchingTextObjects(SelectionSet selSet, List<string> fixedValues)
        {
            List<ObjectId> matchingTextObjects = new List<ObjectId>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selSet)
                {
                    if (selObj != null)
                    {
                        DBObject dbObj = trans.GetObject(selObj.ObjectId, OpenMode.ForRead);
                        if (dbObj is DBText dbText && dbText.Layer == targetLayerName && MatchesCondition(dbText.TextString, fixedValues))
                        {
                            matchingTextObjects.Add(selObj.ObjectId);
                        }
                        else if (dbObj is MText mText && mText.Layer == targetLayerName && MatchesCondition(mText.Text, fixedValues))
                        {
                            matchingTextObjects.Add(selObj.ObjectId);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingTextObjects;
        }
        private static void HighlightMatchingObjects(Editor ed, ObjectId[] objectIds)
        {
            if (objectIds.Length > 0)
            {
                ed.SetImpliedSelection(objectIds);
                ed.WriteMessage($"\n共找到 {objectIds.Length} 个匹配的文字对象.");
            }
            else
            {
                ed.WriteMessage("\n未找到匹配的文字对象.");
            }
        }
        private static bool MatchesCondition(string text, List<string> conditions)
        {
            foreach (var condition in conditions)
            {
                if (double.TryParse(text, out double textValue))
                {
                    // 查找x字符的位置
                    int xIndex = condition.IndexOf('x');
                    if (xIndex != -1)
                    {
                        // 提取x字符前后的字符
                        string lowerBoundStr = condition.Substring(0, xIndex).Trim();
                        string upperBoundStr = condition.Substring(xIndex + 1).Trim();
                        // 转换为double类型
                        if (double.TryParse(lowerBoundStr, out double lowerBound) && double.TryParse(upperBoundStr, out double upperBound))
                        {
                            // 判断textValue是否在区间内
                            if (textValue > lowerBound && textValue < upperBound) return true;
                        }
                    }
                    // 处理大于等于的情况，如 >= 5.3
                    if (condition.StartsWith(">=") && double.TryParse(condition.Substring(2), out double greaterThanOrEqualValue))
                    {
                        if (textValue >= greaterThanOrEqualValue) return true;
                    }
                    // 处理小于等于的情况，如 <= 12
                    else if (condition.StartsWith("<=") && double.TryParse(condition.Substring(2), out double lessThanOrEqualValue))
                    {
                        if (textValue <= lessThanOrEqualValue) return true;
                    }
                    // 处理大于的情况，如 > 5.3
                    else if (condition.StartsWith(">") && double.TryParse(condition.Substring(1), out double greaterThanValue))
                    {
                        if (textValue > greaterThanValue) return true;
                    }
                    // 处理小于的情况，如 < 12
                    else if (condition.StartsWith("<") && double.TryParse(condition.Substring(1), out double lessThanValue))
                    {
                        if (textValue < lessThanValue) return true;
                    }
                    // 处理精确值的情况，如 5.3
                    else if (double.TryParse(condition, out double exactValue))
                    {
                        if (textValue == exactValue) return true;
                    }
                }
            }
            return false;
        }
        // 设置目标图层名称的方法
        public static void SetTargetLayerName(string layerName)
        {
            targetLayerName = layerName;
        }
    }
}
