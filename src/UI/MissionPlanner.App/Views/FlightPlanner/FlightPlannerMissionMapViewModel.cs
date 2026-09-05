using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Provides an independently scoped mission-map editor for the Flight Planner page.
/// </summary>
public sealed partial class FlightPlannerMissionMapViewModel : MissionMapViewModel
{
    /// <summary>Initializes the Flight Planner mission-map editor.</summary>
    public FlightPlannerMissionMapViewModel(IServiceFactory factory, ILogger<FlightPlannerMissionMapViewModel> logger) : base(factory, logger)
    {
    }
}
