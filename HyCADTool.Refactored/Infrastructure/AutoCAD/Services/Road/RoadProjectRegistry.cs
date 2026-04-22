using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Models.Road.Civil;
using HyCADTool.Refactored.Domain.Models.Road.Serialization;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 项目级注册表（045 / M5）。
    ///
    /// 在既有 <see cref="RoadDesignRegistry"/> 之上增加 **项目层** 能力：
    /// <list type="bullet">
    ///   <item>按 AutoCAD 文档名缓存 <see cref="RoadProject"/> 聚合根；</item>
    ///   <item>缓存外部 <c>.roaddesign.json</c> 的懒加载结果（跨 DWG 引用的 Data Shortcut 源）；</item>
    ///   <item>解析 <see cref="DataShortcut"/> → 远端实体（Alignment / Profile / …）的 <c>Guid</c> 对象。</item>
    /// </list>
    ///
    /// 本类**组合**而非替换 <see cref="RoadDesignRegistry"/>：v1.x 命令仍可通过 <see cref="InnerRegistry"/>
    /// 访问传统单-DWG API；项目树与跨 DWG 功能走本类。
    ///
    /// 线程：AutoCAD 命令线程顺序访问为主；内部使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 防御并发。
    /// </summary>
    public sealed class RoadProjectRegistry
    {
        private readonly ConcurrentDictionary<string, RoadProject> _projects
            = new ConcurrentDictionary<string, RoadProject>(StringComparer.OrdinalIgnoreCase);

        // 外部 .roaddesign.json（相对 RoadProject.RootDirectory 或绝对路径）的懒加载缓存
        private readonly ConcurrentDictionary<string, RoadDesign> _externalDesigns
            = new ConcurrentDictionary<string, RoadDesign>(StringComparer.OrdinalIgnoreCase);

        private readonly RoadJsonExportService _io;
        private readonly IRoadEventBus _eventBus;

        public RoadProjectRegistry(RoadJsonExportService io, IRoadEventBus eventBus, RoadDesignRegistry innerRegistry)
        {
            _io = io ?? throw new ArgumentNullException(nameof(io));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            InnerRegistry = innerRegistry ?? throw new ArgumentNullException(nameof(innerRegistry));
        }

        /// <summary>底层单 DWG 注册表（向后兼容通道）。</summary>
        public RoadDesignRegistry InnerRegistry { get; }

        /// <summary>
        /// 取指定文档的 <see cref="RoadProject"/>（懒加载）：
        /// 优先走 <c>LoadProjectForDocument</c>（<c>.roadproject.json</c> &gt; <c>.roaddesign.json</c> 包装），
        /// 不存在时用底层 <see cref="RoadDesignRegistry.GetOrCreate"/> 拿到的 design 包一个新的空 project。
        /// </summary>
        public RoadProject GetOrCreateForDocument(string documentName)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";

            return _projects.GetOrAdd(documentName, docName =>
            {
                var loaded = _io.LoadProjectForDocument(docName);
                if (loaded != null)
                {
                    // 同步到 InnerRegistry：把主 design 绑到文档名下，供旧命令使用
                    var primary = loaded.GetPrimaryDesign();
                    if (primary != null) InnerRegistry.Replace(docName, primary);
                    return loaded;
                }

                // 没有任何文件 → 用 InnerRegistry 拿到（或新建）一个空 design 包装
                var inner = InnerRegistry.GetOrCreate(docName);
                return RoadProjectMigration.WrapSingleDesign(inner);
            });
        }

        /// <summary>查询但不创建。</summary>
        public bool TryGet(string documentName, out RoadProject project)
        {
            return _projects.TryGetValue(documentName ?? string.Empty, out project);
        }

        /// <summary>
        /// 替换指定文档的 <see cref="RoadProject"/>（通常发生在外部加载完整 <c>.roadproject.json</c> 后），
        /// 并把主 design 同步回 <see cref="InnerRegistry"/>，发布 <see cref="RoadDesignReloadedEvent"/>。
        /// </summary>
        public void Replace(string documentName, RoadProject newProject)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";
            if (newProject == null) throw new ArgumentNullException(nameof(newProject));

            _projects[documentName] = newProject;

            var primary = newProject.GetPrimaryDesign();
            if (primary != null) InnerRegistry.Replace(documentName, primary);
            else _eventBus.Publish(new RoadDesignReloadedEvent(newProject.Id));
        }

        /// <summary>
        /// 文档关闭：清 Project + 清 Inner + 释放外部 design 缓存里对应路径（如果可判定）。
        /// 外部缓存按绝对路径键，不一定与 docName 等价，故保守只清 project；外部 design 由容量上限或显式 <see cref="ClearExternalCache"/> 管理。
        /// </summary>
        public void Remove(string documentName)
        {
            _projects.TryRemove(documentName ?? string.Empty, out _);
            InnerRegistry.Remove(documentName);
        }

        /// <summary>清空所有外部 design 懒加载缓存（主要给测试 / 项目切换使用）。</summary>
        public void ClearExternalCache()
        {
            _externalDesigns.Clear();
        }

        // =============================================================
        //  Data Shortcut 解析
        // =============================================================

        /// <summary>
        /// 解析一条 <see cref="DataShortcut"/>：加载远端 <c>.roaddesign.json</c>，按 <see cref="DataShortcut.Kind"/>
        /// 与 <see cref="DataShortcut.SourceId"/> 返回对象。失败返回 null，不抛异常。
        /// <para>路径解析：若 <see cref="DataShortcut.SourcePath"/> 为相对路径，则相对于 <paramref name="rootDirectory"/>
        /// （通常来自 <see cref="RoadProject.RootDirectory"/>）；绝对路径直接使用。</para>
        /// <para>缓存：远端 <see cref="RoadDesign"/> 按绝对路径缓存，避免多次打开同一文件。</para>
        /// </summary>
        public DataShortcutResolveResult Resolve(DataShortcut shortcut, string rootDirectory)
        {
            if (shortcut == null) return DataShortcutResolveResult.Empty;
            if (string.IsNullOrWhiteSpace(shortcut.SourcePath)) return DataShortcutResolveResult.BrokenLink(shortcut, "SourcePath 为空");

            string abs = ResolvePath(rootDirectory, shortcut.SourcePath);
            if (!File.Exists(abs)) return DataShortcutResolveResult.BrokenLink(shortcut, $"找不到文件：{abs}");

            RoadDesign design;
            try
            {
                design = _externalDesigns.GetOrAdd(abs, p => _io.Load(p));
            }
            catch (Exception ex)
            {
                return DataShortcutResolveResult.BrokenLink(shortcut, $"读取失败：{ex.Message}");
            }
            if (design == null) return DataShortcutResolveResult.BrokenLink(shortcut, "远端 design 为空");

            object target = FindTarget(design, shortcut.Kind, shortcut.SourceId);
            if (target == null) return DataShortcutResolveResult.BrokenLink(shortcut, $"远端未找到 {shortcut.Kind}#{shortcut.SourceId:N}");

            return DataShortcutResolveResult.Ok(shortcut, design, target, abs);
        }

        private static object FindTarget(RoadDesign design, DataShortcutKind kind, Guid id)
        {
            if (design == null) return null;
            switch (kind)
            {
                case DataShortcutKind.Alignment:
                    foreach (var a in design.Alignments) if (a != null && a.Id == id) return a;
                    break;
                case DataShortcutKind.Profile:
                    foreach (var a in design.Alignments)
                        if (a?.Profiles != null)
                            foreach (var p in a.Profiles) if (p != null && p.Id == id) return p;
                    break;
                case DataShortcutKind.Template:
                    foreach (var t in design.Templates) if (t != null && t.Id == id) return t;
                    break;
                case DataShortcutKind.Corridor:
                    foreach (var c in design.Corridors) if (c != null && c.Id == id) return c;
                    break;
                case DataShortcutKind.Surface:
                    foreach (var s in design.Surfaces) if (s != null && s.Id == id) return s;
                    break;
                case DataShortcutKind.Intersection:
                    foreach (var i in design.Intersections) if (i != null && i.Id == id) return i;
                    break;
            }
            return null;
        }

        private static string ResolvePath(string rootDirectory, string maybeRelative)
        {
            if (string.IsNullOrWhiteSpace(maybeRelative)) return null;
            if (Path.IsPathRooted(maybeRelative)) return Path.GetFullPath(maybeRelative);
            if (string.IsNullOrWhiteSpace(rootDirectory)) return Path.GetFullPath(maybeRelative);
            return Path.GetFullPath(Path.Combine(rootDirectory, maybeRelative));
        }

        /// <summary>快照所有项目（诊断 / 调试用）。</summary>
        public IReadOnlyDictionary<string, RoadProject> Snapshot()
            => new Dictionary<string, RoadProject>(_projects);
    }

    /// <summary>
    /// <see cref="RoadProjectRegistry.Resolve(DataShortcut, string)"/> 的结果。
    /// </summary>
    public sealed class DataShortcutResolveResult
    {
        public DataShortcut Shortcut { get; }
        public bool IsOk { get; }
        public string Error { get; }

        /// <summary>被引用对象（Alignment / Profile / Template / Corridor / Surface / Intersection），失败时 null。</summary>
        public object Target { get; }

        /// <summary>远端已加载的 <see cref="RoadDesign"/>，失败时 null。</summary>
        public RoadDesign SourceDesign { get; }

        /// <summary>远端文件绝对路径；便于 UI 显示"来源 DWG"。</summary>
        public string ResolvedPath { get; }

        private DataShortcutResolveResult(bool ok, DataShortcut sc, RoadDesign design, object target, string path, string error)
        {
            IsOk = ok; Shortcut = sc; SourceDesign = design; Target = target; ResolvedPath = path; Error = error;
        }

        public static DataShortcutResolveResult Ok(DataShortcut sc, RoadDesign design, object target, string path)
            => new DataShortcutResolveResult(true, sc, design, target, path, null);

        public static DataShortcutResolveResult BrokenLink(DataShortcut sc, string error)
            => new DataShortcutResolveResult(false, sc, null, null, null, error);

        public static readonly DataShortcutResolveResult Empty
            = new DataShortcutResolveResult(false, null, null, null, null, "shortcut 为 null");
    }
}
