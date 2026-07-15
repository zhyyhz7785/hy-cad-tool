using System.Collections.Generic;
using HyCAD.Geometry;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Hosting;

namespace HyCADTool.Features.Fem.Integration
{
    /// <summary>兼容旧名；实现见 <see cref="BeamSupportKind"/>。</summary>
    public enum BeamSupportUi
    {
        Free = BeamSupportKind.Free,
        Pin = BeamSupportKind.Pin,
        Fixed = BeamSupportKind.Fixed,
    }

    /// <summary>薄包装：委托 <see cref="BeamProblemFactory"/>。</summary>
    public static class HyfeaBeamProblemBuilder
    {
        public static FemProblem Build(
            IReadOnlyList<Point2D> axisNodes,
            double youngModulusMpa,
            double areaMm2,
            double inertiaZMm4,
            double qForcePerLength,
            BeamLoadDirection loadDirection,
            BeamSupportUi endA,
            BeamSupportUi endB,
            UnitSystem units)
        {
            return BeamProblemFactory.Build(
                axisNodes,
                youngModulusMpa,
                areaMm2,
                inertiaZMm4,
                qForcePerLength,
                loadDirection,
                (BeamSupportKind)endA,
                (BeamSupportKind)endB,
                units);
        }
    }
}
