// =============================================================================
// 生产构建专用入口（仅在 Configuration=Production 时编译）
// =============================================================================
//
// 触发：HyCADTool.Refactored.csproj 的 Production PropertyGroup 定义了 HYCAD_PRODUCTION 常量。
//
// 作用：把 HyCADTool.Refactored.dll 变成可以被 AutoCAD 直接 NETLOAD 的"成品插件 dll"，
//       不再依赖 ReCall.dll 这层热重载壳，最终客户安装包只需要：
//         - HyCADTool.Refactored.dll（含本文件激活的两条 [assembly:] attribute）
//         - HyCADTool.Refactored.dll 的 NuGet/项目依赖（HyCAD.BlenderUI.dll、Newtonsoft.Json.dll、Autofac.dll …）
//         - commands.json（与 dll 同目录，由 Production 配置自动复制；ReCall 时代的命令表）
//
// 与开发模式（ReCall + C2 热重载）共存：
//   - Debug/Release 构建：HYCAD_PRODUCTION 未定义 → 本文件全部 #if 段被预处理掉 →
//     Refactored.dll 没有任何 [ExtensionApplication] / [CommandClass] 属性 →
//     ReCall.dll 通过 byte[] 加载 Refactored 时，AutoCAD 不会被触发"自动 Initialize" →
//     生命周期完全由 ReCall.C2 控制（与历史架构 100% 兼容）。
//   - Production 构建：本文件激活 → AutoCAD NETLOAD → 直调 ProductionExtension.Initialize → 144 条命令立即可用。
//
// 性能基线测试：用 Production dll NETLOAD 即可还原"用户真实安装包"性能，
//               与开发模式（ReCall byte[] 加载 + 临时目录复制）对比即可识别 ReCall 引入的开销。
// =============================================================================

#if HYCAD_PRODUCTION

using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(HyCADTool.Refactored.Production.ProductionExtension))]
[assembly: CommandClass(typeof(HyCADTool.Refactored.Production.ProductionCommandFacade))]

namespace HyCADTool.Refactored.Production
{
    /// <summary>
    /// 生产模式下的 AutoCAD 扩展应用入口。
    /// AutoCAD NETLOAD HyCADTool.Refactored.dll 后会自动调用 Initialize / Terminate。
    /// 内部直接走 <see cref="HyCADTool.Refactored.Presentation.PluginInitializer"/>
    /// 与开发模式中 ReCall 反射调用的入口完全一致 → 业务初始化路径 100% 复用。
    /// </summary>
    public sealed class ProductionExtension : IExtensionApplication
    {
        private static HyCADTool.Refactored.Presentation.PluginInitializer _initializer;

        public void Initialize()
        {
            try
            {
                _initializer = new HyCADTool.Refactored.Presentation.PluginInitializer();
                _initializer.Initialize();
            }
            catch (System.Exception ex)
            {
                try
                {
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.Editor.WriteMessage($"\n[HyCAD Production] Initialize 失败: {ex.GetType().Name}: {ex.Message}\n");
                }
                catch { /* shutdown 阶段 Editor 不可用，忽略 */ }
            }
        }

        public void Terminate()
        {
            try
            {
                _initializer?.Terminate();
            }
            catch { /* AutoCAD 退出阶段任何异常都不该再阻塞流程 */ }
            finally
            {
                _initializer = null;
            }
        }
    }
}

#endif
