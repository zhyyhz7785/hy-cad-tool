using System;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 单套「平面图 / 坡面图」出图共用的填充配置：可同时启用 AutoCAD 填充与图块中心插入。
    /// </summary>
    public sealed class LayerFillSettings
    {
        public bool PatternEnabled { get; set; }

        public string PatternName { get; set; } = string.Empty;

        public double PatternScale { get; set; } = 1.0;

        public double PatternAngle { get; set; }

        /// <summary>0 = 随层（ByLayer，AutoCAD 256）。</summary>
        public short ColorIndex { get; set; } = 256;

        public bool BlockEnabled { get; set; }

        public string BlockName { get; set; } = string.Empty;

        public double BlockScale { get; set; } = 1.0;

        public double BlockRotation { get; set; }

        public static LayerFillSettings Empty() => new LayerFillSettings();

        public LayerFillSettings Clone()
        {
            return new LayerFillSettings
            {
                PatternEnabled = PatternEnabled,
                PatternName = PatternName ?? string.Empty,
                PatternScale = PatternScale,
                PatternAngle = PatternAngle,
                ColorIndex = ColorIndex,
                BlockEnabled = BlockEnabled,
                BlockName = BlockName ?? string.Empty,
                BlockScale = BlockScale,
                BlockRotation = BlockRotation,
            };
        }

        /// <summary>将旧版顶层的 <see cref="Domain.Models.Road.StructureLayer.PatternName"/> 并入坡面图填充（若新字段空）。</summary>
        public static void MigrateFromLegacyPatternName(LayerFillSettings target, string legacyPatternName)
        {
            if (target == null) return;
            var p = (legacyPatternName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(p)) return;
            if (string.IsNullOrWhiteSpace(target.PatternName))
            {
                target.PatternEnabled = true;
                target.PatternName = p;
            }
        }
    }
}
