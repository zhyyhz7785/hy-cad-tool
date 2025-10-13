using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    public class TypeFilter : IEntityFilter
    {
        public string Key => "Type";

        public SelectionFilter Build(Entity entity)
        {
            if (entity == null) return null;
            var dxf = entity.GetRXClass().DxfName;
            if (string.IsNullOrEmpty(dxf)) return null;
            return new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, dxf)
            });
        }
    }
}


