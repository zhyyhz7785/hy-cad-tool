using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.Road.Events;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Models.Road.Civil;
using HyCADTool.Domain.Models.Road.Serialization;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// ????????????045 / M5?????
    ///
    /// ????????? <see cref="RoadDesignRegistry"/> ????????? **??????** ????????
    /// <list type="bullet">
    ///   <item>??? AutoCAD ????????? <see cref="RoadProject"/> ???????</item>
    ///   <item>????????? <c>.roaddesign.json</c> ?????????????????? DWG ???????? Data Shortcut ?????</item>
    ///   <item>???? <see cref="DataShortcut"/> ??? ?????????Alignment / Profile / ???????? <c>Guid</c> ?????</item>
    /// </list>
    ///
    /// ????**????**????????? <see cref="RoadDesignRegistry"/>??v1.x ??????????? <see cref="InnerRegistry"/>
    /// ?????????-DWG API??????????? DWG ??????????????
    ///
    /// ?????AutoCAD ??????????????????????????? <see cref="ConcurrentDictionary{TKey,TValue}"/> ??????????
    /// </summary>
    public sealed class RoadProjectRegistry
    {
        private readonly ConcurrentDictionary<string, RoadProject> _projects
            = new ConcurrentDictionary<string, RoadProject>(StringComparer.OrdinalIgnoreCase);

        // ????? .roaddesign.json?????? RoadProject.RootDirectory ????????????????????????
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

        /// <summary>?????? DWG ?????????????????????????</summary>
        public RoadDesignRegistry InnerRegistry { get; }

        /// <summary>
        /// ?????????????? <see cref="RoadProject"/>?????????????
        /// ?????? <c>LoadProjectForDocument</c>??<c>.roadproject.json</c> &gt; <c>.roaddesign.json</c> ?????????
        /// ???????????????? <see cref="RoadDesignRegistry.GetOrCreate"/> ????????? design ????????????? project???
        /// </summary>
        public RoadProject GetOrCreateForDocument(string documentName)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";

            return _projects.GetOrAdd(documentName, docName =>
            {
                var loaded = _io.LoadProjectForDocument(docName);
                if (loaded != null)
                {
                    // Sync primary design from loaded project into InnerRegistry.
                    var primaryDesign = loaded.GetPrimaryDesign();
                    if (primaryDesign != null) InnerRegistry.Replace(docName, primaryDesign);
                    return loaded;
                }

                var inner = InnerRegistry.GetOrCreate(docName);
                return RoadProjectMigration.WrapSingleDesign(inner);
            });
        }

        /// <summary>??????????????</summary>
        public bool TryGet(string documentName, out RoadProject project)
        {
            return _projects.TryGetValue(documentName ?? string.Empty, out project);
        }

        /// <summary>
        /// ???????????????? <see cref="RoadProject"/>???????????????????????????? <c>.roadproject.json</c> ??????
        /// ????? design ?????? <see cref="InnerRegistry"/>?????? <see cref="RoadDesignReloadedEvent"/>???
        /// </summary>
        public void Replace(string documentName, RoadProject newProject)
        {
            if (string.IsNullOrEmpty(documentName)) documentName = "default";
            if (newProject == null) throw new ArgumentNullException(nameof(newProject));

            _projects[documentName] = newProject;

            var primaryDesign = newProject.GetPrimaryDesign();
            if (primaryDesign != null) InnerRegistry.Replace(documentName, primaryDesign);
            else _eventBus.Publish(new RoadDesignReloadedEvent(newProject.Id));
        }

        /// <summary>
        /// ?????????????? Project + ?? Inner + ??????????? design ???????????????????????????????
        /// ????????????????????????????? docName ?????????????? project??????? design ??????????????????? <see cref="ClearExternalCache"/> ??????
        /// </summary>
        public void Remove(string documentName)
        {
            _projects.TryRemove(documentName ?? string.Empty, out _);
            InnerRegistry.Remove(documentName);
        }

        /// <summary>?????????????? design ????????????????????? / ?????????????????</summary>
        public void ClearExternalCache()
        {
            _externalDesigns.Clear();
        }

        // =============================================================
        //  Data Shortcut ????
        // =============================================================

        /// <summary>
        /// ??????? <see cref="DataShortcut"/>????????? <c>.roaddesign.json</c>????? <see cref="DataShortcut.Kind"/>
        /// ?? <see cref="DataShortcut.SourceId"/> ????????????????? null????????????
        /// <para>???????????? <see cref="DataShortcut.SourcePath"/> ??????????????????? <paramref name="rootDirectory"/>
        /// ?????????? <see cref="RoadProject.RootDirectory"/>??????????????????????</para>
        /// <para>????????? <see cref="RoadDesign"/> ?????????????????????????????????????</para>
        /// </summary>
        public DataShortcutResolveResult Resolve(DataShortcut shortcut, string rootDirectory)
        {
            if (shortcut == null) return DataShortcutResolveResult.Empty;
            if (string.IsNullOrWhiteSpace(shortcut.SourcePath)) return DataShortcutResolveResult.BrokenLink(shortcut, "SourcePath ??");

            string abs = ResolvePath(rootDirectory, shortcut.SourcePath);
            if (!File.Exists(abs)) return DataShortcutResolveResult.BrokenLink(shortcut, $"?????????????{abs}");

            RoadDesign design;
            try
            {
                design = _externalDesigns.GetOrAdd(abs, p => _io.Load(p));
            }
            catch (Exception ex)
            {
                return DataShortcutResolveResult.BrokenLink(shortcut, $"???????{ex.Message}");
            }
            if (design == null) return DataShortcutResolveResult.BrokenLink(shortcut, "??? design ??");

            object target = FindTarget(design, shortcut.Kind, shortcut.SourceId);
            if (target == null) return DataShortcutResolveResult.BrokenLink(shortcut, $"???????????? {shortcut.Kind}#{shortcut.SourceId:N}");

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

        /// <summary>????????????????????? / ????????????</summary>
        public IReadOnlyDictionary<string, RoadProject> Snapshot()
            => new Dictionary<string, RoadProject>(_projects);
    }

    /// <summary>
    /// <see cref="RoadProjectRegistry.Resolve(DataShortcut, string)"/> ???????????
    /// </summary>
    public sealed class DataShortcutResolveResult
    {
        public DataShortcut Shortcut { get; }
        public bool IsOk { get; }
        public string Error { get; }

        /// <summary>??????????Alignment / Profile / Template / Corridor / Surface / Intersection????????? null???</summary>
        public object Target { get; }

        /// <summary>??????????? <see cref="RoadDesign"/>??????? null???</summary>
        public RoadDesign SourceDesign { get; }

        /// <summary>????????????????? UI ????"?? DWG"???</summary>
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
            = new DataShortcutResolveResult(false, null, null, null, null, "shortcut ? null");
    }
}
