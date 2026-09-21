using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>
/// Interaction logic for OptionalHardwareBaseViewModel.xaml
/// </summary>
public partial class OptionalHardwareBaseViewModel : ViewModelBase
{
    /// <summary>Initializes an optional hardware view with application services.</summary>
    /// <param name="logger">View logger.</param>
    public OptionalHardwareBaseViewModel(ILogger logger) : base(logger)
    {
    }

    /// <summary>Initializes an optional hardware view with explicit presentation services.</summary>
    /// <param name="logger">View logger.</param>
    /// <param name="dispatcher">UI dispatcher.</param>
    /// <param name="eventHub">Domain event hub.</param>
    protected OptionalHardwareBaseViewModel(ILogger logger,
        MissionPlanner.App.Utilities.Dispatching.IUiDispatcher dispatcher,
        MissionPlanner.Library.EventHub.Abstractions.IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
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

