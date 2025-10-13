using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 类型过滤器
    /// Type Filter
    /// </summary>
    public class TypeFilter : IEntityFilter
    {
        public string Key => "Type";

        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            if (selectedEntity == null)
                return new ObjectId[0];

            var entityTypeName = selectedEntity.GetType().Name;
            var chineseName = TypeNameConverter.ToChinese(entityTypeName);
            var dxfTypeName = TypeNameConverter.ToDxfName(chineseName);

            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (doc.LockDocument())
            {
                // 创建选择过滤器
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, dxfTypeName)
                });

                // 清空预选择集
                ed.SetImpliedSelection(new ObjectId[0]);

                // 使用过滤器选择所有符合条件的实体
                var res = ed.SelectAll(filter);

                if (res.Status == PromptStatus.OK)
                {
                    return res.Value.GetObjectIds();
                }

                return new ObjectId[0];
            }
        }
    }
}
