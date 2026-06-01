using System;

namespace HyCAD.Geometry
{
    /// <summary>
    /// 桩号采样点：平面桩号 + 三维点 + XY 单位切线。
    ///
    /// 用于 <see cref="Polyline3D.SamplePlanarStations(double, double, bool)"/> 返回。
    /// 典型消费方是道路桩号标注、纵断面 EG 投影、走廊分段采样等。
    ///
    /// <see cref="Station"/> 语义与 AutoCAD/Civil 3D / 鸿业纬地一致：沿 XY 平面累计弧长，起点为 0，单位 m。
    /// 工程上通常叠加 Alignment.StartStation 换算显示桩号（"K0+020.000"）。
    /// </summary>
    public readonly struct StationSample : IEquatable<StationSample>
    {
        public double Station { get; }
        public Point3D Point { get; }
        public Vector2D Tangent { get; }

        public StationSample(double station, Point3D point, Vector2D tangent)
        {
            Station = station;
            Point = point;
            Tangent = tangent;
        }

        /// <summary>
        /// 将桩号按 AutoCAD/Civil 3D 惯例格式化：K{km}+{m:000.000}，如 K0+020.000 / K1+234.567。
        /// 负桩号（沿延长线往回）前缀 "-"，如 -K0+010.000，工程现场极少用但保留防御。
        ///
        /// 先按 mm 精度规整再拆 km，避免浮点进位跨 km 边界时产生 <c>K0+1000.000</c> 这种脏字符串。
        /// </summary>
        public string FormatStation()
        {
            double abs = System.Math.Round(System.Math.Abs(Station), 3, MidpointRounding.AwayFromZero);
            int km = (int)(abs / 1000);
            double remainder = abs - km * 1000;
            string sign = Station < 0 ? "-" : string.Empty;
            return $"{sign}K{km}+{remainder:000.000}";
        }

        public bool Equals(StationSample other)
        {
            return System.Math.Abs(Station - other.Station) < 1e-9
                && Point.Equals(other.Point)
                && Tangent.Equals(other.Tangent);
        }

        public override bool Equals(object obj) => obj is StationSample s && Equals(s);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Station.GetHashCode();
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Tangent.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"StationSample[{FormatStation()} @ {Point}, T={Tangent}]";
    }
}
