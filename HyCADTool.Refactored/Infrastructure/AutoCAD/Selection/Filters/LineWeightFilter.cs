using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    public class LineWeightFilter : IEntityFilter
    {
        public string Key => "LineWeight";

        public SelectionFilter Build(Entity entity)
        {
            if (entity == null) return null;
            return new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.LineWeight, (int)entity.LineWeight)
            });
        }
    }
}


