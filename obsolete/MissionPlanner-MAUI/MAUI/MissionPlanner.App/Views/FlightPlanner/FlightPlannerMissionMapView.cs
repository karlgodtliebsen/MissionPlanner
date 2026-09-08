using MissionPlanner.App.Helpers;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>Hosts the mission map backed by the Flight Planner mission editor.</summary>
public class FlightPlannerMissionMapView : MissionMapView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerMissionMapView"/> class.
    /// </summary>
    public FlightPlannerMissionMapView() : base(ServiceHelper.GetRequiredService<IDomainFactory>())
    {
    }
}
