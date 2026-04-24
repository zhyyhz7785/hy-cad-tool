using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 横断面预设管理服务（M7.1）：内置预设（来自 <see cref="CrossSectionPresets"/>）+ 用户预设（JSON 文件）。
    ///
    /// <para><b>用户预设存储</b></para>
    /// 目录：<c>%AppData%/HyCAD/presets/crosssection/</c>（可通过 <see cref="UserPresetDirectory"/> 重定向，方便测试）。
    /// 单文件 = 单预设（文件名 = 安全化后的预设名 + <c>.json</c>）；一次 Save 写一个 <see cref="CrossSectionLayout"/>。
    ///
    /// <para><b>与 CrossSectionDesignerWindow 的集成</b></para>
    /// 顶部"预设"下拉 + 顶栏"另存为方案"/"加载方案" 按钮（M7.1 UI）调用本服务；
    /// VM 不直接访问 AppData，只接受 <see cref="PresetDescriptor"/> 列表。
    /// </summary>
    public sealed class CrossSectionPresetService
    {
        private readonly string _userDir;
        private readonly JsonSerializerSettings _settings;

        /// <summary>默认用户预设目录：<c>%AppData%/HyCAD/presets/crosssection/</c>。</summary>
        public static string DefaultUserDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCAD", "presets", "crosssection");

        /// <summary>实际使用的用户预设目录。</summary>
        public string UserPresetDirectory => _userDir;

        public CrossSectionPresetService(string userPresetDirectory = null)
        {
            _userDir = string.IsNullOrWhiteSpace(userPresetDirectory)
                ? DefaultUserDirectory
                : userPresetDirectory;
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Culture = System.Globalization.CultureInfo.InvariantCulture,
            };
        }

        /// <summary>
        /// 汇总所有可见预设：内置（来自 <see cref="CrossSectionPresets.All"/>）+ 用户。
        /// 列表顺序：内置在前、用户在后；用户内按文件名字母序。
        /// </summary>
        public IReadOnlyList<PresetDescriptor> LoadAll()
        {
            var result = new List<PresetDescriptor>(CrossSectionPresets.All);

            if (!Directory.Exists(_userDir)) return result;

            foreach (var file in Directory.EnumerateFiles(_userDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var descr = TryLoadFile(file);
                if (descr != null) result.Add(descr);
            }
            return result;
        }

        /// <summary>
        /// 加载单个用户预设文件。找不到或解析失败返回 null。
        /// </summary>
        public CrossSectionLayout LoadByName(string presetKey)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) return null;

            // 内置
            var built = CrossSectionPresets.All.FirstOrDefault(p => string.Equals(p.Key, presetKey, StringComparison.OrdinalIgnoreCase));
            if (built != null) return built.Create();

            // 用户
            string path = Path.Combine(_userDir, SanitizeFileName(presetKey) + ".json");
            if (File.Exists(path))
            {
                var descr = TryLoadFile(path);
                return descr?.Create();
            }

            // 兼容：若文件名与 key 不一致（例如按显示名覆盖后复用旧文件路径），回退扫描 Key。
            if (!Directory.Exists(_userDir)) return null;
            foreach (var file in Directory.EnumerateFiles(_userDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!TryReadPresetFile(file, out var pf) || pf == null) continue;
                if (!string.Equals((pf.Key ?? string.Empty).Trim(), presetKey.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                return pf.Layout?.ToLayout();
            }
            return null;
        }

        /// <summary>
        /// 把 <paramref name="layout"/> 另存为用户预设。<paramref name="presetKey"/> 会被安全化为文件名。
        /// <para>若已存在同名文件，直接覆盖。</para>
        /// 返回写入的绝对路径。
        /// </summary>
        public string SaveUserPreset(string presetKey, string displayName, CrossSectionLayout layout)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) throw new ArgumentException("presetKey 不能为空", nameof(presetKey));
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Directory.CreateDirectory(_userDir);
            string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? presetKey : displayName.Trim();
            string path = TryFindUserPresetPathByDisplayName(normalizedDisplayName)
                          ?? Path.Combine(_userDir, SanitizeFileName(presetKey) + ".json");
            string persistedKey = presetKey;
            if (File.Exists(path) && TryReadPresetFile(path, out var existing) && existing != null)
            {
                if (!string.IsNullOrWhiteSpace(existing.Key))
                    persistedKey = existing.Key;
            }

            var dto = CrossSectionLayoutDto.FromLayout(layout);
            var file = new PresetFile
            {
                Key = persistedKey,
                DisplayName = normalizedDisplayName,
                Layout = dto,
            };

            string content = JsonConvert.SerializeObject(file, _settings);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        /// <summary>删除用户预设。找不到文件返回 false，非法入参返回 false。</summary>
        public bool DeleteUserPreset(string presetKey)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) return false;
            if (IsBuiltInPresetKey(presetKey)) return false;
            string path = Path.Combine(_userDir, SanitizeFileName(presetKey) + ".json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>按显示名删除用户预设（内置预设不可删）。</summary>
        public bool DeleteUserPresetByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return false;
            string path = TryFindUserPresetPathByDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>是否为内置预设 key。</summary>
        public bool IsBuiltInPresetKey(string presetKey)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) return false;
            return CrossSectionPresets.All.Any(p => string.Equals(p.Key, presetKey, StringComparison.OrdinalIgnoreCase));
        }

        // =============================================================================
        // 私有
        // =============================================================================

        private PresetDescriptor TryLoadFile(string path)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                var file = JsonConvert.DeserializeObject<PresetFile>(text, _settings);
                if (file == null || file.Layout == null) return null;

                string key = string.IsNullOrWhiteSpace(file.Key) ? Path.GetFileNameWithoutExtension(path) : file.Key;
                string display = string.IsNullOrWhiteSpace(file.DisplayName) ? key : file.DisplayName;
                var dto = file.Layout;
                return new PresetDescriptor(key, display, () => dto.ToLayout());
            }
            catch
            {
                return null;
            }
        }

        private string TryFindUserPresetPathByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            if (!Directory.Exists(_userDir)) return null;
            string normalized = displayName.Trim();

            foreach (var path in Directory.EnumerateFiles(_userDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!TryReadPresetFile(path, out var file) || file == null) continue;
                if (string.Equals((file.DisplayName ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }

        private bool TryReadPresetFile(string path, out PresetFile file)
        {
            file = null;
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                file = JsonConvert.DeserializeObject<PresetFile>(text, _settings);
                return file != null;
            }
            catch
            {
                return false;
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        // =============================================================================
        // JSON DTO
        // =============================================================================

        internal sealed class PresetFile
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public CrossSectionLayoutDto Layout { get; set; }
        }

        internal sealed class CrossSectionLayoutDto
        {
            public List<CrossSectionBandDto> LeftBands { get; set; } = new List<CrossSectionBandDto>();
            public List<CrossSectionBandDto> RightBands { get; set; } = new List<CrossSectionBandDto>();
            public double CenterMedianWidth { get; set; }
            public int DesignSpeed { get; set; }
            public int ScaleDenominator { get; set; }
            public string Title { get; set; }
            public double PlanStripLength { get; set; } = 6.5;
            public double CenterlinePosition { get; set; } = double.NaN;
            public double ProfileElevationOffset { get; set; }
            public bool IsEmptyAssembly { get; set; }
            public double StationStart { get; set; }
            public double StationEnd { get; set; }
            public List<StationRangeItemDto> AdditionalStationRanges { get; set; } = new List<StationRangeItemDto>();
            public double MedianLeftSubWidth { get; set; }
            public double MedianLeftCrossSlopePct { get; set; }
            public double MedianRightCrossSlopePct { get; set; }
            public double MedianLeftOuterElevationDiff { get; set; }
            public double MedianLeftInnerElevationDiff { get; set; }
            public double MedianRightInnerElevationDiff { get; set; }
            public double MedianRightOuterElevationDiff { get; set; }
            public bool ElevationDiffLocked { get; set; } = true;

            public static CrossSectionLayoutDto FromLayout(CrossSectionLayout l)
            {
                var dto = new CrossSectionLayoutDto
                {
                    LeftBands = l.LeftBands.Select(CrossSectionBandDto.From).ToList(),
                    RightBands = l.RightBands.Select(CrossSectionBandDto.From).ToList(),
                    CenterMedianWidth = l.CenterMedianWidth,
                    DesignSpeed = l.DesignSpeed,
                    ScaleDenominator = l.ScaleDenominator,
                    Title = l.Title,
                    PlanStripLength = l.PlanStripLength,
                    CenterlinePosition = l.CenterlinePosition,
                    ProfileElevationOffset = l.ProfileElevationOffset,
                    IsEmptyAssembly = l.IsEmptyAssembly,
                    StationStart = l.StationStart,
                    StationEnd = l.StationEnd,
                    MedianLeftSubWidth = l.MedianLeftSubWidth,
                    MedianLeftCrossSlopePct = l.MedianLeftCrossSlopePct,
                    MedianRightCrossSlopePct = l.MedianRightCrossSlopePct,
                    MedianLeftOuterElevationDiff = l.MedianLeftOuterElevationDiff,
                    MedianLeftInnerElevationDiff = l.MedianLeftInnerElevationDiff,
                    MedianRightInnerElevationDiff = l.MedianRightInnerElevationDiff,
                    MedianRightOuterElevationDiff = l.MedianRightOuterElevationDiff,
                    ElevationDiffLocked = l.ElevationDiffLocked,
                };
                if (l.AdditionalStationRanges != null && l.AdditionalStationRanges.Count > 0)
                {
                    foreach (var a in l.AdditionalStationRanges)
                        dto.AdditionalStationRanges.Add(new StationRangeItemDto { StartM = a.StartM, EndM = a.EndM });
                }
                return dto;
            }

            public CrossSectionLayout ToLayout()
            {
                IReadOnlyList<StationRangeSpan> extra = null;
                if (AdditionalStationRanges != null && AdditionalStationRanges.Count > 0)
                {
                    extra = AdditionalStationRanges
                        .Select(x => new StationRangeSpan(x.StartM, x.EndM))
                        .ToList();
                }
                return CrossSectionLayout.Create(
                    LeftBands.Select(b => b.ToBand(BandSide.Left)).ToList().AsReadOnly(),
                    RightBands.Select(b => b.ToBand(BandSide.Right)).ToList().AsReadOnly(),
                    CenterMedianWidth,
                    DesignSpeed > 0 ? DesignSpeed : 50,
                    ScaleDenominator > 0 ? ScaleDenominator : 100,
                    Title ?? "标准横断面图",
                    planStripLength: PlanStripLength > 0 ? PlanStripLength : 6.5,
                    centerlinePosition: CenterlinePosition,
                    profileElevationOffset: ProfileElevationOffset,
                    isEmptyAssembly: IsEmptyAssembly,
                    stationStart: StationStart,
                    stationEnd: StationEnd,
                    additionalStationRanges: extra,
                    medianLeftSubWidth: MedianLeftSubWidth,
                    medianLeftCrossSlopePct: MedianLeftCrossSlopePct,
                    medianRightCrossSlopePct: MedianRightCrossSlopePct,
                    medianLeftOuterElevationDiff: MedianLeftOuterElevationDiff,
                    medianLeftInnerElevationDiff: MedianLeftInnerElevationDiff,
                    medianRightInnerElevationDiff: MedianRightInnerElevationDiff,
                    medianRightOuterElevationDiff: MedianRightOuterElevationDiff,
                    elevationDiffLocked: ElevationDiffLocked);
            }

            public sealed class StationRangeItemDto
            {
                public double StartM { get; set; }
                public double EndM { get; set; }
            }
        }

        internal sealed class CrossSectionBandDto
        {
            public string Name { get; set; }
            public int Kind { get; set; }
            public double Width { get; set; }
            public double CrossSlopePct { get; set; }
            public int LaneCount { get; set; }
            public double ElevationDiff { get; set; }
            public double InnerElevationDiff { get; set; }
            public int SlopeType { get; set; }
            public int CrownProfile { get; set; }
            public int SurfaceLayer { get; set; }
            public KerbSpecDto OuterKerb { get; set; }
            public KerbSpecDto InnerKerb { get; set; }
            public StructureLayerScheme StructureScheme { get; set; }

            public static CrossSectionBandDto From(CrossSectionBand b)
                => new CrossSectionBandDto
                {
                    Name = b.Name,
                    Kind = (int)b.Kind,
                    Width = b.Width,
                    CrossSlopePct = b.CrossSlopePct,
                    LaneCount = b.LaneCount,
                    ElevationDiff = b.ElevationDiff,
                    InnerElevationDiff = b.InnerElevationDiff,
                    SlopeType = (int)b.SlopeType,
                    CrownProfile = (int)b.CrownProfile,
                    SurfaceLayer = (int)b.SurfaceLayer,
                    OuterKerb = KerbSpecDto.From(b.OuterKerb),
                    InnerKerb = KerbSpecDto.From(b.InnerKerb),
                    StructureScheme = b.StructureScheme,
                };

            public CrossSectionBand ToBand(BandSide side)
            {
                var outerKerb = OuterKerb?.ToKerbSpec() ?? KerbSpec.None;
                var innerKerb = InnerKerb?.ToKerbSpec() ?? KerbSpec.None;
                return new CrossSectionBand(
                    Name ?? "Band",
                    (HyCADTool.Refactored.Domain.Models.Road.TemplateComponentKind)Kind,
                    Width > 0 ? Width : 1.0,
                    CrossSlopePct,
                    side,
                    outerKerb,
                    innerKerb,
                    Enum.IsDefined(typeof(RoadSlopeType), SlopeType) ? (RoadSlopeType)SlopeType : RoadSlopeType.Single,
                    Enum.IsDefined(typeof(RoadCrownProfile), CrownProfile) ? (RoadCrownProfile)CrownProfile : RoadCrownProfile.Linear,
                    Enum.IsDefined(typeof(RoadSurfaceLayer), SurfaceLayer) ? (RoadSurfaceLayer)SurfaceLayer : RoadSurfaceLayer.None,
                    LaneCount < 0 ? 0 : LaneCount,
                    StructureScheme,
                    ElevationDiff,
                    InnerElevationDiff);
            }
        }

        internal sealed class KerbSpecDto
        {
            public int Type { get; set; }
            public string Model { get; set; }
            public double Height { get; set; }
            public double Width { get; set; }

            public static KerbSpecDto From(KerbSpec kerb)
                => new KerbSpecDto
                {
                    Type = (int)kerb.Type,
                    Model = kerb.Model,
                    Height = kerb.Height,
                    Width = kerb.Width,
                };

            public KerbSpec ToKerbSpec()
            {
                var kerbType = Enum.IsDefined(typeof(RoadKerbType), Type)
                    ? (RoadKerbType)Type
                    : RoadKerbType.None;
                var h = double.IsNaN(Height) || double.IsInfinity(Height) || Height < 0 ? 0 : Height;
                var w = double.IsNaN(Width) || double.IsInfinity(Width) || Width < 0 ? 0 : Width;
                return new KerbSpec(kerbType, Model ?? string.Empty, h, w);
            }
        }
    }
}
