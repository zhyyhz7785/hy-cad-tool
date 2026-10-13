using FluentAssertions;
using HYFEA.Core.Diagnostics;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Solvers;
using Xunit;

namespace HYFEA.Tests.Unit;

public sealed class DenseLinearSolverTests
{
    [Fact]
    public void Solves_2x2_spd()
    {
        var K = new DenseMatrix(2, 2);
        K[0, 0] = 4; K[0, 1] = 1;
        K[1, 0] = 1; K[1, 1] = 3;
        var F = new DenseVector(2);
        F[0] = 1;
        F[1] = 2;
        var solver = new DenseLinearSolver();
        var r = solver.Solve(K, F);
        r.Success.Should().BeTrue();
        r.Solution.Should().NotBeNull();
        // [4 1; 1 3][x;y]=[1;2] -> x=1/11, y=7/11
        r.Solution![0].Should().BeApproximately(1.0 / 11.0, 1e-12);
        r.Solution[1].Should().BeApproximately(7.0 / 11.0, 1e-12);
        r.Diagnostics!.ResidualNorm.Should().BeLessThan(1e-12);
    }

    [Fact]
    public void Singular_matrix_returns_failure_without_throw()
    {
        var K = new DenseMatrix(2, 2);
        K[0, 0] = 1; K[0, 1] = 1;
        K[1, 0] = 1; K[1, 1] = 1;
        var F = new DenseVector(2);
        F[0] = 1;
        F[1] = 1;
        var solver = new DenseLinearSolver();
        var r = solver.Solve(K, F);
        r.Success.Should().BeFalse();
        r.Error.Should().NotBeNull();
        r.Error!.Kind.Should().BeOneOf(FemErrorKind.NegativePivot, FemErrorKind.SingularMatrix);
    }

    [Fact]
    public void Well_conditioned_tiny_matrix_is_not_rejected()
    {
        const double s = 1e-20;
        var K = new DenseMatrix(2, 2);
        K[0, 0] = 4 * s; K[0, 1] = 1 * s;
        K[1, 0] = 1 * s; K[1, 1] = 3 * s;
        var F = new DenseVector(2);
        F[0] = 1 * s;
        F[1] = 2 * s;

        var r = new DenseLinearSolver().Solve(K, F);

        r.Success.Should().BeTrue();
        r.Solution![0].Should().BeApproximately(1.0 / 11.0, 1e-9);
        r.Solution[1].Should().BeApproximately(7.0 / 11.0, 1e-9);
    }

    [Fact]
    public void Near_mechanism_relative_to_matrix_scale_fails()
    {
        var K = new DenseMatrix(2, 2);
        K[0, 0] = 1; K[0, 1] = 1;
        K[1, 0] = 1; K[1, 1] = 1 + 1e-15;
        var F = new DenseVector(2);
        F[0] = 1;
        F[1] = 1;

        var r = new DenseLinearSolver().Solve(K, F);

        r.Success.Should().BeFalse();
        r.Error!.Kind.Should().Be(FemErrorKind.SingularMatrix);
    }
}
