using MissionPlanner.App.Services;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>Hosts the shared Avalonia mission map with the Flight Data map ViewModel.</summary>
public sealed class FlightDataMissionMapView : MissionMapView
{
    public FlightDataMissionMapView() : base(
        ServiceHelper.GetRequiredService<IDomainFactory>(),
        ServiceHelper.GetRequiredService<IPlatformLocationService>())
    {
    }
}
