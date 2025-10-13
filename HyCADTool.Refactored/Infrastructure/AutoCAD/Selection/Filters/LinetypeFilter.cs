using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    public class LinetypeFilter : IEntityFilter
    {
        public string Key => "Linetype";

        public SelectionFilter Build(Entity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.Linetype)) return null;
            return new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.LinetypeName, entity.Linetype)
            });
        }
    }
}


