using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 基于实体属性的过滤器接口（Infrastructure 层）
    /// 仅负责从实体生成 SelectionFilter，不做数据库写入
    /// </summary>
    public interface IEntityFilter
    {
        string Key { get; }
        SelectionFilter Build(Entity entity);
    }
}


