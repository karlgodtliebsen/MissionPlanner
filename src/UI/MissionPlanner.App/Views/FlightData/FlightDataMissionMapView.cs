using MissionPlanner.App.Services;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>Hosts the shared Avalonia mission map with the Flight Data map ViewModel.</summary>
public sealed class FlightDataMissionMapView : MissionMapView
{
    /// <summary>Creates the shared map control for runtime or design preview.</summary>
    public FlightDataMissionMapView()
    {
    }
}
