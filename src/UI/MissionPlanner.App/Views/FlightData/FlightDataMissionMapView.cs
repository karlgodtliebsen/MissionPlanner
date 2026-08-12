using MissionPlanner.App.Helpers;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>Hosts the mission map backed by the Flight Data mission editor.</summary>
public partial class FlightDataMissionMapView : MissionMapView
{
    /// <summary>Initializes a new instance of the <see cref="FlightDataMissionMapView"/> class.</summary>
    public FlightDataMissionMapView() : base(ServiceHelper.GetRequiredService<IDomainFactory>(), ServiceHelper.GetRequiredService<FlightDataMissionMapViewModel>())
    {
    }
}
