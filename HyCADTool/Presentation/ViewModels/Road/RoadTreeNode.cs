using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyCADTool.Presentation.ViewModels.Road
{
    /// <summary>
    /// 项目树节点种类（045 / M3）。
    ///
    /// 一个枚举值对应「一类节点」—— 组标签（根、路线组、纵断面组…）与实例（具体 Alignment、Profile…）都在同一类型里，
    /// 由 <see cref="RoadTreeNode.Kind"/> 区分。
    /// UI 根据 <c>Kind</c> 选择图标与右键菜单命令集。
    /// </summary>
    public enum RoadTreeNodeKind
    {
        /// <summary>项目根节点（<c>RoadProject</c>）。</summary>
        Project = 0,

        /// <summary>项目元信息（名称 / Schema / CRS）。</summary>
        ProjectMeta = 1,

        /// <summary>曲面组（Surfaces 集合容器）。</summary>
        SurfacesGroup = 10,
        /// <summary>单条曲面。</summary>
        Surface = 11,

        /// <summary>标准断面组（Templates 集合容器）。</summary>
        TemplatesGroup = 20,
        /// <summary>单个模板。</summary>
        Template = 21,

        /// <summary>路线组（Alignments 集合容器）。</summary>
        AlignmentsGroup = 30,
        /// <summary>单条路线。</summary>
        Alignment = 31,

        /// <summary>路线下「几何设计」大类分组节点。</summary>
        AlignmentGeometryGroup = 32,
        /// <summary>路线下「构造物与附属」大类分组节点。</summary>
        AlignmentAssetsGroup = 33,

        /// <summary>桩号体系分组（StationEquations）。</summary>
        StationEquationGroup = 34,
        /// <summary>单条桩号方程。</summary>
        StationEquation = 35,

        /// <summary>纵断面集合分组。</summary>
        ProfilesGroup = 40,
        /// <summary>单个 Profile（EG / FG）。</summary>
        Profile = 41,

        /// <summary>纵断设计图分幅分组。</summary>
        ProfileSheetsGroup = 42,
        /// <summary>单幅纵断设计图。</summary>
        ProfileSheet = 43,

        /// <summary>横断面分组（按 Alignment 过滤 Templates）。</summary>
        CrossSectionsGroup = 50,
        /// <summary>横断面实例（Template 引用）。</summary>
        CrossSection = 51,

        /// <summary>走廊分组（Corridors 按 AlignmentId 过滤）。</summary>
        CorridorsGroup = 60,
        /// <summary>单条走廊。</summary>
        Corridor = 61,

        /// <summary>桥梁分组。</summary>
        BridgesGroup = 70,
        /// <summary>单座桥梁。</summary>
        Bridge = 71,

        /// <summary>隧道分组。</summary>
        TunnelsGroup = 72,
        /// <summary>单座隧道。</summary>
        Tunnel = 73,

        /// <summary>涵洞分组。</summary>
        CulvertsGroup = 74,
        /// <summary>单座涵洞。</summary>
        Culvert = 75,

        /// <summary>路内交通附属分组（LaneMarking/StopLine/Crosswalk 等，按 AlignmentId）。</summary>
        InternalTrafficGroup = 76,

        /// <summary>平交（Intersections）分组。</summary>
        IntersectionsGroup = 80,
        /// <summary>单个平交口。</summary>
        Intersection = 81,

        /// <summary>立交（Interchanges）分组。</summary>
        InterchangesGroup = 90,
        /// <summary>单个立交。</summary>
        Interchange = 91,

        /// <summary>配景（Landscapes）分组。</summary>
        LandscapesGroup = 100,
        /// <summary>单个配景。</summary>
        Landscape = 101,

        /// <summary>其他（路外交通设施）分组。</summary>
        OthersGroup = 110,
        /// <summary>单个路外交通设施。</summary>
        TrafficFacility = 111,

        /// <summary>地质分组。</summary>
        GeologiesGroup = 120,
        /// <summary>单个地质要素。</summary>
        Geology = 121,

        /// <summary>管线（v2 议题占位）。</summary>
        PipesGroup = 130,

        /// <summary>设计审核（派生视图节点）。</summary>
        CodeAudit = 140,
    }

    /// <summary>
    /// 项目树节点（045 / M3）。
    ///
    /// 单一可变节点类型，所有节点类型（组 / 实例）共用；由 <see cref="Kind"/> 决定渲染行为与菜单；
    /// 避免 HierarchicalDataTemplate 在多类型间切换导致的 XAML 体积膨胀与 PaletteSet 宿主下的
    /// 资源字典定位问题（参见 <c>wpf-paletteset-resource-pitfalls</c> skill）。
    ///
    /// <para>线程：仅 UI 线程构造 / 修改。<see cref="Children"/> 是 ObservableCollection。</para>
    /// </summary>
    public sealed class RoadTreeNode : INotifyPropertyChanged
    {
        private string _header;
        private string _detail;
        private bool _isExpanded;
        private bool _isSelected;

        public RoadTreeNodeKind Kind { get; }

        /// <summary>显示名。</summary>
        public string Header
        {
            get => _header;
            set { if (_header != value) { _header = value; OnPropertyChanged(); } }
        }

        /// <summary>次级信息（计数 / 桩号范围 / 来源 DWG 等，UI 右侧淡色显示）。</summary>
        public string Detail
        {
            get => _detail;
            set { if (_detail != value) { _detail = value; OnPropertyChanged(); } }
        }

        /// <summary>后端对象引用（Alignment / Profile / Template / Intersection 等）。可为 null（组节点）。</summary>
        public object Tag { get; set; }

        /// <summary>后端对象 <c>Guid</c>（对应 <c>Alignment.Id</c> 等；Tag 为空时为 <see cref="Guid.Empty"/>）。</summary>
        public Guid TargetId { get; set; }

        /// <summary>子节点（TreeView 按此展开）。</summary>
        public ObservableCollection<RoadTreeNode> Children { get; } = new ObservableCollection<RoadTreeNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public RoadTreeNode(RoadTreeNodeKind kind, string header, string detail = null, object tag = null)
        {
            Kind = kind;
            _header = header;
            _detail = detail;
            Tag = tag;
        }

        /// <summary>便捷：追加子节点并返回自身（链式构建）。</summary>
        public RoadTreeNode Add(RoadTreeNode child)
        {
            if (child != null) Children.Add(child);
            return this;
        }

        public override string ToString() => $"RoadTreeNode[{Kind} '{Header}', Children={Children.Count}]";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
