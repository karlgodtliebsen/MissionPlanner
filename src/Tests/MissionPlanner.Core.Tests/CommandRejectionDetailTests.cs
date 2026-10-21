using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies that command rejections carry the vehicle's own explanation.</summary>
public sealed class CommandRejectionDetailTests
{
    private static readonly VehicleId Vehicle = new(1, 1);
    private static readonly DateTimeOffset SentAt = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static VehicleStatusText Message(MavSeverity severity, string text, int offsetSeconds) =>
        new(Vehicle, 1, 1, severity, text, SentAt.AddSeconds(offsetSeconds));

    /// <summary>Only recent warning-or-worse messages explain the rejection.</summary>
    [Fact]
    public async Task CollectsRecentWarningsOnly()
    {
        var store = Substitute.For<IVehicleMessageStore>();
        store.GetMessages(Vehicle).Returns(
        [
            Message(MavSeverity.Error, "Stale failure before the command", -10),
            Message(MavSeverity.Info, "Chatter", 0),
            Message(MavSeverity.Error, "RC1_MIN=1426, expected <=1300", 1),
            Message(MavSeverity.Error, "RC1_MIN=1426, expected <=1300", 1),
            Message(MavSeverity.Warning, "RC4_MAX=1598, expected >=1700", 1)
        ]);

        var detail = await CommandRejectionDetail.CollectAsync(store, Vehicle, SentAt, TestContext.Current.CancellationToken);

        Assert.Equal($"RC1_MIN=1426, expected <=1300{Environment.NewLine}RC4_MAX=1598, expected >=1700", detail);
        Assert.Contains("Vehicle reported:", CommandRejectionDetail.Append("rejected", detail));
    }

    /// <summary>A silent vehicle leaves the generic message untouched.</summary>
    [Fact]
    public async Task ReturnsNothingWhenTheVehicleSaidNothing()
    {
        Assert.Null(await CommandRejectionDetail.CollectAsync(null, Vehicle, SentAt, TestContext.Current.CancellationToken));
        var store = Substitute.For<IVehicleMessageStore>();
        store.GetMessages(Vehicle).Returns([]);
        var detail = await CommandRejectionDetail.CollectAsync(store, Vehicle, SentAt, TestContext.Current.CancellationToken);
        Assert.Null(detail);
        Assert.Equal("rejected", CommandRejectionDetail.Append("rejected", detail));
    }
}
