using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel(ILogger logger) : ViewModelBase(logger)
{
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

