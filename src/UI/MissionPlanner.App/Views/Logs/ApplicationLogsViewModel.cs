using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Hosts application diagnostics in the Logs workspace.</summary>
public sealed partial class ApplicationLogsViewModel : ViewModelBase
{
    /// <summary>Initializes the application diagnostics host.</summary>
    public ApplicationLogsViewModel(ILogger<ApplicationLogsViewModel> logger) : base(logger)
    {
    }
}
