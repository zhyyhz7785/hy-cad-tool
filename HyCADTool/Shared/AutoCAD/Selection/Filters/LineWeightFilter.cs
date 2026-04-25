using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Linq;

namespace HyCADTool.Shared.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 线宽过滤器
    /// LineWeight Filter
    /// </summary>
    public class LineWeightFilter : IEntityFilter
    {
        public string Key => "LineWeight";

        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            if (selectedEntity == null)
                return new ObjectId[0];

            var lineWeight = GetTrueLineWeight(selectedEntity);
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (doc.LockDocument())
            {
                // 创建选择过滤器
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.LineWeight, lineWeight)
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

        private int GetTrueLineWeight(Entity ent)
        {
            if (ent.LineWeight == LineWeight.ByLayer)
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return (int)layer.LineWeight;
                }
            }
            else if (ent.LineWeight == LineWeight.ByBlock && ent is BlockReference br)
            {
                return (int)br.LineWeight;
            }
            return (int)ent.LineWeight;
        }
    }
}
