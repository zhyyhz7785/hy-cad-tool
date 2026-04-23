using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            if (!File.Exists(path)) return null;

            var descr = TryLoadFile(path);
            return descr?.Create();
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
            string path = Path.Combine(_userDir, SanitizeFileName(presetKey) + ".json");

            var dto = CrossSectionLayoutDto.FromLayout(layout);
            var file = new PresetFile
            {
                Key = presetKey,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? presetKey : displayName,
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
            string path = Path.Combine(_userDir, SanitizeFileName(presetKey) + ".json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
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
            public double CenterlinePosition { get; set; } = double.NaN;
            public double ProfileElevationOffset { get; set; }
            public bool IsEmptyAssembly { get; set; }
            public double StationStart { get; set; }
            public double StationEnd { get; set; }
            public List<StationRangeItemDto> AdditionalStationRanges { get; set; } = new List<StationRangeItemDto>();

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
                    CenterlinePosition = l.CenterlinePosition,
                    ProfileElevationOffset = l.ProfileElevationOffset,
                    IsEmptyAssembly = l.IsEmptyAssembly,
                    StationStart = l.StationStart,
                    StationEnd = l.StationEnd,
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
                    CenterlinePosition,
                    ProfileElevationOffset,
                    IsEmptyAssembly,
                    StationStart,
                    StationEnd,
                    additionalStationRanges: extra);
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

            public static CrossSectionBandDto From(CrossSectionBand b)
                => new CrossSectionBandDto
                {
                    Name = b.Name,
                    Kind = (int)b.Kind,
                    Width = b.Width,
                    CrossSlopePct = b.CrossSlopePct,
                    LaneCount = b.LaneCount,
                };

            public CrossSectionBand ToBand(BandSide side)
            {
                return new CrossSectionBand(
                    Name ?? "Band",
                    (HyCADTool.Refactored.Domain.Models.Road.TemplateComponentKind)Kind,
                    Width > 0 ? Width : 1.0,
                    CrossSlopePct,
                    side);
            }
        }
    }
}
