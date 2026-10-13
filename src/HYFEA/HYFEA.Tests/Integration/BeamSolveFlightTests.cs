using System.Threading;
using FluentAssertions;
using HYFEA.Hosting;
using Xunit;

namespace HYFEA.Tests.Integration;

public sealed class BeamSolveFlightTests
{
    [Fact]
    public void Begin_cancels_previous_ticket_and_only_latest_is_current()
    {
        var flight = new BeamSolveFlight();
        var first = flight.Begin();
        var second = flight.Begin();

        flight.IsCurrent(first.Serial).Should().BeFalse();
        flight.IsCurrent(second.Serial).Should().BeTrue();
        first.Token.IsCancellationRequested.Should().BeTrue();
        second.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void ShouldPresent_rejects_stale_serial_and_wrong_document()
    {
        var flight = new BeamSolveFlight();
        var first = flight.Begin();
        var docA = new object();
        var docB = new object();

        flight.ShouldPresent(first.Serial, docA, docA).Should().BeTrue();

        var second = flight.Begin();
        flight.ShouldPresent(first.Serial, docA, docA).Should().BeFalse();
        flight.ShouldPresent(second.Serial, docA, docB).Should().BeFalse();
        flight.ShouldPresent(second.Serial, docA, docA).Should().BeTrue();
        flight.ShouldPresent(second.Serial, null, docA).Should().BeFalse();
    }

    [Fact]
    public void Cancelled_ticket_still_blocks_compute()
    {
        var flight = new BeamSolveFlight();
        var first = flight.Begin();
        flight.Begin();

        var geom = InMemoryAxisGeometry.Horizontal(10000.0, segments: 2);
        var request = new BeamSolveRequest
        {
            YoungsModulus = 2.06e5,
            Area = 8e4,
            InertiaZ = 1.067e9,
            Q = 10,
            EndA = BeamSupportKind.Fixed,
            EndB = BeamSupportKind.Free,
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new BeamSolveSession().Compute(geom, request, first.Token));
    }
}
