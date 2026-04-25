using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Models.Road.Civil;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 项目树根 ViewModel（045 / M3）。
    ///
    /// 职责：
    /// <list type="bullet">
    ///   <item>接收当前 <see cref="RoadProject"/>，按 045 §4 「优化版树结构」动态建树；</item>
    ///   <item>暴露 <see cref="Roots"/>（单根 <c>RoadProject</c> 节点）供 <c>RoadProjectTreePanel</c> 的 TreeView 绑定；</item>
    ///   <item>支持 <see cref="SearchText"/> 过滤（按 <see cref="RoadTreeNode.Header"/> 前缀匹配）；</item>
    ///   <item>不做 AutoCAD 交互 —— 双击打开 / ZoomTo / 右键菜单等留给 M4。</item>
    /// </list>
    ///
    /// <para>线程：主 UI 线程构造 / 更新。<see cref="Rebuild"/> 完全替换 <see cref="Roots"/>，简单稳妥（节点数 &lt;&lt; 1000）。</para>
    /// </summary>
    public sealed class RoadProjectTreeViewModel : INotifyPropertyChanged
    {
        private RoadProject _project;
        private RoadTreeNode _selectedNode;
        private string _searchText;
        private string _statusLine = "（未加载项目）";
        private readonly IRoadTreeInteractionHandler _handler;

        /// <summary>
        /// 构造：允许外部注入交互分发 handler（默认 <see cref="NullRoadTreeInteractionHandler"/>，测试 / 设计时安全）。
        /// </summary>
        public RoadProjectTreeViewModel(IRoadTreeInteractionHandler handler = null)
        {
            _handler = handler ?? new NullRoadTreeInteractionHandler();

            OpenCommand = new RelayCommand<RoadTreeNode>(n => _handler.Open(n), n => n != null && CanOpen(n));
            ZoomToCommand = new RelayCommand<RoadTreeNode>(n => _handler.ZoomTo(n), n => n != null && HasTargetEntity(n));
            RenameCommand = new RelayCommand<RoadTreeNode>(n => _handler.Rename(n), n => n != null && CanRename(n));
            DeleteCommand = new RelayCommand<RoadTreeNode>(n => _handler.Delete(n), n => n != null && CanDelete(n));
            ExportLandXmlCommand = new RelayCommand<RoadTreeNode>(n => _handler.ExportLandXml(n), n => n != null && CanExportLandXml(n));

            NewAlignmentCommand = new RelayCommand(() => _handler.NewAlignment(), () => _project != null);
            AssignCrossSectionCommand = new RelayCommand(
                () => _handler.AssignCrossSection(SelectedNode), () => _project != null);
            GeneratePlanCommand = new RelayCommand(
                () => _handler.GeneratePlanFromTree(SelectedNode), () => _project != null);
            DetectIntersectionsCommand = new RelayCommand(() => _handler.DetectIntersections(), () => _project != null);
        }

        /// <summary>双击 / 回车：调用对应编辑器（ICommand 绑定 ContextMenu / KeyBinding）。</summary>
        public RelayCommand<RoadTreeNode> OpenCommand { get; }

        /// <summary>右键「ZoomTo」。</summary>
        public RelayCommand<RoadTreeNode> ZoomToCommand { get; }

        /// <summary>右键「重命名」。</summary>
        public RelayCommand<RoadTreeNode> RenameCommand { get; }

        /// <summary>右键「删除」。</summary>
        public RelayCommand<RoadTreeNode> DeleteCommand { get; }

        /// <summary>右键「导出 LandXML」。</summary>
        public RelayCommand<RoadTreeNode> ExportLandXmlCommand { get; }

        public RelayCommand NewAlignmentCommand { get; }
        public RelayCommand AssignCrossSectionCommand { get; }
        public RelayCommand GeneratePlanCommand { get; }
        public RelayCommand DetectIntersectionsCommand { get; }

        /// <summary>当前绑定的项目；赋值后自动 <see cref="Rebuild"/>。</summary>
        public RoadProject Project
        {
            get => _project;
            set
            {
                if (!ReferenceEquals(_project, value))
                {
                    _project = value;
                    OnPropertyChanged();
                    Rebuild();
                    NewAlignmentCommand?.RaiseCanExecuteChanged();
                    AssignCrossSectionCommand?.RaiseCanExecuteChanged();
                    GeneratePlanCommand?.RaiseCanExecuteChanged();
                    DetectIntersectionsCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>根节点集合（通常只含 1 个 <c>RoadProject</c> 节点；空项目时为空集合）。</summary>
        public ObservableCollection<RoadTreeNode> Roots { get; } = new ObservableCollection<RoadTreeNode>();

        /// <summary>当前选中节点（由 <c>TreeView</c> 双向绑定）。变化时联动 <see cref="IRoadTreeInteractionHandler.Highlight"/>。</summary>
        public RoadTreeNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (!ReferenceEquals(_selectedNode, value))
                {
                    _selectedNode = value;
                    OnPropertyChanged();

                    // M4：选中 → AutoCAD 高亮（null 或组节点时由 handler 自行判断忽略）
                    try { _handler?.Highlight(value); } catch { /* handler 异常不阻断选择 */ }

                    OpenCommand.RaiseCanExecuteChanged();
                    ZoomToCommand.RaiseCanExecuteChanged();
                    RenameCommand.RaiseCanExecuteChanged();
                    DeleteCommand.RaiseCanExecuteChanged();
                    ExportLandXmlCommand.RaiseCanExecuteChanged();
                    AssignCrossSectionCommand.RaiseCanExecuteChanged();
                    GeneratePlanCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>搜索文本（留空表示不过滤）。赋值后自动重建。</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    Rebuild();
                }
            }
        }

        /// <summary>状态栏摘要（节点计数 / 当前 DWG 来源）。</summary>
        public string StatusLine
        {
            get => _statusLine;
            private set
            {
                if (_statusLine != value)
                {
                    _statusLine = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 重建整棵树。对外暴露以便外部事件（如 <c>RoadDesignReloadedEvent</c>）触发刷新。
        /// 幂等：多次调用只会得到相同的最新快照。
        /// </summary>
        public void Rebuild()
        {
            Roots.Clear();

            if (_project == null)
            {
                StatusLine = "（未加载项目）";
                return;
            }

            var root = BuildProjectRoot(_project);
            if (root != null) Roots.Add(root);

            var total = CountNodes(root);
            var designCount = _project.Designs?.Count ?? 0;
            StatusLine = $"项目 '{_project.Name}'：{designCount} 个 design，共 {total} 个节点";
        }

        // =============================================================
        //  建树实现（按 045 §4 优化版树结构）
        // =============================================================

        private RoadTreeNode BuildProjectRoot(RoadProject project)
        {
            var root = new RoadTreeNode(
                RoadTreeNodeKind.Project,
                string.IsNullOrWhiteSpace(project.Name) ? "（未命名项目）" : project.Name,
                detail: project.Schema,
                tag: project)
            {
                TargetId = project.Id,
                IsExpanded = true
            };

            // 1) 项目元信息
            root.Add(new RoadTreeNode(
                RoadTreeNodeKind.ProjectMeta,
                "项目信息",
                detail: project.Schema,
                tag: project));

            // 2) 遍历 Designs，把每个 design 的分组展开到根下（单 DWG 项目无视觉差异）
            if (project.Designs != null)
            {
                foreach (var design in project.Designs)
                {
                    if (design == null) continue;
                    AppendDesignNodes(root, design);
                }
            }

            // 3) 引用节点（Data Shortcut，M5 前仅占位，无数据不渲染）
            if (project.Shortcuts != null && project.Shortcuts.Count > 0)
            {
                root.Add(BuildShortcutsGroup(project.Shortcuts));
            }

            // 4) 设计审核（派生视图）
            root.Add(new RoadTreeNode(
                RoadTreeNodeKind.CodeAudit,
                "设计审核",
                detail: "待执行",
                tag: null));

            FilterInPlace(root, _searchText);
            return root;
        }

        private void AppendDesignNodes(RoadTreeNode root, RoadDesign design)
        {
            // 曲面
            root.Add(BuildGroup(RoadTreeNodeKind.SurfacesGroup, "曲面", design.Surfaces,
                s => new RoadTreeNode(RoadTreeNodeKind.Surface, s.Name, detail: s.Kind.ToString(), tag: s) { TargetId = s.Id }));

            // 标准断面（Templates 提到顶层）
            root.Add(BuildGroup(RoadTreeNodeKind.TemplatesGroup, "标准断面", design.Templates,
                t => new RoadTreeNode(RoadTreeNodeKind.Template, SafeName(t.Name, "模板"), detail: null, tag: t) { TargetId = t.Id }));

            // 路线（每条 Alignment 下再分「几何设计」+「构造物与附属」）
            var alignGroup = new RoadTreeNode(
                RoadTreeNodeKind.AlignmentsGroup,
                "路线",
                detail: $"{design.Alignments.Count}")
            { IsExpanded = true };
            foreach (var aln in design.Alignments)
            {
                if (aln == null) continue;
                alignGroup.Add(BuildAlignmentNode(design, aln));
            }
            root.Add(alignGroup);

            // 平交
            root.Add(BuildGroup(RoadTreeNodeKind.IntersectionsGroup, "道路平交", design.Intersections,
                i => new RoadTreeNode(RoadTreeNodeKind.Intersection, SafeName(i.Name, "交叉口"), detail: $"{i.Legs.Count}臂", tag: i) { TargetId = i.Id }));

            // 立交
            root.Add(BuildGroup(RoadTreeNodeKind.InterchangesGroup, "立交", design.Interchanges,
                x => new RoadTreeNode(RoadTreeNodeKind.Interchange, SafeName(x.Name, "立交"), detail: x.Kind.ToString(), tag: x) { TargetId = x.Id }));

            // 配景
            root.Add(BuildGroup(RoadTreeNodeKind.LandscapesGroup, "配景", design.Landscapes,
                l => new RoadTreeNode(RoadTreeNodeKind.Landscape, SafeName(l.Name, "配景"), detail: l.Kind.ToString(), tag: l) { TargetId = l.Id }));

            // 其他（路外交通设施）
            root.Add(BuildGroup(RoadTreeNodeKind.OthersGroup, "其他", design.ExternalTrafficFacilities,
                f => new RoadTreeNode(RoadTreeNodeKind.TrafficFacility, SafeName(f.Name, "交通设施"), detail: f.Kind.ToString(), tag: f) { TargetId = f.Id }));

            // 地质
            root.Add(BuildGroup(RoadTreeNodeKind.GeologiesGroup, "地质", design.Geologies,
                g => new RoadTreeNode(RoadTreeNodeKind.Geology, SafeName(g.Name, "地质"), detail: g.Kind.ToString(), tag: g) { TargetId = g.Id }));

            // 管线占位（v2 议题）
            root.Add(new RoadTreeNode(RoadTreeNodeKind.PipesGroup, "管线", detail: "v2 规划", tag: null));
        }

        private RoadTreeNode BuildAlignmentNode(RoadDesign design, Alignment aln)
        {
            var node = new RoadTreeNode(
                RoadTreeNodeKind.Alignment,
                SafeName(aln.Name, "未命名路线"),
                detail: $"起K{aln.StartStation:F0}",
                tag: aln)
            { TargetId = aln.Id };

            // ============ 几何设计组 ============
            var geom = new RoadTreeNode(RoadTreeNodeKind.AlignmentGeometryGroup, "几何设计", detail: null)
            {
                IsExpanded = true
            };

            // 桩号体系
            geom.Add(BuildGroup(RoadTreeNodeKind.StationEquationGroup, "桩号体系",
                aln.StationEquations ?? new List<HyCADTool.Refactored.Domain.ValueObjects.Road.StationEquation>(),
                eq => new RoadTreeNode(RoadTreeNodeKind.StationEquation,
                    $"K{eq.BeforeRaw:F3} → K{eq.AheadStation:F3}", detail: null, tag: eq)));

            // 纵断面 Profiles + 每个 Profile 下可再挂 Sheets
            var profGroup = new RoadTreeNode(RoadTreeNodeKind.ProfilesGroup, "纵断面", detail: $"{aln.Profiles.Count}");
            foreach (var prof in aln.Profiles)
            {
                if (prof == null) continue;
                var profNode = new RoadTreeNode(
                    RoadTreeNodeKind.Profile,
                    SafeName(prof.Name, prof.IsDesignProfile ? "设计纵断" : "自然纵断"),
                    detail: $"PVI={prof.Vertices.Count}",
                    tag: prof)
                { TargetId = prof.Id };

                // 纵断设计图 Sheets
                if (prof.Sheets != null && prof.Sheets.Count > 0)
                {
                    var sheetsGroup = new RoadTreeNode(RoadTreeNodeKind.ProfileSheetsGroup, "纵断设计图", detail: $"{prof.Sheets.Count}");
                    foreach (var sh in prof.Sheets)
                    {
                        sheetsGroup.Add(new RoadTreeNode(
                            RoadTreeNodeKind.ProfileSheet,
                            SafeName(sh.Name, $"{sh.StartStation:F0}~{sh.EndStation:F0}"),
                            detail: $"1:{sh.ScaleHorizontal:F0}/1:{sh.ScaleVertical:F0}",
                            tag: sh)
                        { TargetId = sh.Id });
                    }
                    profNode.Add(sheetsGroup);
                }

                profGroup.Add(profNode);
            }
            geom.Add(profGroup);

            // 横断面：按 AlignmentId（若 Template 记录了归属字段；目前 Template 没有，先全量展示所有 templates 作为"可引用模板"）
            geom.Add(BuildCrossSectionGroup(design, aln));

            // 走廊：按 AlignmentId 过滤
            geom.Add(BuildCorridorGroup(design, aln));

            node.Add(geom);

            // ============ 构造物与附属组 ============
            var assets = new RoadTreeNode(RoadTreeNodeKind.AlignmentAssetsGroup, "构造物与附属", detail: null);

            assets.Add(BuildGroup(RoadTreeNodeKind.BridgesGroup, "桥梁",
                FilterByAlignment(design.Bridges, aln.Id, b => b.OwnerAlignmentId),
                b => new RoadTreeNode(RoadTreeNodeKind.Bridge, SafeName(b.Name, "桥梁"),
                    detail: $"K{b.StartStation:F0}~K{b.EndStation:F0}", tag: b) { TargetId = b.Id }));

            assets.Add(BuildGroup(RoadTreeNodeKind.TunnelsGroup, "隧道",
                FilterByAlignment(design.Tunnels, aln.Id, t => t.OwnerAlignmentId),
                t => new RoadTreeNode(RoadTreeNodeKind.Tunnel, SafeName(t.Name, "隧道"),
                    detail: $"K{t.StartStation:F0}~K{t.EndStation:F0}", tag: t) { TargetId = t.Id }));

            assets.Add(BuildGroup(RoadTreeNodeKind.CulvertsGroup, "涵洞",
                FilterByAlignment(design.Culverts, aln.Id, c => c.OwnerAlignmentId),
                c => new RoadTreeNode(RoadTreeNodeKind.Culvert, SafeName(c.Name, "涵洞"),
                    detail: $"K{c.Station:F0}", tag: c) { TargetId = c.Id }));

            // 路内交通附属（v1.x 已有服务，v2.0 做占位分组节点，计数为 0，待命令填充）
            assets.Add(new RoadTreeNode(RoadTreeNodeKind.InternalTrafficGroup, "路内交通附属", detail: "0"));

            node.Add(assets);
            return node;
        }

        private RoadTreeNode BuildCrossSectionGroup(RoadDesign design, Alignment aln)
        {
            int n = 0;
            var group = new RoadTreeNode(
                RoadTreeNodeKind.CrossSectionsGroup,
                "横断面",
                detail: null);

            if (aln.CrossSectionAssignments != null && aln.CrossSectionAssignments.Count > 0)
            {
                foreach (var a in aln.CrossSectionAssignments)
                {
                    if (a == null) continue;
                    var tpl = design.Templates.FirstOrDefault(t => t != null && t.Id == a.TemplateId);
                    string tplName = tpl != null ? SafeName(tpl.Name, "模板") : "(未找到模板)";
                    double lo = System.Math.Min(a.StartStation, a.EndStation);
                    double hi = System.Math.Max(a.StartStation, a.EndStation);
                    group.Add(new RoadTreeNode(
                        RoadTreeNodeKind.CrossSection,
                        $"{lo:F0}~{hi:F0} m → {tplName}",
                        detail: a.Note,
                        tag: tpl ?? (object)a)
                    { TargetId = tpl?.Id ?? a.Id });
                    n++;
                }
            }
            else
            {
                foreach (var t in design.Templates)
                {
                    if (t == null) continue;
                    group.Add(new RoadTreeNode(RoadTreeNodeKind.CrossSection, SafeName(t.Name, "模板"), detail: "可引用", tag: t) { TargetId = t.Id });
                    n++;
                }
            }

            group.Detail = n.ToString();
            return group;
        }

        private RoadTreeNode BuildCorridorGroup(RoadDesign design, Alignment aln)
        {
            var group = new RoadTreeNode(RoadTreeNodeKind.CorridorsGroup, "走廊", detail: null);
            int n = 0;
            foreach (var c in design.Corridors)
            {
                if (c == null) continue;
                if (!IsCorridorOwnedBy(c, aln.Id)) continue;
                group.Add(new RoadTreeNode(RoadTreeNodeKind.Corridor, SafeName(c.Name, "走廊"), detail: null, tag: c) { TargetId = c.Id });
                n++;
            }
            group.Detail = n.ToString();
            return group;
        }

        private static bool IsCorridorOwnedBy(Corridor corridor, Guid alignmentId)
            => corridor != null && corridor.AlignmentId == alignmentId;

        private static IEnumerable<T> FilterByAlignment<T>(IEnumerable<T> source, Guid alignmentId, Func<T, Guid?> owner)
        {
            if (source == null) yield break;
            foreach (var x in source)
            {
                if (x == null) continue;
                var g = owner(x);
                if (!g.HasValue || g.Value == alignmentId) yield return x;
            }
        }

        private static RoadTreeNode BuildGroup<T>(RoadTreeNodeKind kind, string header, IEnumerable<T> items, Func<T, RoadTreeNode> project)
        {
            var group = new RoadTreeNode(kind, header, detail: null);
            int n = 0;
            if (items != null)
            {
                foreach (var it in items)
                {
                    if (it == null) continue;
                    var child = project(it);
                    if (child != null)
                    {
                        group.Children.Add(child);
                        n++;
                    }
                }
            }
            group.Detail = n.ToString();
            return group;
        }

        private static RoadTreeNode BuildShortcutsGroup(IEnumerable<DataShortcut> shortcuts)
        {
            var group = new RoadTreeNode(RoadTreeNodeKind.ProjectMeta, "引用（Data Shortcut）", detail: null);
            int n = 0;
            foreach (var sc in shortcuts)
            {
                if (sc == null) continue;
                group.Children.Add(new RoadTreeNode(
                    RoadTreeNodeKind.ProjectMeta,
                    SafeName(sc.LocalAlias, sc.Kind.ToString()),
                    detail: System.IO.Path.GetFileName(sc.SourcePath ?? string.Empty),
                    tag: sc));
                n++;
            }
            group.Detail = n.ToString();
            return group;
        }

        // =============================================================
        //  搜索过滤（就地裁剪，不记结构快照）
        // =============================================================

        /// <summary>
        /// 按 <paramref name="keyword"/> 过滤 <paramref name="node"/> 的整个子树：
        /// 只要子树存在至少一个 Header 命中 keyword 的节点，则保留该路径；否则剪掉。
        /// 空 keyword 直接展开关键子树。
        /// </summary>
        private static bool FilterInPlace(RoadTreeNode node, string keyword)
        {
            if (node == null) return false;
            if (string.IsNullOrWhiteSpace(keyword)) return true;

            var kept = new List<RoadTreeNode>();
            foreach (var c in node.Children)
            {
                if (FilterInPlace(c, keyword)) kept.Add(c);
            }
            bool selfHit = node.Header != null && node.Header.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

            if (kept.Count != node.Children.Count)
            {
                node.Children.Clear();
                foreach (var c in kept) node.Children.Add(c);
            }
            if (kept.Count > 0)
            {
                node.IsExpanded = true;
                return true;
            }
            return selfHit;
        }

        private static int CountNodes(RoadTreeNode n)
        {
            if (n == null) return 0;
            int c = 1;
            foreach (var ch in n.Children) c += CountNodes(ch);
            return c;
        }

        private static string SafeName(string raw, string fallback)
            => string.IsNullOrWhiteSpace(raw) ? fallback : raw;

        // =============================================================
        //  045 / M4：CanExecute 决策（按节点 Kind 白名单）
        // =============================================================

        /// <summary>
        /// 哪些 Kind 的节点支持「双击打开」（有对应编辑器）。
        /// 组节点 / 派生视图节点返回 false。
        /// </summary>
        public static bool CanOpen(RoadTreeNode node)
        {
            if (node == null) return false;
            switch (node.Kind)
            {
                case RoadTreeNodeKind.Alignment:
                case RoadTreeNodeKind.Profile:
                case RoadTreeNodeKind.ProfileSheet:
                case RoadTreeNodeKind.Template:
                case RoadTreeNodeKind.CrossSection:
                case RoadTreeNodeKind.Corridor:
                case RoadTreeNodeKind.Intersection:
                case RoadTreeNodeKind.Interchange:
                case RoadTreeNodeKind.Surface:
                case RoadTreeNodeKind.Bridge:
                case RoadTreeNodeKind.Tunnel:
                case RoadTreeNodeKind.Culvert:
                case RoadTreeNodeKind.ProjectMeta:
                case RoadTreeNodeKind.CodeAudit:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>哪些 Kind 的节点挂载着 AutoCAD 实体（可 ZoomTo / Highlight）。</summary>
        public static bool HasTargetEntity(RoadTreeNode node)
        {
            if (node == null) return false;
            switch (node.Kind)
            {
                case RoadTreeNodeKind.Alignment:
                case RoadTreeNodeKind.Intersection:
                case RoadTreeNodeKind.Corridor:
                case RoadTreeNodeKind.Interchange:
                case RoadTreeNodeKind.Bridge:
                case RoadTreeNodeKind.Tunnel:
                case RoadTreeNodeKind.Culvert:
                case RoadTreeNodeKind.Surface:
                case RoadTreeNodeKind.TrafficFacility:
                case RoadTreeNodeKind.Landscape:
                case RoadTreeNodeKind.Geology:
                    return node.TargetId != Guid.Empty;
                default:
                    return false;
            }
        }

        /// <summary>哪些 Kind 可以重命名（实例型节点）。</summary>
        public static bool CanRename(RoadTreeNode node)
        {
            if (node == null) return false;
            switch (node.Kind)
            {
                case RoadTreeNodeKind.Project:
                case RoadTreeNodeKind.Alignment:
                case RoadTreeNodeKind.Profile:
                case RoadTreeNodeKind.ProfileSheet:
                case RoadTreeNodeKind.Template:
                case RoadTreeNodeKind.Corridor:
                case RoadTreeNodeKind.Intersection:
                case RoadTreeNodeKind.Interchange:
                case RoadTreeNodeKind.Surface:
                case RoadTreeNodeKind.Bridge:
                case RoadTreeNodeKind.Tunnel:
                case RoadTreeNodeKind.Culvert:
                case RoadTreeNodeKind.Landscape:
                case RoadTreeNodeKind.TrafficFacility:
                case RoadTreeNodeKind.Geology:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>哪些 Kind 可以删除（与可重命名对齐，但不含 Project 根）。</summary>
        public static bool CanDelete(RoadTreeNode node)
        {
            if (node == null || node.Kind == RoadTreeNodeKind.Project) return false;
            return CanRename(node) && node.Kind != RoadTreeNodeKind.Project;
        }

        /// <summary>哪些 Kind 支持导出 LandXML（Civil 3D 对齐：Alignment / Profile / Surface）。</summary>
        public static bool CanExportLandXml(RoadTreeNode node)
        {
            if (node == null) return false;
            switch (node.Kind)
            {
                case RoadTreeNodeKind.Project:          // 整包导出
                case RoadTreeNodeKind.Alignment:
                case RoadTreeNodeKind.Profile:
                case RoadTreeNodeKind.Surface:
                    return true;
                default:
                    return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
