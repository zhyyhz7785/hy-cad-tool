using System;
using HyCADTool.Shell.Commands;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>
    /// 内置路由与运行时 commands.json 的比对。Production 调度只执行内置 Type/Method；
    /// json 被改成指向别的类时拒绝。无 AutoCAD 引用，Linux 测试可编译。
    /// </summary>
    public static class CommandRouteResolver
    {
        /// <summary>
        /// 面板入队时没带命令键：不能继承 _HyExec 的 Freemium，按 Standard 拦。
        /// </summary>
        public const string MissingPendingKey = "panel-ungated";

        public static CommandRoute GetBuiltin(string key)
        {
            return CommandRoutes.Get(key);
        }

        /// <summary>
        /// 运行时 type/method 与内置不一致则拒绝。runtime 缺省（json 被删）不视为篡改。
        /// </summary>
        public static bool TryRejectIfTampered(string key, string runtimeType, string runtimeMethod, out string reason)
        {
            reason = null;
            var builtin = CommandRoutes.Get(key);
            if (builtin == null)
            {
                reason = "内置路由表中没有键「" + key + "」，已拒绝执行。";
                return true;
            }

            if (string.IsNullOrWhiteSpace(runtimeType))
                return false;

            var expectedType = builtin.Type ?? "";
            var expectedMethod = builtin.Method ?? "Execute";
            var actualMethod = string.IsNullOrWhiteSpace(runtimeMethod) ? "Execute" : runtimeMethod.Trim();
            if (string.Equals(expectedType, runtimeType.Trim(), StringComparison.Ordinal)
                && string.Equals(expectedMethod, actualMethod, StringComparison.Ordinal))
                return false;

            reason = "commands.json 中命令「" + key + "」的 type/method 与内置路由不一致，已拒绝执行。";
            return true;
        }

        /// <summary>
        /// 对照已加载的 <see cref="CommandCatalog"/> 条目。条目为空表示占位或文件缺失，不视为篡改。
        /// </summary>
        public static bool TryRejectIfTampered(string key, CommandEntry runtime, out string reason)
        {
            if (runtime == null)
                return TryRejectIfTampered(key, null, null, out reason);

            return TryRejectIfTampered(key, runtime.Type, runtime.Method, out reason);
        }

        public static string ResolvePendingLicenseKey(string pendingKey)
        {
            return string.IsNullOrWhiteSpace(pendingKey) ? MissingPendingKey : pendingKey.Trim();
        }

        /// <summary>
        /// Production 调度入口：取内置路由并做篡改检查。Linux 测试覆盖这一层，Dispatcher 只转发。
        /// </summary>
        public static bool TryPrepareDispatch(string key, CommandEntry runtime, out CommandRoute route, out string error)
        {
            route = GetBuiltin(key);
            if (route == null || string.IsNullOrWhiteSpace(route.Type))
            {
                route = null;
                error = "内置路由表中未定义键 \"" + key + "\"。";
                return false;
            }

            if (TryRejectIfTampered(key, runtime, out error))
            {
                route = null;
                return false;
            }

            error = null;
            return true;
        }
    }
}
