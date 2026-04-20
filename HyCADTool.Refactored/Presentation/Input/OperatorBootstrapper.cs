using System;
using HyCAD.BlenderUI.WM.Operators;
using HyCADTool.ReCall;

namespace HyCADTool.Refactored.Presentation.Input
{
    /// <summary>
    /// 启动时把 <see cref="CommandTable"/> 里的每条命令批量注册为 Operator（Id = "hy.cmd.{key}"）。
    /// 约束：
    ///   - 进程级幂等：重复调用只真正注册一次；
    ///   - 不触发 CommandTable.EnsureLoaded 以外的副作用；
    ///   - 失败（commands.json 读取异常）时吞掉并记录状态，不阻塞 UI；
    ///   - 只产出对应工厂（闭包捕获 key），不会预先实例化 Operator 对象。
    /// </summary>
    public static class OperatorBootstrapper
    {
        private static readonly object _sync = new object();
        private static bool _registered;

        /// <summary>已注册的命令条数（仅统计成功注册的）。供启动日志显示。</summary>
        public static int RegisteredCommandCount { get; private set; }

        public static void RegisterAllCommands()
        {
            if (_registered) return;
            lock (_sync)
            {
                if (_registered) return;
                try
                {
                    var groups = CommandTable.GroupByCategory();
                    int count = 0;
                    foreach (var g in groups)
                    {
                        foreach (var it in g.Items)
                        {
                            if (string.IsNullOrWhiteSpace(it.Key)) continue;
                            var key = it.Key;
                            OperatorRegistry.Register(
                                DispatchOperator.IdPrefix + key,
                                () => new DispatchOperator(key));
                            count++;
                        }
                    }
                    RegisteredCommandCount = count;
                }
                catch
                {
                    // commands.json 异常交给上层 UI 的 StatusMessage 反馈；
                    // 此处静默失败，保证 UI 层加载不被阻塞。
                }
                _registered = true;
            }
        }

        /// <summary>测试用：允许重复注册（单次 AppDomain 生命周期内）。</summary>
        internal static void ResetForTests()
        {
            lock (_sync)
            {
                _registered = false;
                RegisteredCommandCount = 0;
            }
        }
    }
}
