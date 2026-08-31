using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData;

/// <inheritdoc />
public partial class FlightDataMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightDataMissionMapViewModel(IServiceFactory factory, ILogger<FlightDataMissionMapViewModel> logger) : base(factory, logger)
    {
    }
}

