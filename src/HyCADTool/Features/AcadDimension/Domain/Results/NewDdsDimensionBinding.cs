using HyCADTool.Features.AcadDimension.Domain.Config;

namespace HyCADTool.Features.AcadDimension.Domain.Results
{
    /// <summary>
    /// 写入每条 NewDDS 生成标注的扩展字典载荷（JSON，键名 <see cref="NewDdsBindingKeys.ExtensionDictionaryKey"/>）。
    /// Phase 5：供日后 nddsR 按 Handle 找回源多段线并比对 <see cref="FeatureSignature"/>。
    /// </summary>
    public sealed class NewDdsDimensionBinding
    {
        public string SourcePolylineHandle { get; set; }

        /// <summary>顶点序列（量化）的 SHA256，源轮廓变更后可检测。</summary>
        public string FeatureSignature { get; set; }

        public NewDdsConfig ConfigSnapshot { get; set; }
    }

    /// <summary>扩展字典键常量（避免魔法字符串散落）。</summary>
    public static class NewDdsBindingKeys
    {
        public const string ExtensionDictionaryKey = "HyCAD_NewDDS";
    }
}
