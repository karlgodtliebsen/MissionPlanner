using FluentAssertions;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Maps.Terrain;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies map click routing, overlay composition and pointer presentation.</summary>
public sealed class MissionMapInteractionCoordinatorTests
{
    private static readonly VehicleId Vehicle = new(1, 1);
    private readonly MissionMapInteractionService interactionService = new();
    private readonly IPlanningPolygonService polygons = Substitute.For<IPlanningPolygonService>();
    private readonly IActiveVehicleContext activeVehicle = Substitute.For<IActiveVehicleContext>();
    private readonly IFenceConfigurationService fenceService = Substitute.For<IFenceConfigurationService>();
    private readonly IRallyConfigurationService rallyService = Substitute.For<IRallyConfigurationService>();
    private readonly IPoiService poiService = Substitute.For<IPoiService>();
    private readonly ITrackerHomeService trackerHome = Substitute.For<ITrackerHomeService>();
    private readonly MissionMapInteractionCoordinator coordinator;

    public MissionMapInteractionCoordinatorTests()
    {
        activeVehicle.VehicleId.Returns(Vehicle);
        fenceService.GetSnapshot(Vehicle).Returns(new FenceConfigurationSnapshot(Vehicle, FencePlan.Empty, null, null, 0, null, false));
        rallyService.GetSnapshot(Vehicle).Returns(new RallyPlanSnapshot(Vehicle, RallyPlan.Empty, null, 0, null, false, null));
        poiService.Snapshot.Returns(new PoiSnapshot([], null));
        polygons.Snapshot.Returns(PlanningPolygonSnapshot.Empty);
        coordinator = new MissionMapInteractionCoordinator(interactionService, polygons, activeVehicle, fenceService, rallyService, poiService, trackerHome);
    }

    [Fact]
    public void HandleClick_NoActiveInteraction_IsNotConsumed()
    {
        coordinator.HandleClick(new GeoPosition(56, 10)).Should().Be(MapClickOutcome.NotConsumed);
    }

    [Fact]
    public void HandleClick_FenceReturnLocation_UpdatesTheLocalPlanAndCompletes()
    {
        coordinator.Begin(MissionMapInteractionMode.SetFenceReturnLocation, "click");

        var outcome = coordinator.HandleClick(new GeoPosition(56, 10));

        outcome.Consumed.Should().BeTrue();
        outcome.Message.Should().Be("Fence return location updated locally; upload to apply it.");
        fenceService.Received(1).SetLocalPlan(Vehicle, Arg.Is<FencePlan>(plan => plan.ReturnPoint == new GeoPosition(56, 10)));
        coordinator.State.Mode.Should().Be(MissionMapInteractionMode.None);
    }

    [Fact]
    public void HandleClick_RallyPoint_AppendsAPointAtThePendingAltitude()
    {
        coordinator.PendingRallyAltitude = new MissionAltitude(75, MissionAltitudeReference.MeanSeaLevel);
        coordinator.Begin(MissionMapInteractionMode.SetRallyPoint, "click");

        var outcome = coordinator.HandleClick(new GeoPosition(56, 10));

        outcome.Message.Should().Be("Rally point added locally; upload to apply it.");
        rallyService.Received(1).SetLocalPlan(Vehicle, Arg.Is<RallyPlan>(plan =>
            plan.Points.Count == 1
            && plan.Points[0].Position == new GeoPosition(56, 10)
            && plan.Points[0].Altitude == new MissionAltitude(75, MissionAltitudeReference.MeanSeaLevel)));
    }

    [Fact]
    public void HandleClick_FenceReturnLocationWithoutVehicle_ConsumesTheClickWithoutAMessage()
    {
        activeVehicle.VehicleId.Returns((VehicleId?)null);
        coordinator.Begin(MissionMapInteractionMode.SetFenceReturnLocation, "click");

        var outcome = coordinator.HandleClick(new GeoPosition(56, 10));

        outcome.Should().Be(new MapClickOutcome(true, null));
        fenceService.DidNotReceiveWithAnyArgs().SetLocalPlan(default, default!);
    }

    [Fact]
    public void HandleClick_MeasureDistance_ReportsDistanceAfterTheSecondClick()
    {
        coordinator.Begin(MissionMapInteractionMode.MeasureDistance, "measure");

        var first = coordinator.HandleClick(new GeoPosition(0, 0));
        var second = coordinator.HandleClick(new GeoPosition(0, 1));

        first.Should().Be(new MapClickOutcome(true, null));
        second.Message.Should().StartWith("Distance 1111");
        coordinator.State.Mode.Should().Be(MissionMapInteractionMode.None);
    }

    [Fact]
    public void HandleClick_DrawPolygon_KeepsCollectingVertices()
    {
        coordinator.Begin(MissionMapInteractionMode.DrawPolygon, "draw");

        coordinator.HandleClick(new GeoPosition(1, 1));
        coordinator.HandleClick(new GeoPosition(1, 2));

        coordinator.State.Mode.Should().Be(MissionMapInteractionMode.DrawPolygon);
        coordinator.State.Positions.Should().HaveCount(2);
    }

    [Fact]
    public void Changed_IsRaisedWhenTheInteractionChanges_AndCanBeUnsubscribed()
    {
        var raised = 0;
        void Handler(object? sender, EventArgs args) => raised++;

        coordinator.Changed += Handler;
        coordinator.Begin(MissionMapInteractionMode.MeasureDistance, "measure");
        coordinator.Changed -= Handler;
        coordinator.Cancel();

        raised.Should().Be(1);
    }

    [Fact]
    public void ComposeOverlay_CombinesLocalPlanningState()
    {
        var fencePlan = new FencePlan(null, [FenceArea.Polygon(FenceAreaKind.PolygonInclusion, [new GeoPosition(1, 1), new GeoPosition(1, 2), new GeoPosition(2, 2)])]);
        fenceService.GetSnapshot(Vehicle).Returns(new FenceConfigurationSnapshot(Vehicle, fencePlan, null, null, 1, null, true));
        var rallyPlan = new RallyPlan([new RallyPoint(RallyPointId.New(), new GeoPosition(3, 3), new MissionAltitude(10, MissionAltitudeReference.Home))]);
        rallyService.GetSnapshot(Vehicle).Returns(new RallyPlanSnapshot(Vehicle, rallyPlan, null, 1, null, true, null));
        trackerHome.Snapshot.Returns(new TrackerHomeSnapshot(new GeoPosition(4, 4), null, DateTimeOffset.UnixEpoch, "test"));
        IReadOnlyList<GeoPosition> preview = [new GeoPosition(5, 5)];

        var overlay = coordinator.ComposeOverlay([], preview);

        overlay.FencePreview.Should().HaveCount(3);
        overlay.RallyPoints.Should().Equal(new GeoPosition(3, 3));
        overlay.TrackerHome.Should().Be(new GeoPosition(4, 4));
        overlay.SurveyPreview.Should().BeSameAs(preview);
    }

    [Fact]
    public void ComposeOverlay_WithoutAVehicle_HasNoFenceOrRally()
    {
        activeVehicle.VehicleId.Returns((VehicleId?)null);

        var overlay = coordinator.ComposeOverlay([], []);

        overlay.FencePreview.Should().BeEmpty();
        overlay.RallyPoints.Should().BeEmpty();
    }

    [Fact]
    public void FenceOutline_CircleArea_ApproximatesTheCircleWithThirtySixPoints()
    {
        var plan = new FencePlan(null, [FenceArea.Circle(FenceAreaKind.CircleInclusion, new GeoPosition(56, 10), 500)]);

        var outline = MissionMapInteractionCoordinator.FenceOutline(plan);

        outline.Should().HaveCount(36);
        outline[0].LatitudeDegrees.Should().BeGreaterThan(56);
        outline[0].LongitudeDegrees.Should().BeApproximately(10, 1e-9);
    }

    [Fact]
    public void FenceOutline_EmptyPlanWithReturnPoint_UsesTheReturnPoint()
    {
        MissionMapInteractionCoordinator.FenceOutline(FencePlan.Empty with { ReturnPoint = new GeoPosition(9, 9) })
            .Should().Equal(new GeoPosition(9, 9));
        MissionMapInteractionCoordinator.FenceOutline(FencePlan.Empty).Should().BeEmpty();
    }

    [Theory]
    [InlineData(TerrainElevationStatus.Idle, null, "")]
    [InlineData(TerrainElevationStatus.Loading, null, "Terrain: loading")]
    [InlineData(TerrainElevationStatus.Available, "ignored", "Terrain: available")]
    [InlineData(TerrainElevationStatus.OutsideCoverage, "N/A", "Terrain: outside coverage (N/A)")]
    [InlineData(TerrainElevationStatus.NetworkUnavailable, " ", "Terrain: network unavailable")]
    public void TerrainStatusText_FormatsTheStatusAndAppendsMessagesForFailures(TerrainElevationStatus status, string? message, string expected)
    {
        MapPointerPresentation.TerrainStatusText(status, message).Should().Be(expected);
    }

    [Fact]
    public void CoordinateText_WithoutAPointer_ReportsUnavailable()
    {
        MapPointerPresentation.CoordinateText("GEO", null, 10).Should().Be("Position unavailable");
        MapPointerPresentation.CoordinateText("GEO", 56, 10).Should().NotBe("Position unavailable");
    }
}
