using HYFEA.Core.Analysis;
using HYFEA.Core.Dofs;
using HYFEA.Core.Model;

namespace HYFEA.Core.Validation;

public sealed record Tolerance(double DisplacementRelative, double ReactionAbsolute, double AxialRelative);

public sealed record ExpectedResults(
    IReadOnlyList<(NodeId node, DofType dof, double displacement)> ExpectedDisplacements,
    IReadOnlyList<(NodeId node, DofType dof, double reaction)> ExpectedReactions,
    IReadOnlyList<(ElementId element, double axial)>? ExpectedAxials = null);

public sealed record GoldenCase(string Name, FemProblem Problem, ExpectedResults Expected, Tolerance Tolerance);

public sealed record AssertionLine(string Label, double Expected, double Actual, double AbsError, double RelError, bool Pass);

public sealed record ValidationReport(bool AllPassed, IReadOnlyList<AssertionLine> Lines);

public static class GoldenCaseRunner
{
    public static ValidationReport Run(GoldenCase gc)
    {
        var analysis = new LinearStaticAnalysis();
        var res = analysis.Run(gc.Problem);
        if (!res.Success || res.Displacements is null || res.Reactions is null)
            throw new InvalidOperationException($"Analysis failed: {res.Error?.Message ?? "unknown"}");

        var lines = new List<AssertionLine>();
        bool all = true;

        foreach (var (node, dof, ev) in gc.Expected.ExpectedDisplacements)
        {
            double av = res.Displacements.Get(node, dof);
            all &= Add(lines, $"u {node.Value}.{dof}", ev, av, gc.Tolerance.DisplacementRelative, relative: true);
        }

        foreach (var (node, dof, ev) in gc.Expected.ExpectedReactions)
        {
            double av = res.Reactions.Get(node, dof);
            all &= Add(lines, $"R {node.Value}.{dof}", ev, av, gc.Tolerance.ReactionAbsolute, relative: false);
        }

        if (gc.Expected.ExpectedAxials is not null && res.AxialForces is not null)
        {
            foreach (var (eid, ev) in gc.Expected.ExpectedAxials)
            {
                double av = res.AxialForces.Get(eid);
                all &= Add(lines, $"N {eid.Value}", ev, av, gc.Tolerance.AxialRelative, relative: true);
            }
        }

        return new ValidationReport(all, lines);
    }

    private static bool Add(
        List<AssertionLine> lines,
        string label,
        double expected,
        double actual,
        double tol,
        bool relative)
    {
        double abs = Math.Abs(actual - expected);
        double rel = relative && Math.Abs(expected) > 1e-30
            ? abs / Math.Abs(expected)
            : abs;
        bool pass = rel <= tol;
        lines.Add(new AssertionLine(label, expected, actual, abs, rel, pass));
        return pass;
    }
}
