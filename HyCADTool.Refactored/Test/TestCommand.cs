using HyCADTool.Refactored.Presentation.Commands;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：只执行下方配置的一条测试命令（带计时）。
    /// 切换测试对象时，仅修改 Run() 中“只改下面这一行”。
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            SimpleLogger.LogElapsedTime("命令执行", () =>
            {
                // 只改下面这一行即可切换 C1 测试命令
                new DesignSpecCommand().Execute();
                // 例如：new OverKillCommand().Execute();
            });
        }
    }
}
