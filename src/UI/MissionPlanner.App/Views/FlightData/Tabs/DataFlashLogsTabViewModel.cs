using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Hosts offline DataFlash analysis; controller log acquisition remains a separate future workflow.</summary>
public partial class DataFlashLogsTabViewModel(ILogger<DataFlashLogsTabViewModel> logger) : ViewModelBase(logger)
{
    /// <inheritdoc />
    public override void Dispose()
    {
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}

