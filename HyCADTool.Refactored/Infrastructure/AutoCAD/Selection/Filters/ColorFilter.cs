using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    public class ColorFilter : IEntityFilter
    {
        public string Key => "Color";

        public SelectionFilter Build(Entity entity)
        {
            if (entity == null) return null;
            return new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Color, (short)entity.ColorIndex)
            });
        }
    }
}


