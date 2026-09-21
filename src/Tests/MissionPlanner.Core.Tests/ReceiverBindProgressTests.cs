using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies truthful, bounded RC recovery monitoring without repeated diagnostic transitions.</summary>
public sealed class ReceiverBindProgressTests
{
    /// <summary>Fresh traffic alone cannot establish that a receiver was paired.</summary>
    [Fact]
    public void ContinuousInputTimesOutWithoutFailingTheAcceptedCommand()
    {
        var now = DateTimeOffset.UtcNow;
        var progress = Accepted(now);
        Assert.True(progress.Observe(State(now), now));
        Assert.False(progress.Observe(State(now.AddSeconds(1)), now.AddSeconds(1)));
        Assert.True(progress.Observe(State(now.AddSeconds(21)), now.AddSeconds(21)));
        Assert.Equal(ReceiverBindState.WaitingForLink, progress.State);
        Assert.True(progress.IsComplete);
        Assert.Contains("accepted", progress.Message);
        Assert.Contains("Lua script", progress.Message);
        Assert.False(progress.Observe(State(now.AddSeconds(22)), now.AddSeconds(22)));
    }

    /// <summary>Input loss followed by fresh valid input produces one recovery transition.</summary>
    [Fact]
    public void LossThenFreshInputRestoresLink()
    {
        var now = DateTimeOffset.UtcNow;
        var progress = Accepted(now);
        progress.Observe(State(now) with { Radio = VehicleRadioState.Empty }, now);
        Assert.True(progress.Observe(State(now.AddSeconds(1)), now.AddSeconds(1)));
        Assert.Equal(ReceiverBindState.LinkRestored, progress.State);
        Assert.True(progress.IsComplete);
        Assert.False(progress.Observe(State(now.AddSeconds(2)), now.AddSeconds(2)));
    }

    /// <summary>Rejected and unsupported commands never enter a recovery wait.</summary>
    [Theory]
    [InlineData(VehicleCommandResult.Unsupported, ReceiverBindState.Unsupported)]
    [InlineData(VehicleCommandResult.Failed, ReceiverBindState.Failed)]
    public void UnsuccessfulAckIsTerminal(VehicleCommandResult result, ReceiverBindState expected)
    {
        var progress = new ReceiverBindProgress();
        progress.Request();
        Assert.Equal(ReceiverBindState.Requested, progress.State);
        progress.Apply(new(new(1, 1), result, DateTimeOffset.UtcNow));
        Assert.Equal(expected, progress.State);
        Assert.True(progress.IsComplete);
        Assert.False(progress.Observe(null, DateTimeOffset.UtcNow));
    }

    /// <summary>Disconnect stops observation without rewriting command acceptance.</summary>
    [Fact]
    public void DisconnectStopsWait()
    {
        var now = DateTimeOffset.UtcNow;
        var progress = Accepted(now);
        progress.Observe(null, now);
        Assert.True(progress.IsComplete);
        Assert.Equal(ReceiverBindState.WaitingForLink, progress.State);
    }

    private static ReceiverBindProgress Accepted(DateTimeOffset now)
    {
        var progress = new ReceiverBindProgress();
        progress.Request();
        progress.Apply(new(new(1, 1), VehicleCommandResult.Accepted, now));
        Assert.Equal(ReceiverBindState.CommandAccepted, progress.State);
        return progress;
    }

    private static VehicleState State(DateTimeOffset now)
    {
        return new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null)
        {
            Radio = VehicleRadioState.Empty with { ObservedAt = now, ChannelsRaw = new ushort[] { 1500, 1500, 1000, 1500 }, RssiPercent = 75 }
        };
    }
}
