using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shell.Commands
{
    /// <summary>
    /// 三个入口（Blender 面板 / AutoCAD Ribbon / CUIX 菜单）的统一命令分发出口。
    ///
    /// 原理：ReCall/CommandFacade 已经把 commands.json 里每个 key 都注册成 AutoCAD 的 [CommandMethod]。
    /// 这里直接 SendStringToExecute(key) 让 AutoCAD 按正常命令流程调用，复用 ReCall 的反射调度、
    /// SettingsPanelViewModel 前置钩子、异常捕获等完整行为，避免 UI 层重复实现。
    ///
    /// 与 SettingsPanelViewModel.SendCommand 的区别：
    /// - SettingsPanelViewModel.SendCommand 是旧参数面板按钮用的，依赖 PendingCommand + C1 路由（保留不动）。
    /// - CommandDispatcher.Send 是新命令面板/Ribbon/菜单按钮用的，直接按 key 走 AutoCAD 命令行。
    ///
    /// 特例：<c>hyRoadAlnStation</c> / 短别名 <c>rSt</c> 首步为关键字子提示，需在命令名后多发送一次回车才能接受默认「应用」，
    /// 否则 SendStringToExecute 只排队 <c>key</c>+空格时子提示无输入、命令挂起。
    /// </summary>
    public static class CommandDispatcher
    {
        /// <summary>
        /// 把命令 key（如 "gj"、"hyab"）送到 AutoCAD 命令行异步执行。
        /// 多线程安全：SendStringToExecute 会排队到 AutoCAD 命令线程。
        /// </summary>
        /// <param name="key">commands.json 中定义的命令键，与 CommandFacade.[CommandMethod] 完全一致</param>
        public static void Send(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            // activate=true：需要当前文档被激活
            // wrapUpInactive=false、echo=false：避免干扰其他宏
            // false（第 4 个 enforceQueue）：排队执行
            try
            {
                // 多数命令首步即拾取/输入；但 hyRoadAlnStation / rSt 首步是关键字 [应用/配置/默认]，
                // 仅发 "key " 不会向该 Prompt 注入回车，命令会停在子提示处，面板按钮表现为「点了没反应」。
                bool stationCmd = string.Equals(key, "hyRoadAlnStation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "rSt", StringComparison.OrdinalIgnoreCase);
                string tail = stationCmd ? " \n" : " ";
                doc.SendStringToExecute(key + tail, true, false, false);
            }
            catch (System.Exception ex)
            {
                try { doc.Editor?.WriteMessage($"\n[CommandDispatcher] 发送 {key} 失败：{ex.Message}"); }
                catch { }
            }
        }
    }
}
