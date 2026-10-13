using System;

namespace HyCADTool.Licensing
{
    /// <summary>
    /// 许可 SDK 不在本仓库时的编译桩。仅当 csproj 判定 SDK 文件不存在、且 Configuration 为 Debug 时编入。
    /// 不验签、不写 license 文件；<see cref="LicenseService.LastStatus"/> 固定为 Enterprise，让本机命令能跑。
    /// Release / Production 在 SDK 缺失时由 MSBuild Error 中止，本文件不会进入非 Debug 包。
    /// </summary>
    public enum LicenseProductTier
    {
        Freemium = 0,
        Standard = 1,
        Professional = 2,
        Enterprise = 3
    }

    public sealed class LicenseStatus
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public bool ClockRollBackLocked { get; set; }
        public LicenseProductTier RecognizedTier { get; set; }
        public LicenseProductTier Tier { get; set; }
        public bool Perpetual { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public static class MachineId
    {
        public static string GetMachineCode() => "STUB-NO-LICENSING-SDK";
    }

    public sealed class LicenseService
    {
        public static LicenseService Instance { get; } = new LicenseService();

        public static string StateRecoveryHint =>
            "当前编译的是许可开发桩（未链接注册机 SDK），不验签、不写入 license.lic。";

        public LicenseStatus LastStatus { get; private set; }

        private LicenseService()
        {
            LastStatus = Granted();
        }

        public void Refresh() => Refresh(false);

        public void Refresh(bool forceRefresh)
        {
            LastStatus = Granted();
        }

        public static bool TryImportLicenseText(string text, out string err)
        {
            err = "未链接许可 SDK，开发桩不接受授权码。把注册机 SDK 放到约定相对路径后重新编译再激活。";
            return false;
        }

        private static LicenseStatus Granted()
        {
            return new LicenseStatus
            {
                Ok = true,
                Tier = LicenseProductTier.Enterprise,
                RecognizedTier = LicenseProductTier.Enterprise,
                Perpetual = true,
                ErrorMessage = "开发桩：未验签，已授予 Enterprise，不能用于发行。"
            };
        }
    }
}
