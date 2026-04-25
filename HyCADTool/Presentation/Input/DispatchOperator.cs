using System;
using HyCAD.BlenderUI.WM.Operators;

namespace HyCADTool.Presentation.Input
{
    /// <summary>
    /// 把 <see cref="Commands.CommandDispatcher.Send"/> 桥成 BlenderUI Operator 模式。
    /// 每条命令（key）对应一个 Operator 实例：Id = "hy.cmd.{key}"；
    /// Invoke / Execute 统一走 CommandDispatcher.Send(key)，保持多文档 / 命令线程安全语义。
    /// 这样：
    ///   1) KeyMap 可以直接把按键映射到 "hy.cmd.{key}"，无需单独定义多条 wrapper；
    ///   2) 未来的 SearchMenu / CommandPalette 也可直接按 OperatorId 驱动，避免 CommandDispatcher 与 WM 架构分裂；
    ///   3) 对于单元测试，可以替换一个 fake DispatchSink 验证 Execute 行为。
    /// </summary>
    public sealed class DispatchOperator : Operator
    {
        /// <summary>hy 命令 Operator 的 Id 前缀。</summary>
        public const string IdPrefix = "hy.cmd.";

        private readonly string _key;
        private readonly Action<string> _sink;

        public DispatchOperator(string commandKey) : this(commandKey, Commands.CommandDispatcher.Send)
        {
        }

        internal DispatchOperator(string commandKey, Action<string> sink)
        {
            _key = commandKey ?? throw new ArgumentNullException(nameof(commandKey));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public override string Id => IdPrefix + _key;

        public override OperatorResult Execute(OperatorContext ctx)
        {
            _sink(_key);
            return OperatorResult.Finished;
        }
    }
}
