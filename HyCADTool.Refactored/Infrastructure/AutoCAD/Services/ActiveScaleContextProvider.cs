using System;
using HyCADTool.Refactored.Domain.Models.Drawing;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 当前活动比例上下文提供者（全局单例）。
    /// 命令在写新实体时统一调用 <see cref="Current"/> 拿到 <see cref="ScaleContext"/>，
    /// 用其 <c>GeomMul</c> 乘到 <c>LinetypeScale</c> / <c>Hatch.PatternScale</c> / 块 <c>ScaleFactors</c>。
    /// 
    /// 由 <c>SettingsPanelViewModel</c> 在参数变更时调用 <see cref="Set"/> 推送最新快照；
    /// 命令侧只读不写。
    /// 
    /// 本期仅提供 provider + 约定；老命令按需逐步迁移。
    /// </summary>
    public static class ActiveScaleContextProvider
    {
        private static ScaleContext _current =
            new ScaleContext(mainScale: 50.0, subScale: 50.0, useSubScale: false, unit: DrawingUnit.Millimeter, precision: 0);

        private static readonly object _lock = new object();

        /// <summary>
        /// 获取当前活动比例上下文快照（永不返回 null）。
        /// </summary>
        public static ScaleContext Current
        {
            get
            {
                lock (_lock) { return _current; }
            }
        }

        /// <summary>
        /// 推送最新快照。ViewModel 在任一相关属性变更时调用一次。
        /// </summary>
        public static void Set(ScaleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            lock (_lock) { _current = context; }
        }
    }
}
