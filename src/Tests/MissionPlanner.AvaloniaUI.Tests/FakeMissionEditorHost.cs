using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Missions.Workflows;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Records what a mission workflow asks of its editor, without any view model or UI.</summary>
internal sealed class FakeMissionEditorHost : IMissionEditorHost
{
    public FakeMissionEditorHost()
    {
        Polygons = Substitute.For<IPlanningPolygonService>();
        Polygons.Snapshot.Returns(PlanningPolygonSnapshot.Empty);
        Interaction = Substitute.For<IMissionMapInteractionCoordinator>();
        Interaction.Polygons.Returns(Polygons);
        Interaction.State.Returns(MissionMapInteractionState.None);
    }

    public IPlanningPolygonService Polygons { get; }

    public List<string> Statuses { get; } = [];

    public List<string> MissionChanges { get; } = [];

    public List<(GeoPosition Position, MissionMapContextSource Source)> Contexts { get; } = [];

    public List<GeoPosition> CenterRequests { get; } = [];

    public List<double> RotationRequests { get; } = [];

    public int OverlayRefreshes { get; private set; }

    public int FitRequests { get; private set; }

    public int ClearedRowEdits { get; private set; }

    public string LastStatus => Statuses[^1];

    public Mission Mission { get; private set; } = new(MissionId.New(), "Test");

    public GeoPosition? HomePosition { get; set; }

    public double DefaultAltitudeMeters { get; set; } = 100;

    public double WaypointRadiusMeters => 30;

    public double LoiterRadiusMeters => 45;

    public string SelectedSourceId => "osm-standard";

    public double? PointerAltitude { get; set; }

    public GeoPosition? VehiclePosition { get; set; }

    public GeoPosition? ContextPosition { get; set; }

    public IMissionMapInteractionCoordinator Interaction { get; }

    public GeoPosition? TargetPosition() => ContextPosition ?? VehiclePosition;

    public MissionAltitude DefaultAltitude() => new(DefaultAltitudeMeters, MissionAltitudeReference.Home);

    public void ShowStatus(string message) => Statuses.Add(message);

    public void NotifyMissionChanged(string message)
    {
        MissionChanges.Add(message);
        Statuses.Add(message);
    }

    public void ReplaceMission(Mission mission, string message)
    {
        Mission = mission;
        NotifyMissionChanged(message);
        FitRequests++;
    }

    public void SetMission(Mission mission, string message)
    {
        Mission = mission;
        NotifyMissionChanged(message);
    }

    public void AddWaypoint(GeoPosition position, string message)
    {
        Mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, position, DefaultAltitude(), TimeSpan.Zero, WaypointRadiusMeters));
        NotifyMissionChanged(message);
    }

    public void ClearPendingRowEdits() => ClearedRowEdits++;

    public void RequestFitToMission() => FitRequests++;

    public void RefreshPlanningOverlay() => OverlayRefreshes++;

    public void SetMapContext(GeoPosition position, MissionMapContextSource source) => Contexts.Add((position, source));

    public void RequestMapCenter(GeoPosition position) => CenterRequests.Add(position);

    public void RequestMapRotation(double degrees) => RotationRequests.Add(degrees);

    public WaypointMissionItem AddWaypointItem(double latitude, double longitude, double altitude = 50)
    {
        var item = new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(latitude, longitude),
            new MissionAltitude(altitude, MissionAltitudeReference.Home), TimeSpan.Zero, 30);
        Mission.Add(item);
        return item;
    }
}
