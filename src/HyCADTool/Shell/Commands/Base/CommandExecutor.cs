using System;
using System.Threading.Tasks;

namespace HyCADTool.Shell.Commands.Base
{
    /// <summary>
    /// 极简命令执行器
    /// 统一异常处理和日志输出
    /// </summary>
    public static class CommandExecutor
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <returns>命令执行结果</returns>
        public static async Task<CommandResult> RunAsync(ICommand command)
        {
            if (command == null)
                return CommandResult.Fail("命令不能为空");

            try
            {
                var success = await command.ExecuteAsync();
                return success 
                    ? CommandResult.Ok($"{command.Name} 执行成功") 
                    : CommandResult.Fail($"{command.Name} 执行失败");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                return CommandResult.Fail($"{command.Name} AutoCAD异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"{command.Name} 异常: {ex.Message}");
            }
        }
    }
}

