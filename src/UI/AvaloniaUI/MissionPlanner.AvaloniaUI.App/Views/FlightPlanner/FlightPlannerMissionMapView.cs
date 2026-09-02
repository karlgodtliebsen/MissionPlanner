using MissionPlanner.AvaloniaUI.App.Services;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightPlanner;

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
