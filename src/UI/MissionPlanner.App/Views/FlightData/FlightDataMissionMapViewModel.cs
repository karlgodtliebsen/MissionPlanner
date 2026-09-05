using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <inheritdoc />
public partial class FlightDataMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightDataMissionMapViewModel(IServiceFactory factory, ILogger<FlightDataMissionMapViewModel> logger) : base(factory, logger)
    {
    }
}

