using System.Threading.Tasks;

namespace HyCADTool.Refactored.Presentation.Commands.Base
{
    /// <summary>
    /// 极简命令接口
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 命令名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 异步执行命令
        /// </summary>
        /// <returns>执行是否成功</returns>
        Task<bool> ExecuteAsync();
    }
}

