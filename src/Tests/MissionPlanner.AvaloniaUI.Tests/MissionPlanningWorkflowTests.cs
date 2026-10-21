using FluentAssertions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Missions.Workflows;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Prefetch;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the fence/rally, polygon, generated-mission and map-tool workflows.</summary>
public sealed class MissionPlanningWorkflowTests
{
    private static readonly VehicleId Vehicle = new(1, 1);
    private readonly FakeMissionEditorHost host = new();
    private readonly IDialogService dialogs = Substitute.For<IDialogService>();
    private readonly IUserConfirmationService confirmations = Substitute.For<IUserConfirmationService>();
    private readonly IMissionFileInteraction files = Substitute.For<IMissionFileInteraction>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();

    public MissionPlanningWorkflowTests()
    {
        dialogs.CreateOptions(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>()).Returns(_ => new Ursa.Controls.OverlayDialogOptions());
    }

    private void AnswerChoice(string? choice)
    {
        dialogs.ChooseAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(choice);
    }

    private void AnswerTextPrompt(string title, string? answer, string? message = null)
    {
        dialogs.PromptAsync(title, message ?? Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(answer);
    }

    #region Vehicle plans

    private (VehiclePlanWorkflow Workflow, IActiveVehicleContext Vehicle, IFenceConfigurationService Fence, IRallyConfigurationService Rally,
        IFencePlanFileCodec FenceCodec) CreateVehiclePlan(bool online = true)
    {
        var vehicle = Substitute.For<IActiveVehicleContext>();
        vehicle.VehicleId.Returns(online ? Vehicle : (VehicleId?)null);
        var fence = Substitute.For<IFenceConfigurationService>();
        fence.GetSnapshot(Vehicle).Returns(new FenceConfigurationSnapshot(Vehicle, FencePlan.Empty, null, null, 0, null, false));
        var rally = Substitute.For<IRallyConfigurationService>();
        rally.GetSnapshot(Vehicle).Returns(new RallyPlanSnapshot(Vehicle, RallyPlan.Empty, null, 0, null, false, null));
        var fenceCodec = Substitute.For<IFencePlanFileCodec>();
        var workflow = new VehiclePlanWorkflow(vehicle, fence, rally, fenceCodec, Substitute.For<IRallyPlanFileCodec>(), clock,
            confirmations, dialogs, files);
        workflow.Attach(host);
        return (workflow, vehicle, fence, rally, fenceCodec);
    }

    [Fact]
    public async Task DownloadFence_WithoutAVehicle_Reports()
    {
        var (workflow, _, fence, _, _) = CreateVehiclePlan(false);

        await workflow.DownloadFenceAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Connect a vehicle first.");
        await fence.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default, default);
    }

    [Fact]
    public async Task DownloadFence_DirtyLocalPlanNotConfirmed_DoesNotDownload()
    {
        var (workflow, _, fence, _, _) = CreateVehiclePlan();
        fence.GetSnapshot(Vehicle).Returns(new FenceConfigurationSnapshot(Vehicle, FencePlan.Empty, null, null, 1, null, true));
        confirmations.ConfirmAsync("Replace local fence", Arg.Any<string>(), "Download", Arg.Any<CancellationToken>()).Returns(false);

        await workflow.DownloadFenceAsync(CancellationToken.None);

        await fence.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default, default);
    }

    [Fact]
    public async Task DownloadFence_ReportsTheServiceMessage()
    {
        var (workflow, _, fence, _, _) = CreateVehiclePlan();
        var snapshot = fence.GetSnapshot(Vehicle);
        fence.DownloadAsync(Vehicle, true, Arg.Any<IProgress<FenceTransferProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new FenceOperationReport(true, "Fence downloaded.", snapshot, FenceValidationResult.Valid));

        await workflow.DownloadFenceAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Fence downloaded.");
    }

    [Fact]
    public async Task SaveFence_WritesTheSerializedPlanAndReportsThePath()
    {
        var (workflow, _, _, _, codec) = CreateVehiclePlan();
        codec.Serialize(Arg.Any<FencePlan>()).Returns("{fence}");
        files.SaveTextAsync("mission-fence.mpfence", "{fence}", Arg.Any<CancellationToken>()).Returns("C:\\fence.mpfence");

        await workflow.SaveFenceAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Fence saved to C:\\fence.mpfence.");
    }

    [Fact]
    public async Task SaveFence_Cancelled_Reports()
    {
        var (workflow, _, _, _, _) = CreateVehiclePlan();
        files.SaveTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        await workflow.SaveFenceAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Fence save cancelled.");
    }

    [Fact]
    public async Task LoadFence_InvalidFile_ReportsTheCodecMessage()
    {
        var (workflow, _, fence, _, codec) = CreateVehiclePlan();
        files.OpenTextAsync("Open MissionPlanner fence", Arg.Any<CancellationToken>()).Returns(new OpenedText("a.mpfence", "bad"));
        codec.Deserialize("bad").Returns(_ => throw new InvalidDataException("Not a fence file."));

        await workflow.LoadFenceAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Not a fence file.");
        fence.DidNotReceiveWithAnyArgs().SetLocalPlan(default, default!);
    }

    [Fact]
    public async Task LoadFence_ValidFile_ReplacesTheLocalPlan()
    {
        var (workflow, _, fence, _, codec) = CreateVehiclePlan();
        var plan = new FencePlan(new GeoPosition(1, 2), []);
        files.OpenTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new OpenedText("a.mpfence", "ok"));
        codec.Deserialize("ok").Returns(plan);

        await workflow.LoadFenceAsync(CancellationToken.None);

        fence.Received(1).SetLocalPlan(Vehicle, plan);
        host.LastStatus.Should().Be("Fence loaded locally; upload to apply it.");
    }

    [Fact]
    public async Task UploadFence_NotConfirmed_DoesNotTouchTheVehicle()
    {
        var (workflow, _, fence, _, _) = CreateVehiclePlan();
        confirmations.ConfirmAsync("Replace vehicle fence", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await workflow.UploadFenceAsync(CancellationToken.None);

        await fence.DidNotReceiveWithAnyArgs().OpenParameterSessionAsync(default, default);
    }

    [Fact]
    public async Task ClearRally_LocalOnly_ResetsTheLocalPlanWithoutConfirmation()
    {
        var (workflow, _, _, rally, _) = CreateVehiclePlan();
        AnswerChoice("Clear local plan only");

        await workflow.ClearRallyAsync(CancellationToken.None);

        rally.Received(1).SetLocalPlan(Vehicle, RallyPlan.Empty);
        host.LastStatus.Should().Be("Local rally points cleared; vehicle unchanged.");
        await rally.DidNotReceiveWithAnyArgs().ClearVehicleAsync(default, default);
    }

    [Fact]
    public async Task ClearRally_Vehicle_RequiresConfirmation()
    {
        var (workflow, _, _, rally, _) = CreateVehiclePlan();
        AnswerChoice("Clear vehicle rally points");
        confirmations.ConfirmAsync("Clear vehicle rally points", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await workflow.ClearRallyAsync(CancellationToken.None);

        await rally.DidNotReceiveWithAnyArgs().ClearVehicleAsync(default, default);
    }

    [Fact]
    public async Task SetRallyPoint_StoresTheAltitudeAndStartsTheInteraction()
    {
        CreateVehiclePlan();
        var (workflow, _, _, _, _) = CreateVehiclePlan();
        AnswerTextPrompt("Set rally point", "80");
        AnswerChoice("Terrain");

        await workflow.SetRallyPointAsync(CancellationToken.None);

        host.Interaction.PendingRallyAltitude.Should().Be(new MissionAltitude(80, MissionAltitudeReference.Terrain));
        host.Interaction.Received(1).Begin(MissionMapInteractionMode.SetRallyPoint, Arg.Any<string>());
    }

    [Fact]
    public async Task SetRallyPoint_InvalidAltitude_Reports()
    {
        var (workflow, _, _, _, _) = CreateVehiclePlan();
        AnswerTextPrompt("Set rally point", "high");

        await workflow.SetRallyPointAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Enter a valid rally altitude.");
        host.Interaction.DidNotReceiveWithAnyArgs().Begin(default, default!);
    }

    #endregion

    #region Polygon planning

    private PolygonPlanningWorkflow CreatePolygonWorkflow()
    {
        var workflow = new PolygonPlanningWorkflow(clock, confirmations, dialogs, files);
        workflow.Attach(host);
        return workflow;
    }

    [Fact]
    public void FinishPolygon_WhenNotDrawing_Reports()
    {
        CreatePolygonWorkflow().FinishPolygon();

        host.LastStatus.Should().Be("Polygon drawing is not active.");
    }

    [Fact]
    public void FinishPolygon_StoresTheDrawnVerticesAndCompletes()
    {
        GeoPosition[] vertices = [new(1, 1), new(1, 2), new(2, 2)];
        host.Interaction.State.Returns(new MissionMapInteractionState(MissionMapInteractionMode.DrawPolygon, "draw", vertices, null));
        host.Polygons.Set("Planning polygon", Arg.Any<IEnumerable<GeoPosition>>()).Returns(new PlanningPolygonOperationResult(true, "Polygon set."));

        CreatePolygonWorkflow().FinishPolygon();

        host.Interaction.Received(1).Complete();
        host.LastStatus.Should().Be("Polygon set.");
    }

    [Fact]
    public void FinishPolygon_Rejected_KeepsTheInteractionRunning()
    {
        host.Interaction.State.Returns(new MissionMapInteractionState(MissionMapInteractionMode.DrawPolygon, "draw", [], null));
        host.Polygons.Set(Arg.Any<string>(), Arg.Any<IEnumerable<GeoPosition>>()).Returns(new PlanningPolygonOperationResult(false, "Too few vertices."));

        CreatePolygonWorkflow().FinishPolygon();

        host.Interaction.DidNotReceive().Complete();
        host.LastStatus.Should().Be("Too few vertices.");
    }

    [Fact]
    public void ClearPolygon_ClearsAndCancelsTheInteraction()
    {
        CreatePolygonWorkflow().ClearPolygon();

        host.Polygons.Received(1).Clear();
        host.Interaction.Received(1).Cancel();
        host.LastStatus.Should().Be("Planning polygon cleared.");
    }

    [Fact]
    public void ShowPolygonArea_WithoutAPolygon_Reports()
    {
        CreatePolygonWorkflow().ShowPolygonArea();

        host.LastStatus.Should().Be("Create a polygon first.");
    }

    [Fact]
    public async Task SavePolygon_WithoutAPolygon_DoesNotOpenAPicker()
    {
        await CreatePolygonWorkflow().SavePolygonAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Create a polygon first.");
        await files.DidNotReceiveWithAnyArgs().SaveTextAsync(default!, default!, default);
    }

    [Fact]
    public async Task KmlOverlay_ReplacesTheImportedOverlayAndRefreshes()
    {
        var workflow = CreatePolygonWorkflow();
        GeospatialFeature[] features =
        [
            new("Area", GeospatialGeometryKind.Polygon, [new GeoPosition(1, 1), new GeoPosition(1, 2), new GeoPosition(2, 2)]),
            new("Empty", GeospatialGeometryKind.Point, [])
        ];
        files.ImportGeospatialAsync("Open KML overlay", false, Arg.Any<CancellationToken>())
            .Returns(new GeospatialFileResult(new GeospatialImportResult(true, "ok", features, new GeospatialImportPreview(0, 0, 1, 0, 0)), null));

        await workflow.KmlOverlayAsync(CancellationToken.None);

        var overlay = workflow.ImportedOverlays.Should().ContainSingle().Subject;
        overlay.Name.Should().Be("Area");
        overlay.IsClosed.Should().BeTrue();
        host.OverlayRefreshes.Should().Be(1);
        host.LastStatus.Should().Be("Overlay replaced with 1 imported features.");

        workflow.ClearImportedOverlay();

        workflow.ImportedOverlays.Should().BeEmpty();
        host.OverlayRefreshes.Should().Be(2);
    }

    [Fact]
    public async Task KmlOverlay_ImportFailure_ReportsAndKeepsTheOverlay()
    {
        var workflow = CreatePolygonWorkflow();
        files.ImportGeospatialAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GeospatialFileResult(null, "Unsupported file."));

        await workflow.KmlOverlayAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Unsupported file.");
        workflow.ImportedOverlays.Should().BeEmpty();
        host.OverlayRefreshes.Should().Be(0);
    }

    [Fact]
    public async Task LoadKmlFile_ReplaceMission_BuildsWaypointsAtTheDefaultAltitude()
    {
        var workflow = CreatePolygonWorkflow();
        GeospatialFeature[] features = [new("Route", GeospatialGeometryKind.LineString, [new GeoPosition(1, 1), new GeoPosition(1, 2)], null, 70)];
        files.ImportGeospatialAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GeospatialFileResult(new GeospatialImportResult(true, "ok", features, new GeospatialImportPreview(0, 1, 0, 2, 0)), null));
        AnswerChoice("Replace mission");

        await workflow.LoadKmlFileAsync(CancellationToken.None);

        host.Mission.Name.Should().Be("Imported mission");
        host.Mission.Items.Should().HaveCount(2);
        host.FitRequests.Should().Be(1);
        host.LastStatus.Should().Be("Mission replaced with 2 imported waypoints.");
    }

    [Fact]
    public async Task LoadKmlFile_AppendWaypoints_KeepsTheCurrentMission()
    {
        var workflow = CreatePolygonWorkflow();
        host.AddWaypointItem(5, 5);
        GeospatialFeature[] features = [new("Spot", GeospatialGeometryKind.Point, [new GeoPosition(1, 1)])];
        files.ImportGeospatialAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GeospatialFileResult(new GeospatialImportResult(true, "ok", features, new GeospatialImportPreview(1, 0, 0, 1, 0)), null));
        AnswerChoice("Append waypoints");

        await workflow.LoadKmlFileAsync(CancellationToken.None);

        host.Mission.Items.Should().HaveCount(2);
        host.MissionChanges.Should().Equal("Appended 1 imported waypoints.");
        host.FitRequests.Should().Be(0);
    }

    #endregion

    #region Generated missions

    private (GeneratedMissionWorkflow Workflow, IAutoWaypointGenerator Waypoints, ISurveyMissionGenerator Surveys) CreateGeneratedWorkflow()
    {
        var waypoints = Substitute.For<IAutoWaypointGenerator>();
        var surveys = Substitute.For<ISurveyMissionGenerator>();
        var workflow = new GeneratedMissionWorkflow(waypoints, surveys, dialogs, confirmations);
        workflow.Attach(host);
        return (workflow, waypoints, surveys);
    }

    private static MissionItem[] GeneratedItems()
    {
        return
        [
            new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(1, 1), new MissionAltitude(50, MissionAltitudeReference.Home), TimeSpan.Zero, 10),
            new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(1, 2), new MissionAltitude(50, MissionAltitudeReference.Home), TimeSpan.Zero, 10)
        ];
    }

    [Fact]
    public async Task CreateCircle_WithoutATargetPosition_Reports()
    {
        var (workflow, waypoints, _) = CreateGeneratedWorkflow();

        await workflow.CreateWaypointCircleAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Select a map position for the circle center.");
        waypoints.DidNotReceiveWithAnyArgs().GenerateCircle(default!);
    }

    [Fact]
    public async Task AutoWaypointText_AppendsTheGeneratedItemsAndClearsThePreview()
    {
        var (workflow, waypoints, _) = CreateGeneratedWorkflow();
        host.ContextPosition = new GeoPosition(56, 10);
        host.AddWaypointItem(5, 5);
        AnswerTextPrompt("Waypoint text", "HI", "Text (supported stroke-font letters/digits, maximum 32)");
        AnswerTextPrompt("Waypoint text", "50", "Character height in metres");
        var items = GeneratedItems();
        waypoints.GenerateText(Arg.Any<TextWaypointRequest>())
            .Returns(new AutoWaypointGenerationResult(true, "ok", items, [new GeoPosition(1, 1)]));
        AnswerChoice("Append to mission");

        await workflow.AutoWaypointTextAsync(CancellationToken.None);

        host.Mission.Items.Should().HaveCount(3);
        host.MissionChanges.Should().Equal("Appended 2 generated mission items.");
        workflow.SurveyPreview.Should().BeEmpty();
    }

    [Fact]
    public async Task AutoWaypointText_ReplaceMission_ReplacesAndFits()
    {
        var (workflow, waypoints, _) = CreateGeneratedWorkflow();
        host.ContextPosition = new GeoPosition(56, 10);
        host.AddWaypointItem(5, 5);
        AnswerTextPrompt("Waypoint text", "HI", "Text (supported stroke-font letters/digits, maximum 32)");
        AnswerTextPrompt("Waypoint text", "50", "Character height in metres");
        waypoints.GenerateText(Arg.Any<TextWaypointRequest>())
            .Returns(new AutoWaypointGenerationResult(true, "ok", GeneratedItems(), []));
        AnswerChoice("Replace mission");

        await workflow.AutoWaypointTextAsync(CancellationToken.None);

        host.Mission.Name.Should().Be("Generated mission");
        host.Mission.Items.Should().HaveCount(2);
        host.FitRequests.Should().Be(1);
    }

    [Fact]
    public async Task AutoWaypointText_DismissedPreview_LeavesTheMissionAlone()
    {
        var (workflow, waypoints, _) = CreateGeneratedWorkflow();
        host.ContextPosition = new GeoPosition(56, 10);
        AnswerTextPrompt("Waypoint text", "HI", "Text (supported stroke-font letters/digits, maximum 32)");
        AnswerTextPrompt("Waypoint text", "50", "Character height in metres");
        waypoints.GenerateText(Arg.Any<TextWaypointRequest>())
            .Returns(new AutoWaypointGenerationResult(true, "ok", GeneratedItems(), [new GeoPosition(1, 1)]));
        AnswerChoice(null);

        await workflow.AutoWaypointTextAsync(CancellationToken.None);

        host.Mission.Items.Should().BeEmpty();
        workflow.SurveyPreview.Should().BeEmpty();
        host.OverlayRefreshes.Should().Be(2);
    }

    [Fact]
    public async Task AutoWaypointText_GeneratorFailure_ReportsItsMessage()
    {
        var (workflow, waypoints, _) = CreateGeneratedWorkflow();
        host.ContextPosition = new GeoPosition(56, 10);
        AnswerTextPrompt("Waypoint text", "????", "Text (supported stroke-font letters/digits, maximum 32)");
        AnswerTextPrompt("Waypoint text", "50", "Character height in metres");
        waypoints.GenerateText(Arg.Any<TextWaypointRequest>())
            .Returns(new AutoWaypointGenerationResult(false, "Unsupported character.", [], []));

        await workflow.AutoWaypointTextAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Unsupported character.");
    }

    [Fact]
    public async Task CreateGridSurvey_WithoutAPolygon_Reports()
    {
        var (workflow, _, surveys) = CreateGeneratedWorkflow();

        await workflow.CreateGridSurveyAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Create or load a planning polygon first.");
        surveys.DidNotReceiveWithAnyArgs().GenerateGrid(default!);
    }

    #endregion

    #region Map tools

    private (MapToolsWorkflow Workflow, IPoiService Poi, IMapTilePrefetchService Prefetch) CreateMapTools()
    {
        var prefetch = Substitute.For<IMapTilePrefetchService>();
        var poi = Substitute.For<IPoiService>();
        poi.Snapshot.Returns(new PoiSnapshot([], null));
        var workflow = new MapToolsWorkflow(prefetch, poi, Substitute.For<ITrackerHomeService>(), new GeodeticCoordinateConverter(),
            dialogs, confirmations, clock);
        workflow.Attach(host);
        return (workflow, poi, prefetch);
    }

    [Theory]
    [InlineData("370", 10)]
    [InlineData("-90", 270)]
    [InlineData("0", 0)]
    public async Task RotateMap_NormalizesTheBearing(string text, double expected)
    {
        var (workflow, _, _) = CreateMapTools();
        AnswerTextPrompt("Rotate map", text);

        await workflow.RotateMapAsync(CancellationToken.None);

        host.RotationRequests.Should().Equal(expected);
        host.LastStatus.Should().StartWith("Map rotation set to");
    }

    [Fact]
    public async Task RotateMap_InvalidBearing_ReportsButCancelIsSilent()
    {
        var (workflow, _, _) = CreateMapTools();
        AnswerTextPrompt("Rotate map", "north");
        await workflow.RotateMapAsync(CancellationToken.None);
        host.LastStatus.Should().Be("Enter a valid bearing.");

        host.Statuses.Clear();
        AnswerTextPrompt("Rotate map", null);
        await workflow.RotateMapAsync(CancellationToken.None);

        host.Statuses.Should().BeEmpty();
        host.RotationRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task PrefetchVisible_WithoutAnyCenter_Reports()
    {
        var (workflow, _, prefetch) = CreateMapTools();

        await workflow.PrefetchVisibleAsync(CancellationToken.None);

        host.LastStatus.Should().Be("Select a map position before visible-area prefetch.");
        await prefetch.DidNotReceiveWithAnyArgs().EstimateAsync(default!, default);
    }

    [Fact]
    public async Task PrefetchWaypointPath_EmptyMission_Reports()
    {
        var (workflow, _, _) = CreateMapTools();

        await workflow.PrefetchWaypointPathAsync(CancellationToken.None);

        host.LastStatus.Should().Be("The mission has no positioned route to prefetch.");
    }

    [Fact]
    public async Task EnterUtmCoordinate_Invalid_ReportsTheParserMessage()
    {
        var (workflow, _, _) = CreateMapTools();
        AnswerTextPrompt("Enter UTM coordinate", "garbage");
        dialogs.PromptAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("garbage");

        await workflow.EnterUtmCoordinateAsync(CancellationToken.None);

        host.Statuses.Should().ContainSingle();
        host.Contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task EnterUtmCoordinate_CenterMap_RecordsContextAndRequestsCentering()
    {
        var (workflow, _, _) = CreateMapTools();
        dialogs.PromptAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("32N 500000 6170000");
        AnswerChoice("Center map here");

        await workflow.EnterUtmCoordinateAsync(CancellationToken.None);

        host.Contexts.Should().ContainSingle().Which.Source.Should().Be(MissionMapContextSource.CoordinateEntry);
        host.CenterRequests.Should().ContainSingle();
        host.CenterRequests[0].LatitudeDegrees.Should().BeApproximately(55.6, 0.5);
        host.LastStatus.Should().Be("Map centered on converted UTM coordinate.");
    }

    [Fact]
    public async Task EnterUtmCoordinate_AddWaypoint_AddsAtTheConvertedPosition()
    {
        var (workflow, _, _) = CreateMapTools();
        dialogs.PromptAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("32N 500000 6170000");
        AnswerChoice("Add waypoint here");

        await workflow.EnterUtmCoordinateAsync(CancellationToken.None);

        host.Mission.Items.Should().ContainSingle();
        host.MissionChanges.Should().Equal("Waypoint added from UTM coordinate.");
    }

    [Fact]
    public async Task DeletePoi_WithNothingNearby_Reports()
    {
        var (workflow, _, _) = CreateMapTools();
        host.ContextPosition = new GeoPosition(56, 10);

        await workflow.DeletePoiAsync(CancellationToken.None);

        host.LastStatus.Should().Be("No POI is available to delete.");
    }

    [Fact]
    public async Task DeletePoi_Confirmed_DeletesTheNearestItem()
    {
        var (workflow, poi, _) = CreateMapTools();
        host.ContextPosition = new GeoPosition(56, 10);
        var item = new PointOfInterest(PointOfInterestId.New(), "Barn", new GeoPosition(56, 10), null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        poi.FindNearest(Arg.Any<GeoPosition>()).Returns(item);
        confirmations.ConfirmAsync("Delete nearest POI", Arg.Any<string>(), "Delete", Arg.Any<CancellationToken>()).Returns(true);

        await workflow.DeletePoiAsync(CancellationToken.None);

        await poi.Received(1).DeleteAsync(item.Id, Arg.Any<CancellationToken>());
        host.LastStatus.Should().Be("Local POI 'Barn' deleted.");
    }

    #endregion
}
