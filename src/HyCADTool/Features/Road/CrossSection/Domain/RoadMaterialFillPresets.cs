using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shell.Configuration.User;

namespace HyCADTool.Features.Road.CrossSection.Domain
{
    /// <summary>
    /// 横断面条带内「填料」可编辑下拉的候选项。
    /// 在内置默认项之上支持用户新增、删除，并可导出为用户设置增量。
    /// </summary>
    public static class RoadMaterialFillPresets
    {
        /// <summary>常用 AutoCAD 预定义填充名，供属性栏下拉（可编辑补充）。</summary>
        private static readonly string[] DefaultHatchPatternNames =
        {
            "SOLID",
            "ANSI31",
            "ANSI37",
            "AR-CONC",
            "AR-SAND",
            "GRAVEL",
            "EARTH",
        };

        private static readonly ReadOnlyCollection<string> _hatchPatternNames =
            new ReadOnlyCollection<string>(new List<string>(DefaultHatchPatternNames));

        /// <summary>填充图案名下拉数据（只读列表）。</summary>
        public static IReadOnlyList<string> HatchPatternNames => _hatchPatternNames;

        private static readonly string[] DefaultSurface =
        {
            "细粒式SBS改性沥青混凝土(AC-13C)",
            "粘层油(PC-3)",
            "中粒式沥青混凝土(AC-16C)",
            "粗粒式沥青混凝土AC-25C",
            "乳化沥青稀浆封层(ES-3)",
            "透层油(AL(M)-2)",
        };

        private static readonly string[] DefaultBase =
        {
            "水泥稳定碎石(5.0%)",
            "水泥稳定碎石(4.5%)",
            "14%灰土",
            "12%灰土",
        };

        private static readonly string[] DefaultSubbase =
        {
            "路基处理8%灰土",
        };

        private static readonly ObservableCollection<string> _surface = new ObservableCollection<string>();
        private static readonly ObservableCollection<string> _base = new ObservableCollection<string>();
        private static readonly ObservableCollection<string> _subbase = new ObservableCollection<string>();
        private static readonly ReadOnlyObservableCollection<string> _surfaceView = new ReadOnlyObservableCollection<string>(_surface);
        private static readonly ReadOnlyObservableCollection<string> _baseView = new ReadOnlyObservableCollection<string>(_base);
        private static readonly ReadOnlyObservableCollection<string> _subbaseView = new ReadOnlyObservableCollection<string>(_subbase);

        static RoadMaterialFillPresets()
        {
            RestoreDefaults();
        }

        public static ReadOnlyObservableCollection<string> Surface => _surfaceView;
        public static ReadOnlyObservableCollection<string> Base => _baseView;
        public static ReadOnlyObservableCollection<string> Subbase => _subbaseView;

        public static void RestoreDefaults()
        {
            ReplaceWith(_surface, DefaultSurface);
            ReplaceWith(_base, DefaultBase);
            ReplaceWith(_subbase, DefaultSubbase);
        }

        public static void ApplyUserSettings(RoadMaterialFillSettings settings)
        {
            ReplaceWith(_surface, Merge(DefaultSurface, settings?.SurfaceRemoved, settings?.SurfaceAdded));
            ReplaceWith(_base, Merge(DefaultBase, settings?.BaseRemoved, settings?.BaseAdded));
            ReplaceWith(_subbase, Merge(DefaultSubbase, settings?.SubbaseRemoved, settings?.SubbaseAdded));
        }

        public static RoadMaterialFillSettings ExportUserSettings()
        {
            return new RoadMaterialFillSettings
            {
                Version = 1,
                SurfaceAdded = ComputeAdded(_surface, DefaultSurface),
                SurfaceRemoved = ComputeRemoved(_surface, DefaultSurface),
                BaseAdded = ComputeAdded(_base, DefaultBase),
                BaseRemoved = ComputeRemoved(_base, DefaultBase),
                SubbaseAdded = ComputeAdded(_subbase, DefaultSubbase),
                SubbaseRemoved = ComputeRemoved(_subbase, DefaultSubbase),
            };
        }

        public static bool Contains(StructureLayerKind kind, string material)
        {
            var t = Normalize(material);
            if (t == null) return false;
            var list = ListFor(kind);
            foreach (var x in list)
            {
                if (string.Equals(x, t, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static bool TryAdd(StructureLayerKind kind, string material)
        {
            var t = Normalize(material);
            if (t == null || Contains(kind, t)) return false;
            ListFor(kind).Add(t);
            return true;
        }

        public static bool TryRemove(StructureLayerKind kind, string material)
        {
            var t = Normalize(material);
            if (t == null) return false;

            var list = ListFor(kind);
            for (int i = 0; i < list.Count; i++)
            {
                if (!string.Equals(list[i], t, StringComparison.Ordinal)) continue;
                list.RemoveAt(i);
                return true;
            }

            return false;
        }

        public static ReadOnlyObservableCollection<string> For(StructureLayerKind kind) =>
            kind == StructureLayerKind.Base ? Base : kind == StructureLayerKind.Subbase ? Subbase : Surface;

        /// <summary>
        /// 根据填料名称与层类别推荐 Hatch 图案；仅应在 <c>SectionFill.PatternName</c> 为空时写入。
        /// </summary>
        public static string SuggestHatchFor(StructureLayerKind kind, string material)
        {
            var m = (material ?? string.Empty).Trim();
            if (m.IndexOf("沥青", StringComparison.Ordinal) >= 0
                || m.IndexOf("稀浆", StringComparison.Ordinal) >= 0
                || m.IndexOf("AC-", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AR-CONC";
            if (m.IndexOf("水泥稳定", StringComparison.Ordinal) >= 0
                || m.IndexOf("水稳", StringComparison.Ordinal) >= 0)
                return "AR-SAND";
            if (m.IndexOf("灰土", StringComparison.Ordinal) >= 0)
                return "EARTH";
            if (m.IndexOf("油", StringComparison.Ordinal) >= 0 && m.IndexOf("沥青", StringComparison.Ordinal) < 0)
                return "SOLID";

            switch (kind)
            {
                case StructureLayerKind.Subbase:
                    return "EARTH";
                case StructureLayerKind.Base:
                    return "AR-SAND";
                case StructureLayerKind.Surface:
                    return "AR-CONC";
                default:
                    return "SOLID";
            }
        }

        private static ObservableCollection<string> ListFor(StructureLayerKind kind) =>
            kind == StructureLayerKind.Base ? _base : kind == StructureLayerKind.Subbase ? _subbase : _surface;

        private static IEnumerable<string> Merge(
            IEnumerable<string> defaults,
            IEnumerable<string> removed,
            IEnumerable<string> added)
        {
            var removedSet = new HashSet<string>(NormalizeMany(removed), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in NormalizeMany(defaults))
            {
                if (removedSet.Contains(item)) continue;
                if (seen.Add(item)) yield return item;
            }

            foreach (var item in NormalizeMany(added))
            {
                if (seen.Add(item)) yield return item;
            }
        }

        private static List<string> ComputeAdded(IEnumerable<string> current, IEnumerable<string> defaults)
        {
            var defaultSet = new HashSet<string>(NormalizeMany(defaults), StringComparer.Ordinal);
            return NormalizeMany(current)
                .Where(x => !defaultSet.Contains(x))
                .ToList();
        }

        private static List<string> ComputeRemoved(IEnumerable<string> current, IEnumerable<string> defaults)
        {
            var currentSet = new HashSet<string>(NormalizeMany(current), StringComparer.Ordinal);
            return NormalizeMany(defaults)
                .Where(x => !currentSet.Contains(x))
                .ToList();
        }

        private static IEnumerable<string> NormalizeMany(IEnumerable<string> values)
        {
            if (values == null) yield break;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var item = Normalize(value);
                if (item == null || !seen.Add(item)) continue;
                yield return item;
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim();
        }

        private static void ReplaceWith(ObservableCollection<string> target, IEnumerable<string> values)
        {
            target.Clear();
            foreach (var value in NormalizeMany(values))
                target.Add(value);
        }
    }
}
