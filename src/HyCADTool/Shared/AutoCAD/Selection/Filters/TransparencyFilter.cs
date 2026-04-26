using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 透明度过滤器
    /// Transparency Filter
    /// </summary>
    public class TransparencyFilter : IEntityFilter
    {
        public string Key => "Transparency";

        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            if (selectedEntity == null)
                return new ObjectId[0];

            var targetTransparency = GetTrueTransparency(selectedEntity);
            var doc = Application.DocumentManager.MdiActiveDocument;

            return FilterByTransparency(doc, targetTransparency, baseIds);
        }

        private int GetTrueTransparency(Entity ent)
        {
            try
            {
                if (ent.Transparency.IsByAlpha)
                    return ent.Transparency.Alpha;

                if (ent.Transparency.IsByLayer)
                {
                    using (var tr = ent.Database.TransactionManager.StartTransaction())
                    {
                        var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                        tr.Commit();
                        return layer.Transparency.IsByAlpha ? layer.Transparency.Alpha : 255;
                    }
                }
                if (ent.Transparency.IsByBlock && ent is BlockReference br)
                {
                    return br.Transparency.Alpha;
                }
            }
            catch
            {
                return 255;
            }
            return 255;
        }

        private ObjectId[] FilterByTransparency(Document doc, int targetAlpha, ObjectId[] ids)
        {
            var result = new List<ObjectId>();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        var alpha = GetTrueTransparency(ent);
                        if (alpha == targetAlpha)
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

