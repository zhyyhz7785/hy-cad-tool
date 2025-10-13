using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    public class LayerFilter : IEntityFilter
    {
        public string Key => "Layer";

        public SelectionFilter Build(Entity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.Layer)) return null;
            return new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.LayerName, entity.Layer)
            });
        }
    }
}


