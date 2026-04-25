using System;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 横断面适用桩号区间（米）。可多段，一段对应一套断面实例的绑定规划。
    /// </summary>
    public readonly struct StationRangeSpan : IEquatable<StationRangeSpan>
    {
        public double StartM { get; }
        public double EndM { get; }

        public StationRangeSpan(double startM, double endM)
        {
            StartM = startM;
            EndM = endM;
        }

        public bool Equals(StationRangeSpan other) => StartM.Equals(other.StartM) && EndM.Equals(other.EndM);

        public override bool Equals(object obj) => obj is StationRangeSpan other && Equals(other);

        public override int GetHashCode() => unchecked((StartM.GetHashCode() * 397) ^ EndM.GetHashCode());
    }
}
