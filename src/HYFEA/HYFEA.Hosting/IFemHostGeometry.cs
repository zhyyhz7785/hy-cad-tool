using System.Collections.Generic;
using HyCAD.Geometry;

namespace HYFEA.Hosting;

/// <summary>宿主提供梁轴线节点（坐标单位与求解 <c>UnitSystem</c> 一致）。</summary>
public interface IFemHostGeometry
{
    IReadOnlyList<Point2D> GetAxisNodes();
}
