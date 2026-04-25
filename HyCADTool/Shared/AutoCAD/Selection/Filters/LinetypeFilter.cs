using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 线型过滤器
    /// LineType Filter
    /// </summary>
    public class LineTypeFilter : IEntityFilter
    {
        public string Key => "LineType";

        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            if (selectedEntity == null)
                return new ObjectId[0];

            var targetLinetype = GetTrueLinetype(selectedEntity);
            var doc = Application.DocumentManager.MdiActiveDocument;

            return FilterByLinetype(doc, targetLinetype, baseIds);
        }

        private ObjectId GetTrueLinetype(Entity ent)
        {
            if (ent.Linetype == "ByLayer")
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return layer.LinetypeObjectId;
                }
            }
            else if (ent.Linetype == "ByBlock" && ent is BlockReference br)
            {
                return br.LinetypeId;
            }
            return ent.LinetypeId;
        }

        private ObjectId[] FilterByLinetype(Document doc, ObjectId targetLinetype, ObjectId[] ids)
        {
            var result = new List<ObjectId>();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        var linetype = GetTrueLinetype(ent);
                        if (linetype == targetLinetype)
                        {
                            result.Add(id);
                        }
                    }
                }
                tr.Commit();
            }
            return result.ToArray();
        }
    }
}
