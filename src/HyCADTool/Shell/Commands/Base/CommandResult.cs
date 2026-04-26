namespace HyCADTool.Shell.Commands.Base
{
    /// <summary>
    /// 极简命令执行结果
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 执行消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static CommandResult Ok(string message = "执行成功") 
            => new CommandResult { Success = true, Message = message };

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static CommandResult Fail(string message) 
            => new CommandResult { Success = false, Message = message };

        /// <summary>
        /// 重写 ToString 方法
        /// </summary>
        public override string ToString() 
            => Success ? $"✓ {Message}" : $"✗ {Message}";
    }
}

