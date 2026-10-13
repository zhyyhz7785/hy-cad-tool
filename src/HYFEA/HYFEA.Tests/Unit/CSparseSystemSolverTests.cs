using FluentAssertions;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Model;
using HYFEA.Core.Solvers;
using HYFEA.Hosting;
using Xunit;

namespace HYFEA.Tests.Unit;

public sealed class CSparseSystemSolverTests
{
    [Fact]
    public void Solves_2x2_spd()
    {
        var triplets = new[]
        {
            new SparseTriplet(0, 0, 4),
            new SparseTriplet(0, 1, 1),
            new SparseTriplet(1, 0, 1),
            new SparseTriplet(1, 1, 3),
        };
        var F = new DenseVector(2);
        F[0] = 1;
        F[1] = 2;

        var r = new CSparseSystemSolver().SolveSparse(triplets, 2, F);

        r.Success.Should().BeTrue();
        r.Solution.Should().NotBeNull();
        r.Solution![0].Should().BeApproximately(1.0 / 11.0, 1e-12);
        r.Solution[1].Should().BeApproximately(7.0 / 11.0, 1e-12);
        r.Diagnostics!.ResidualNorm.Should().BeLessThan(1e-12);
    }

    [Fact]
    public void Free_free_beam_mechanism_fails()
    {
        var geom = InMemoryAxisGeometry.Horizontal(10000, 4);
        var request = new BeamSolveRequest
        {
            EndA = BeamSupportKind.Free,
            EndB = BeamSupportKind.Free,
            Q = 10,
        };

        var output = new BeamSolveSession().Compute(geom, request);

        output.Result.Success.Should().BeFalse();
        output.Result.Error.Should().NotBeNull();
        output.Result.Error!.Kind.Should().Be(FemErrorKind.SingularMatrix);
    }

    [Fact]
    public void Singular_matrix_returns_failure_without_throw()
    {
        var triplets = new[]
        {
            new SparseTriplet(0, 0, 1),
            new SparseTriplet(0, 1, 1),
            new SparseTriplet(1, 0, 1),
            new SparseTriplet(1, 1, 1),
        };
        var F = new DenseVector(2);
        F[0] = 1;
        F[1] = 1;

        var r = new CSparseSystemSolver().SolveSparse(triplets, 2, F);

        r.Success.Should().BeFalse();
        r.Error.Should().NotBeNull();
        r.Error!.Kind.Should().Be(FemErrorKind.SingularMatrix);
    }
}
