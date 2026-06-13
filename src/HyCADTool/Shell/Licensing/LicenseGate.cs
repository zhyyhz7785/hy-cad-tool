using System;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Licensing;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>生产模式 [CommandMethod] 许可门闸：按命令名检查当前 License 档位是否足够。</summary>
    public static class LicenseGate
    {
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

            // fail-open 有意：License 状态文件异常时仍放行 Freemium 档命令，仅拦截更高档位。
            if (!st.Ok)
            {
                if (!string.IsNullOrWhiteSpace(st.ErrorMessage))
                    Write($"\n[HyCAD] License 状态异常（fail-open 放行免费命令）：{st.ErrorMessage}\n");
                if (required > LicenseProductTier.Freemium && !string.Equals(commandKey, "hyLicense", StringComparison.OrdinalIgnoreCase))
                {
                    Write($"\n[HyCAD] License 无效：{st.ErrorMessage}\n[HyCAD] 输入 hyLicense 可查看本机机器码并输入授权码激活。\n");
                    return;
                }
            }

            if ((int)st.Tier < (int)required)
            {
                Write($"\n[HyCAD] 命令「{commandKey}」需要 {required} 及以上许可，当前为 {st.Tier}。输入 hyLicense 可查看机器码并激活。\n");
                return;
            }

            action();
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
