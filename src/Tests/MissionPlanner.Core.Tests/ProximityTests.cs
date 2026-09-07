using MissionPlanner.Core.Setup.Advanced.Proximity;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

public sealed class ProximityTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.Parse("2026-09-07T00:00:00Z");

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 90)]
    [InlineData(7, 315)]
    [InlineData(12, 180)]
    public void DistanceUnitsAndHorizontalRotations(byte orientation, double bearing)
    {
        var aggregate = New();
        aggregate.Observe(Distance() with { Orientation = orientation });
        var point = Assert.Single(aggregate.Snapshot().Points);
        Assert.Equal(bearing, point.BearingDegrees);
        Assert.Equal(1.25, point.DistanceMeters);
        Assert.Equal(.1, point.MinimumMeters);
        Assert.Equal(10, point.MaximumMeters);
        Assert.Equal(ProximitySampleState.Valid, point.State);
        Assert.Null(point.Covariance);
        Assert.Null(point.Quality);
    }

    [Fact]
    public void InvalidUnknownVerticalAndCustomOrientationAreDistinct()
    {
        var aggregate = New();
        aggregate.Observe(Distance() with { CurrentDistance = 0 });
        Assert.Equal(ProximitySampleState.Unknown, Assert.Single(aggregate.Snapshot().Points).State);
        aggregate.Observe(Distance() with { CurrentDistance = 5 });
        Assert.Equal(ProximitySampleState.TooClose, Assert.Single(aggregate.Snapshot().Points).State);
        aggregate.Observe(Distance() with { CurrentDistance = 1001 });
        Assert.Equal(ProximitySampleState.OutOfRange, Assert.Single(aggregate.Snapshot().Points).State);
        aggregate.Observe(Distance() with { Orientation = 24 });
        Assert.Equal(ProximitySampleState.Unsupported, Assert.Single(aggregate.Snapshot().Points).State);
        Assert.Null(aggregate.Snapshot().Nearest);
        aggregate.Observe(Distance() with { Orientation = 100, Quaternion = [.70710678f, 0, 0, .70710678f] });
        Assert.Equal(90, Assert.Single(aggregate.Snapshot().Points).BearingDegrees!.Value, 5);
        aggregate.Observe(Distance() with { MaxDistance = 0 });
        Assert.Empty(aggregate.Snapshot().Points);
        Assert.Equal(1, aggregate.Snapshot().Malformed);
    }

    [Fact]
    public void ObstacleArraysUseFloatIncrementOffsetAndSentinels()
    {
        var aggregate = New();
        var message = Obstacles();
        message.Distances[0] = 125;
        message.Distances[1] = 1001;
        message.Distances[2] = 0;
        aggregate.Observe(message);
        var points = aggregate.Snapshot().Points;
        Assert.Equal(72, points.Count);
        Assert.Equal(355, points[0].BearingDegrees);
        Assert.Equal(350, points[1].BearingDegrees);
        Assert.Equal(ProximitySampleState.OutOfRange, points[1].State);
        Assert.Equal(ProximitySampleState.TooClose, points[2].State);
        Assert.Equal(ProximitySampleState.Unknown, points[3].State);
        Assert.Null(points[3].DistanceMeters);
        aggregate.Observe(message with { MinDistance = 0 });
        Assert.Equal(0, aggregate.Snapshot().Nearest!.DistanceMeters);
        aggregate.Observe(message with { IncrementF = float.NaN });
        Assert.Empty(aggregate.Snapshot().Points);
        Assert.Equal(1, aggregate.Snapshot().Malformed);
    }

    [Fact]
    public void GlobalArraysNeedHeadingAndSourcesDoNotOverwriteEachOther()
    {
        var aggregate = New();
        var message = Obstacles() with { Frame = 0, IncrementF = 90, AngleOffset = 10 };
        message.Distances[0] = 200;
        aggregate.Observe(message);
        Assert.Null(aggregate.Snapshot().Nearest);
        aggregate.Observe(message, 100);
        Assert.Equal(270, aggregate.Snapshot().Nearest!.BearingDegrees);
        aggregate.Observe(Distance());
        aggregate.Observe(Distance() with { Id = 2, CurrentDistance = 80 });
        var snapshot = aggregate.Snapshot();
        Assert.Equal(3, snapshot.Points.Select(point => point.Source).Distinct().Count());
        Assert.EndsWith("distance/2", snapshot.Nearest!.Source);
        Assert.Equal(.8, snapshot.Nearest.DistanceMeters);
    }

    [Fact]
    public void FreshnessAndBoundsAreClockDrivenAndLateSamplesCannotReplaceNewOnes()
    {
        var clock = new Clock();
        var options = new ProximityOptions { StaleAfter = TimeSpan.FromSeconds(1) };
        var aggregate = new ProximityAggregator(clock, options);
        aggregate.Observe(Distance());
        aggregate.Observe(Distance() with { ReceivedAt = now.AddSeconds(-1), CurrentDistance = 20 });
        Assert.Equal(1.25, aggregate.Snapshot().Nearest!.DistanceMeters);
        clock.Now = now.AddSeconds(1.01);
        Assert.Null(aggregate.Snapshot().Nearest);
        Assert.Equal(ProximitySampleState.Stale, Assert.Single(aggregate.Snapshot().Points).State);
        for (var id = 0; id < 256; id++)
        {
            aggregate.Observe(Distance() with { Id = (byte)id });
        }
        Assert.Equal(64, aggregate.Snapshot().Points.Count);
        Assert.True(aggregate.Snapshot().Dropped > 0);
        aggregate.Clear();
        Assert.Equal(ProximitySnapshot.Empty, aggregate.Snapshot() with { Points = ProximitySnapshot.Empty.Points });
    }

    private static ProximityAggregator New() => new(new Clock(), new());
    private static DistanceSensorMessage Distance() => new(1, 1, new TransportEndPoint("test"),
        0, 10, 1000, 125, 0, 1, 0, 255, 0, 0, [1, 0, 0, 0], 0, now);
    private static ObstacleDistanceMessage Obstacles() => new(1, 1, new TransportEndPoint("test"), 0, 0,
        Enumerable.Repeat(ushort.MaxValue, 72).ToArray(), 10, 10, 1000, -5, -5, 12, now);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
