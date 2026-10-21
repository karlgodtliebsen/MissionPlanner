using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>
/// The editor state and notifications a mission workflow may use. The singleton
/// <see cref="MissionMapViewModel"/> is the only implementation; workflows never reach past it.
/// </summary>
public interface IMissionEditorHost
{
    /// <summary>Gets the mission being edited.</summary>
    Mission Mission { get; }

    /// <summary>Gets or sets the planned home position.</summary>
    GeoPosition? HomePosition { get; set; }

    /// <summary>Gets or sets the default altitude in metres, relative to home, for new mission items.</summary>
    double DefaultAltitudeMeters { get; set; }

    /// <summary>Gets the waypoint acceptance radius in metres.</summary>
    double WaypointRadiusMeters { get; }

    /// <summary>Gets the loiter radius in metres.</summary>
    double LoiterRadiusMeters { get; }

    /// <summary>Gets the selected map source identifier.</summary>
    string SelectedSourceId { get; }

    /// <summary>Gets the terrain altitude under the map pointer, when known.</summary>
    double? PointerAltitude { get; }

    /// <summary>Gets the position of the active vehicle, when it has a fix.</summary>
    GeoPosition? VehiclePosition { get; }

    /// <summary>Gets the position the last map context action targeted, when valid.</summary>
    GeoPosition? ContextPosition { get; }

    /// <summary>Gets the map interaction state shared by this editor.</summary>
    IMissionMapInteractionCoordinator Interaction { get; }

    /// <summary>Gets the context position, falling back to the vehicle position.</summary>
    GeoPosition? TargetPosition();

    /// <summary>Gets the default altitude as a home-relative mission altitude.</summary>
    MissionAltitude DefaultAltitude();

    /// <summary>Shows a short feedback message.</summary>
    void ShowStatus(string message);

    /// <summary>Rebuilds the rows and map state after the mission changed, then reports <paramref name="message"/>.</summary>
    void NotifyMissionChanged(string message);

    /// <summary>Replaces the mission with <paramref name="mission"/> and asks the map to show all of it.</summary>
    void ReplaceMission(Mission mission, string message);

    /// <summary>Replaces the mission with <paramref name="mission"/> without moving the map.</summary>
    void SetMission(Mission mission, string message);

    /// <summary>Discards pending edits in the mission list rows.</summary>
    void ClearPendingRowEdits();

    /// <summary>Asks the map to pan and zoom to show the whole mission.</summary>
    void RequestFitToMission();

    /// <summary>Appends a waypoint at <paramref name="position"/> when it is valid.</summary>
    void AddWaypoint(GeoPosition position, string message);

    /// <summary>Recomposes the planning overlay drawn over the map.</summary>
    void RefreshPlanningOverlay();

    /// <summary>Records the position later context actions target.</summary>
    void SetMapContext(GeoPosition position, MissionMapContextSource source);

    /// <summary>Asks the map to center on <paramref name="position"/>.</summary>
    void RequestMapCenter(GeoPosition position);

    /// <summary>Asks the map to rotate to <paramref name="degrees"/> for this session.</summary>
    void RequestMapRotation(double degrees);
}

/// <summary>A workflow that acts on one mission editor.</summary>
public interface IMissionWorkflow
{
    /// <summary>Binds the workflow to the editor it acts on; called once by the editor.</summary>
    void Attach(IMissionEditorHost host);
}

/// <summary>Shared host access for workflows, so moved editor logic reads as it did inside the editor.</summary>
public abstract class MissionWorkflowBase : IMissionWorkflow
{
    private IMissionEditorHost? host;

    /// <inheritdoc />
    public void Attach(IMissionEditorHost host)
    {
        this.host = host;
    }

    /// <summary>Gets the attached editor.</summary>
    protected IMissionEditorHost Host => host ?? throw new InvalidOperationException("The workflow is not attached to a mission editor.");

    /// <summary>Gets the mission being edited.</summary>
    protected Mission Mission => Host.Mission;

    /// <summary>Gets the map interaction state of the attached editor.</summary>
    protected IMissionMapInteractionCoordinator Interaction => Host.Interaction;

    /// <summary>Gets the planning polygon of the attached editor.</summary>
    protected Core.Missions.Planning.IPlanningPolygonService Polygons => Host.Interaction.Polygons;

    /// <summary>Gets the terrain altitude under the map pointer, when known.</summary>
    protected double? PointerAltitude => Host.PointerAltitude;

    /// <summary>Gets the selected map source identifier.</summary>
    protected string SelectedSourceId => Host.SelectedSourceId;

    /// <summary>Gets the home position of the attached editor.</summary>
    protected GeoPosition? HomePosition => Host.HomePosition;

    /// <summary>Gets the default altitude in metres.</summary>
    protected double DefaultAltitudeMeters => Host.DefaultAltitudeMeters;

    /// <summary>Gets the waypoint radius in metres.</summary>
    protected double WaypointRadiusMeters => Host.WaypointRadiusMeters;

    /// <summary>Shows a short feedback message.</summary>
    protected void ShowStatus(string message)
    {
        Host.ShowStatus(message);
    }

    /// <summary>Gets the position map context actions target.</summary>
    protected GeoPosition? TargetPosition()
    {
        return Host.TargetPosition();
    }

    /// <summary>Gets the default altitude as a mission altitude.</summary>
    protected MissionAltitude DefaultAltitude()
    {
        return Host.DefaultAltitude();
    }

    /// <summary>Reports a mission change to the editor.</summary>
    protected void OnMissionChanged(string message)
    {
        Host.NotifyMissionChanged(message);
    }

    /// <summary>Replaces the mission and fits the map to it.</summary>
    protected void ReplaceMission(Mission mission, string message)
    {
        Host.ReplaceMission(mission, message);
    }

    /// <summary>Appends a waypoint at a position.</summary>
    protected void AddWaypoint(GeoPosition position, string message)
    {
        Host.AddWaypoint(position, message);
    }

    /// <summary>Recomposes the planning overlay.</summary>
    protected void UpdatePlanningOverlay()
    {
        Host.RefreshPlanningOverlay();
    }
}
