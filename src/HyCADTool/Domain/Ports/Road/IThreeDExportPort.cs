using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Domain.Ports.Road
{
    /// <summary>
    /// 三维导出端口（v1 占位 / v2 实现）。
    ///
    /// 设计决策 5：v2 启动（P7）时接入 Blender 插件。
    /// 本端口定义 C# 侧的导出契约，实现放在 Infrastructure 层：
    /// - v2 实现 glTF 2.0（<c>.glb</c>）导出器 → Blender Addon 读取。
    /// - v3 扩展为 FBX 导出器 → Lumion LiveSync。
    ///
    /// v1 阶段：
    /// - Autofac 注册 <c>NotImplementedThreeDExportPort</c>，任何调用抛出 NotImplementedException。
    /// - Presentation 层可注册命令占位（如 <c>hyRoad3dExportGltf</c>），执行时提示用户功能未上线。
    /// </summary>
    public interface IThreeDExportPort
    {
        /// <summary>
        /// 是否支持指定格式（v1 始终返回 false）。
        /// </summary>
        bool Supports(ThreeDExportFormat format);

        /// <summary>
        /// 执行导出。
        /// </summary>
        /// <param name="roadDesign">待导出的道路设计聚合根。</param>
        /// <param name="options">导出选项（目标路径、坐标系变换等）。</param>
        /// <returns>导出结果，含成功状态 / 错误信息 / 生成文件路径。</returns>
        ThreeDExportResult Export(RoadDesign roadDesign, ThreeDExportOptions options);
    }

    /// <summary>
    /// 支持的导出格式。
    /// </summary>
    public enum ThreeDExportFormat
    {
        Gltf = 0,  // v2 首发
        Fbx = 1,   // v3
        Obj = 2,   // 备选
        Usd = 3    // v3+
    }

    /// <summary>
    /// 导出选项。
    /// </summary>
    public sealed class ThreeDExportOptions
    {
        /// <summary>目标文件路径（含扩展名）。</summary>
        public string OutputPath { get; set; }

        /// <summary>导出格式。</summary>
        public ThreeDExportFormat Format { get; set; } = ThreeDExportFormat.Gltf;

        /// <summary>平移到原点（避免 Blender 大坐标精度问题）。</summary>
        public bool TranslateToOrigin { get; set; } = true;

        /// <summary>是否导出材质信息（映射到 <c>blender-material-mapping.json</c>）。</summary>
        public bool IncludeMaterials { get; set; } = true;

        /// <summary>走廊采样步距（m），覆盖 <c>CorridorSegment.SamplingStep</c>。0 表示不覆盖。</summary>
        public double OverrideSamplingStep { get; set; } = 0;
    }

    /// <summary>
    /// 导出结果。
    /// </summary>
    public sealed class ThreeDExportResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }

        public static ThreeDExportResult Fail(string message)
            => new ThreeDExportResult { Success = false, ErrorMessage = message };

        public static ThreeDExportResult Ok(string path)
            => new ThreeDExportResult { Success = true, OutputPath = path };
    }
}
