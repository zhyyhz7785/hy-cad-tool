using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// 按图层语义 ID 从 <see cref="SettingsPanelViewModel"/> 的层表解析用户可改名后的实际图层名；无设置或无匹配时回退为默认名。
    /// </summary>
    public static class UserLayerNameResolver
    {
        public static string Get(string semanticId, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(semanticId)) return defaultName ?? string.Empty;
            try
            {
                var vm = SettingsPanelViewModel.Current;
                if (vm == null) return defaultName ?? string.Empty;
                return vm.TryResolveLayerName(semanticId, defaultName);
            }
            catch
            {
                return defaultName ?? string.Empty;
            }
        }
    }
}
