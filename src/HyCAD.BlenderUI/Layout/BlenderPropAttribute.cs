using System;

namespace HyCAD.BlenderUI.Layout
{
    /// <summary>属性元数据（auto-UI / 数值子类型）。</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BlenderPropAttribute : Attribute
    {
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public int Precision { get; set; } = 3;
        public BlenderPropSubtype Subtype { get; set; } = BlenderPropSubtype.None;
    }

    public enum BlenderPropSubtype
    {
        None,
        Factor,
        Angle,
        Percentage,
        Distance,
        Color,
    }
}
