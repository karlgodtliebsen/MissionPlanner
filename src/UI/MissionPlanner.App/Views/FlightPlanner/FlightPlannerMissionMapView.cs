using MissionPlanner.App.Services;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>Hosts the shared Avalonia mission map for the Flight Planner page.</summary>
public sealed class FlightPlannerMissionMapView : MissionMapView
{
    /// <summary>Initializes a new instance of the <see cref="FlightPlannerMissionMapView"/> class.</summary>
    public FlightPlannerMissionMapView() : base(
        ServiceHelper.GetRequiredService<IDomainFactory>(),
        ServiceHelper.GetRequiredService<IPlatformLocationService>())
    {
    }
}
