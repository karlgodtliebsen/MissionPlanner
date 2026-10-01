using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Hosts offline DataFlash analysis; controller log acquisition remains a separate future workflow.</summary>
public partial class DataFlashLogsViewModel(ILogger<DataFlashLogsViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events)
    : ViewModelBase(logger, dispatcher, events)
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

