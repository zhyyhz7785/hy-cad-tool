using System;
using System.Collections.Generic;
using HyCAD.Geometry;
using HYFEA.Core.Boundary;
using HYFEA.Core.Dofs;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Hosting;

/// <summary>由轴线折线建立 Euler 梁模型 + 均布荷载（每段单元同一 q）。</summary>
public static class BeamProblemFactory
{
    public static FemProblem Build(
        IReadOnlyList<Point2D> axisNodes,
        double youngModulus,
        double area,
        double inertiaZ,
        double qForcePerLength,
        BeamLoadDirection loadDirection,
        BeamSupportKind endA,
        BeamSupportKind endB,
        UnitSystem units)
    {
        if (axisNodes == null || axisNodes.Count < 2)
            throw new ArgumentException("至少需要 2 个节点。", nameof(axisNodes));

        var matId = new MaterialId(1);
        var secId = new SectionId(1);

        var b = new FemProblemBuilder()
            .WithUnits(units)
            .AddMaterial(new LinearElasticMaterial(matId, youngModulus, 0.3))
            .AddSection(new BeamSection(secId, area, inertiaZ));

        for (int i = 0; i < axisNodes.Count; i++)
            b.AddNode(new NodeId(i), axisNodes[i].X, axisNodes[i].Y);

        for (int i = 0; i < axisNodes.Count - 1; i++)
        {
            var eid = new ElementId(i + 1);
            b.AddEulerBeam2D(eid, new NodeId(i), new NodeId(i + 1), matId, secId);
            b.AddElementLoad(new UniformBeamLoad(eid, loadDirection, qForcePerLength));
        }

        ApplySupport(b, new NodeId(0), endA);
        ApplySupport(b, new NodeId(axisNodes.Count - 1), endB);

        return b.Build();
    }

    public static FemProblem Build(IReadOnlyList<Point2D> axisNodes, BeamSolveRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        return Build(
            axisNodes,
            request.YoungsModulus,
            request.Area,
            request.InertiaZ,
            request.Q,
            request.LoadDirection,
            request.EndA,
            request.EndB,
            request.Units);
    }

    private static void ApplySupport(FemProblemBuilder b, NodeId n, BeamSupportKind kind)
    {
        switch (kind)
        {
            case BeamSupportKind.Free:
                return;
            case BeamSupportKind.Pin:
                b.AddSupport(new FixedSupport(n, DofType.UX));
                b.AddSupport(new FixedSupport(n, DofType.UY));
                return;
            case BeamSupportKind.Fixed:
                b.AddSupport(new FixedSupport(n, DofType.UX));
                b.AddSupport(new FixedSupport(n, DofType.UY));
                b.AddSupport(new FixedSupport(n, DofType.RZ));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
