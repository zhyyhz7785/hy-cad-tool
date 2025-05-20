using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        public static ObjectId[] GetEntitiesWithMatchingProperties(Document doc, PromptSelectionResult selectionResult, TypedValue[] filterValues)
        {
            Editor ed = doc.Editor;
            // 如果选择结果状态不是 OK，则返回 null
            if (selectionResult.Status != PromptStatus.OK)
            {
                return null;
            }
            SelectionSet allEntities = selectionResult.Value;
            // 创建SelectionFilter对象，使用过滤器列表
            SelectionFilter filter = new SelectionFilter(filterValues);
            // 使用选择过滤器选择全部符合条件的对象
            PromptSelectionResult res = ed.SelectAll(filter);
            if (res.Status != PromptStatus.OK)
            {
                return null;
            }
            return res.Value.GetObjectIds();
        }
    }
}
