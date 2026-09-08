using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <inheritdoc />
public partial class FlightPlannerMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightPlannerMissionMapViewModel(IServiceFactory factory, ILogger<FlightDataMissionMapViewModel> logger) : base(factory, logger)
    {
    }
}
