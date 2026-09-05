using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Help;

/// <summary>
/// Provides the public API for HelpViewModel.
/// </summary>
public partial class HelpViewModel(ILogger<HelpViewModel> logger) : ViewModelBase(logger)
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

