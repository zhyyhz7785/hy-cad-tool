using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 颜色过滤器
    /// Color Filter
    /// </summary>
    public class ColorFilter : IEntityFilter
    {
        public string Key => "Color";

        public ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds)
        {
            if (selectedEntity == null)
                return new ObjectId[0];

            var targetColor = GetTrueColor(selectedEntity);
            var doc = Application.DocumentManager.MdiActiveDocument;

            return FilterByColor(doc, targetColor, baseIds);
        }

        private Color GetTrueColor(Entity ent)
        {
            if (ent.Color.IsByLayer)
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return layer.Color;
                }
            }
            else if (ent.Color.IsByBlock && ent is BlockReference br)
            {
                return br.Color;
            }
            return ent.Color;
        }

        private ObjectId[] FilterByColor(Document doc, Color targetColor, ObjectId[] ids)
        {
            var result = new List<ObjectId>();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        var color = GetTrueColor(ent);
                        if (color.ColorValue == targetColor.ColorValue)
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
