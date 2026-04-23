using System;
using System.Collections.ObjectModel;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 横断面条带内「填料」可编辑下拉的候选项，并支持用户新填内容回灌到候选项列表。
    /// </summary>
    public static class RoadMaterialFillPresets
    {
        static RoadMaterialFillPresets()
        {
            foreach (var s in new[]
                     {
                         "细粒式SBS改性沥青混凝土(AC-13C)", "粘层油(PC-3)", "中粒式沥青混凝土(AC-16C)", "粗粒式沥青混凝土AC-25C",
                         "乳化沥青稀浆封层(ES-3)", "透层油(AL(M)-2)",
                     })
                _surface.Add(s);

            foreach (var s in new[] { "水泥稳定碎石(5.0%)", "水泥稳定碎石(4.5%)", "14%灰土", "12%灰土" })
                _base.Add(s);

            _subbase.Add("路基处理8%灰土");
        }

        private static readonly ObservableCollection<string> _surface = new ObservableCollection<string>();
        private static readonly ObservableCollection<string> _base = new ObservableCollection<string>();
        private static readonly ObservableCollection<string> _subbase = new ObservableCollection<string>();

        public static ReadOnlyObservableCollection<string> Surface => new ReadOnlyObservableCollection<string>(_surface);
        public static ReadOnlyObservableCollection<string> Base => new ReadOnlyObservableCollection<string>(_base);
        public static ReadOnlyObservableCollection<string> Subbase => new ReadOnlyObservableCollection<string>(_subbase);

        public static void EnsureInList(StructureLayerKind kind, string material)
        {
            if (string.IsNullOrWhiteSpace(material)) return;
            var t = material.Trim();
            var list = ListFor(kind);
            foreach (var x in list)
            {
                if (string.Equals(x, t, StringComparison.Ordinal)) return;
            }
            list.Add(t);
        }

        public static ReadOnlyObservableCollection<string> For(StructureLayerKind kind) =>
            kind == StructureLayerKind.Base ? Base : kind == StructureLayerKind.Subbase ? Subbase : Surface;

        private static ObservableCollection<string> ListFor(StructureLayerKind kind) =>
            kind == StructureLayerKind.Base ? _base : kind == StructureLayerKind.Subbase ? _subbase : _surface;
    }
}
