using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 选择集过滤服务接口（接口位于 Domain，具体实现放在 Infrastructure）
    /// </summary>
    public interface ISelectionFilterService
    {
        /// <summary>
        /// 根据条件构建 SelectionFilter（可选类型、图层、颜色、线型、线宽）
        /// 任意参数为 null/空则忽略该条件
        /// </summary>
        SelectionFilter Build(
            string dxfType = null,
            string layerName = null,
            short? colorIndex = null,
            string linetypeName = null,
            LineWeight? lineWeight = null);

        /// <summary>
        /// 从原型实体构建 SelectionFilter（按需启用各属性过滤项）
        /// </summary>
        SelectionFilter BuildFromEntity(
            Entity prototype,
            bool byLayer = false,
            bool byColor = false,
            bool byLinetype = false,
            bool byLineWeight = false,
            bool byType = false);
    }
}


