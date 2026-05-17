using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 渲染器接口：把 Domain 标注写入 AutoCAD 模型空间。
    /// 设计要点（修 06 §7 #9）：
    ///  - 接收外层 Service 的 Document + Transaction，避免嵌套事务；
    ///  - 失败由 Service 整体回滚（throw 即可）；
    ///  - 返回写入的实体数量供命令行汇报。
    /// 默认实现：AcadDimensionRenderer（Phase 3a 落地）。
    /// </summary>
    public interface INewDdsRenderer
    {
        /// <param name="bindingContext">Phase 5：非 null 且 <see cref="NewDdsRenderBindingContext.IsBindingEnabled"/> 时为每条标注写入扩展字典。</param>
        int Render(
            NewDdsResult result,
            Document document,
            Transaction transaction,
            NewDdsRenderBindingContext bindingContext = null);
    }
}
