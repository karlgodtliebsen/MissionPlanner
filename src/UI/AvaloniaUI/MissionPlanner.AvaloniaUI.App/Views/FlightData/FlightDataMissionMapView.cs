using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData;

/// <summary>Hosts the shared Avalonia mission map with the Flight Data map ViewModel.</summary>
public sealed class FlightDataMissionMapView : MissionMapView
{
    public FlightDataMissionMapView() : base(ServiceHelper.GetRequiredService<IDomainFactory>())
    {
    }
}
