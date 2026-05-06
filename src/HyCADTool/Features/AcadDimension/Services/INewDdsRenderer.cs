using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 渲染器接口：把 Domain 标注写入 AutoCAD 模型空间（Phase 5 实现）。
    /// 设计要点：单 Transaction 写入，失败整体回滚（修 06 §7 #9）；
    /// XData 持久化输入特征签名，供 NewDDS-Refresh 增量重生成。
    /// </summary>
    public interface INewDdsRenderer
    {
        /// <summary>渲染并写入。返回成功写入的实体数（用于命令行汇报）。</summary>
        int Render(NewDdsResult result);
    }
}
