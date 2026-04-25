using System;

namespace HyCADTool.Domain.Models.Road.Civil
{
    /// <summary>
    /// 跨 DWG / 跨 <c>.roaddesign.json</c> 对象引用（045 / M5 Data Shortcut）。
    ///
    /// 对齐 Autodesk Civil 3D 的 Data Shortcut 概念（不追求 Project Folder 固定结构）：
    /// <list type="bullet">
    ///   <item><see cref="SourcePath"/>：目标 <c>.roaddesign.json</c> 的绝对或项目相对路径；</item>
    ///   <item><see cref="SourceId"/>：目标对象（Alignment / Profile / Template / Corridor / Surface / Intersection）
    ///         在目标 <c>RoadDesign</c> 里的稳定 <c>Guid</c>；</item>
    ///   <item><see cref="LocalAlias"/>：在本项目树里显示的别名。</item>
    /// </list>
    ///
    /// 持久化到 <see cref="RoadProject.Shortcuts"/>；由 <c>RoadProjectRegistry</c> 解析。
    /// M5 之前仅值对象占位，不会在 UI 渲染引用节点。
    /// </summary>
    public sealed class DataShortcut
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>被引用对象的种类。</summary>
        public DataShortcutKind Kind { get; set; }

        /// <summary>源 <c>.roaddesign.json</c> 的路径（绝对或相对项目根）。</summary>
        public string SourcePath { get; set; }

        /// <summary>源对象在远端 <c>RoadDesign</c> 里的稳定 <c>Guid</c>。</summary>
        public Guid SourceId { get; set; }

        /// <summary>显示名（本地别名）。</summary>
        public string LocalAlias { get; set; }

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public override string ToString()
            => $"DataShortcut[{Kind} '{LocalAlias}' → {System.IO.Path.GetFileName(SourcePath ?? string.Empty)}#{SourceId:N}]";
    }

    /// <summary>被 <see cref="DataShortcut"/> 引用的对象种类。</summary>
    public enum DataShortcutKind
    {
        Alignment = 0,
        Profile = 1,
        Template = 2,
        Corridor = 3,
        Surface = 4,
        Intersection = 5
    }
}
