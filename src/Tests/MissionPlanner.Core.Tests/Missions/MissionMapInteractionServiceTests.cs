using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests.MissionPlanning;

/// <summary>Verifies renderer-independent mission-map interaction behavior.</summary>
public sealed class MissionMapInteractionServiceTests
{
    /// <summary>Starting a mode replaces the previous interaction rather than combining modes.</summary>
    [Fact]
    public void Enter_ReplacesCurrentModeAndTemporaryState()
    {
        var service = new MissionMapInteractionService();
        service.Enter(MissionMapInteractionMode.DrawPolygon, "Draw");
        service.AcceptClick(new GeoPosition(55, 12));

        service.Enter(MissionMapInteractionMode.MeasureDistance, "Measure");

        Assert.Equal(MissionMapInteractionMode.MeasureDistance, service.State.Mode);
        Assert.Empty(service.State.Positions);
        Assert.Empty(service.Overlay.DrawnPolygon);
    }

    /// <summary>Clicks are consumed only while an interaction is active.</summary>
    [Fact]
    public void AcceptClick_RoutesToActiveInteraction()
    {
        var service = new MissionMapInteractionService();
        Assert.False(service.AcceptClick(new GeoPosition(55, 12)));
        service.Enter(MissionMapInteractionMode.DrawPolygon, "Draw");

        Assert.True(service.AcceptClick(new GeoPosition(55, 12)));
        Assert.Single(service.State.Positions);
        Assert.Single(service.Overlay.DrawnPolygon);
    }

    /// <summary>Cancelling removes temporary geometry and restores the inactive state.</summary>
    [Fact]
    public void Cancel_ClearsTemporaryOverlay()
    {
        var service = new MissionMapInteractionService();
        service.Enter(MissionMapInteractionMode.MeasureDistance, "Measure");
        service.AcceptClick(new GeoPosition(55, 12));

        service.Cancel();

        Assert.Equal(MissionMapInteractionState.None, service.State);
        Assert.Empty(service.Overlay.TemporaryMeasurement);
    }

    /// <summary>Completion ends input while retaining completed planning geometry.</summary>
    [Fact]
    public void Complete_RetainsCompletedOverlay()
    {
        var service = new MissionMapInteractionService();
        service.Enter(MissionMapInteractionMode.DrawPolygon, "Draw");
        service.AcceptClick(new GeoPosition(55, 12));
        service.AcceptClick(new GeoPosition(55.1, 12.1));

        service.Complete();

        Assert.Equal(MissionMapInteractionMode.None, service.State.Mode);
        Assert.Equal(2, service.Overlay.DrawnPolygon.Count);
    }

    /// <summary>Disabled command availability retains its explanatory reason.</summary>
    [Fact]
    public void CommandAvailability_PropagatesReason()
    {
        var availability = MissionMapCommandAvailability.Disabled("Connect a vehicle first.");

        Assert.False(availability.IsEnabled);
        Assert.Equal("Connect a vehicle first.", availability.Reason);
    }
}
