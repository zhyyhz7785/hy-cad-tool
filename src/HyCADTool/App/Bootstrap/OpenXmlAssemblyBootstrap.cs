using System;
using System.Linq;

namespace HyCADTool.App.Bootstrap
{
    /// <summary>
    /// DocumentFormat.OpenXml 版本对齐说明（2026-06 定案，运行时取证）：
    /// AutoCAD 2025 进程自带并预加载 DocumentFormat.OpenXml 2.16.0.0（单一程序集），且用自带 AssemblyResolve
    /// 抢先返回它。若本工程引用 3.x（拆成 DocumentFormat.OpenXml + DocumentFormat.OpenXml.Framework 两程序集），
    /// 运行时 Sheet 绑到 AutoCAD 的 2.16、OpenXmlElement 绑到我们的 3.5 Framework → 类型身份割裂 →
    /// "type argument 'Sheet' violates the constraint of type parameter 'T'"。
    /// 解决：HyCADTool.csproj 直接引用 2.16.0 与 AutoCAD 对齐，单程序集、无 Framework 拆分，无需任何 AssemblyResolve 介入。
    /// 本类仅保留诊断方法（供调试日志统计 AppDomain 内 OpenXml 程序集副本）。
    /// </summary>
    internal static class OpenXmlAssemblyBootstrap
    {
        /// <summary>历史遗留入口：2.16 对齐后无需任何主动加载/解析，保留空实现以免改动调用点。</summary>
        public static void EnsureLoaded()
        {
        }

        internal static int CountLoadedOpenXmlAssemblies()
        {
            return GetLoadedOpenXmlAssemblies().Length;
        }

        internal static string[] GetLoadedOpenXmlAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => (a.GetName().Name ?? string.Empty).StartsWith("DocumentFormat.OpenXml", StringComparison.Ordinal))
                .Select(a =>
                {
                    string location;
                    try
                    {
                        location = string.IsNullOrWhiteSpace(a.Location) ? "<memory>" : a.Location;
                    }
                    catch
                    {
                        location = "<memory>";
                    }

                    string mvid;
                    try
                    {
                        mvid = a.ManifestModule.ModuleVersionId.ToString();
                    }
                    catch
                    {
                        mvid = "?";
                    }

                    return $"{a.GetName().Name}|{a.GetName().Version}|{location}|{mvid}";
                })
                .ToArray();
        }
    }
}
