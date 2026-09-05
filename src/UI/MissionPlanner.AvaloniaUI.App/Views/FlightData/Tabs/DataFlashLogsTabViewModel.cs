using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs;

/// <summary>Placeholder for the future DataFlash log workflow.</summary>
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

