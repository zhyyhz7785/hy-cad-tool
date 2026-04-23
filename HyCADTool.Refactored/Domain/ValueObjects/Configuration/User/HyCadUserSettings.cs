namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 用户级 HyCAD 绘图设置根对象（与 <c>hy-settings.json</c> 中可拆出的域块对应，便于今后把样式等也迁入结构体）。
    /// 当前主面板参数仍由设置 ViewModel 的 JSON DTO 承载；本类型供领域层引用与后续拆分。
    /// </summary>
    public sealed class HyCadUserSettings
    {
        public int Version { get; set; } = 1;

        public UserLayerSettings Layers { get; set; } = new UserLayerSettings();
    }
}
