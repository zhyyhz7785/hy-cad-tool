using System;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Licensing;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>生产模式 [CommandMethod] 许可门闸：按命令名检查当前 License 档位是否足够。</summary>
    public static class LicenseGate
    {
        private static string _lastBlockMessageKey;

        public static void ResetBlockMessage() => _lastBlockMessageKey = null;

        public static void RunGated(string commandKey, Action action)
        {
#if !HYCAD_PRODUCTION
            // 开发调试用旁路：仅 Debug 构建编译进来，生产包（Configuration=Production）不存在此后门。
            if (string.Equals(Environment.GetEnvironmentVariable("HYCAD_BYPASS_LICENSE"), "1", StringComparison.OrdinalIgnoreCase))
            {
                action();
                return;
            }
#endif

            LicenseService.Instance.Refresh();
            var st = LicenseService.Instance.LastStatus;
            var required = LicenseCommandTierMap.GetRequiredTier(commandKey);

            if (required <= LicenseProductTier.Freemium
                || string.Equals(commandKey, "hyLicense", StringComparison.OrdinalIgnoreCase))
            {
                action();
                return;
            }

            if (!st.Ok)
            {
                var reason = FormatBlockReason(st);
                WriteBlockOnce(
                    "invalid:" + reason,
                    $"\n[HyCAD] 命令「{commandKey}」需要 {required} 及以上许可：{reason}\n[HyCAD] 输入 hyLicense 可查看机器码并激活。\n");
                return;
            }

            if ((int)st.Tier < (int)required)
            {
                WriteBlockOnce(
                    "tier:" + commandKey + ":" + st.Tier,
                    $"\n[HyCAD] 命令「{commandKey}」需要 {required} 及以上许可，当前为 {st.Tier}。输入 hyLicense 可查看机器码并激活。\n");
                return;
            }

            _lastBlockMessageKey = null;
            action();
        }

        private static string FormatBlockReason(LicenseStatus st)
        {
            if (st.ClockRollBackLocked && st.RecognizedTier > LicenseProductTier.Freemium
                && !string.IsNullOrWhiteSpace(st.ErrorMessage))
                return st.ErrorMessage;
            return st.ErrorMessage ?? "未激活（免费版）。";
        }

        private static void WriteBlockOnce(string key, string msg)
        {
            if (string.Equals(_lastBlockMessageKey, key, StringComparison.Ordinal))
                return;
            _lastBlockMessageKey = key;
            Write(msg);
        }

        private static void Write(string msg)
        {
            try
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(msg);
            }
            catch
            {
            }
        }
    }
}
