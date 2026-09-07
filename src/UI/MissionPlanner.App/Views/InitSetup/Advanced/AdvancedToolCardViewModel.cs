using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced;

/// <summary>Owns one bounded catalogue card and its launch request.</summary>
public sealed partial class AdvancedToolCardViewModel : ViewModelBase
{
    /// <summary>Initializes a card with its immutable descriptor.</summary>
    public AdvancedToolCardViewModel(AdvancedFeature feature, ILogger logger, IUiDispatcher dispatcher, IDomainEventHub eventHub)
        : base(logger, dispatcher, eventHub)
    {
        Feature = feature;
    }

    /// <summary>Gets the catalogue descriptor.</summary>
    public AdvancedFeature Feature { get; }

    /// <summary>Gets the current launch prerequisites.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial AdvancedAvailability Availability { get; set; } = new(AdvancedAvailabilityState.TemporarilyUnavailable, "Checking availability.");

    /// <summary>Requests navigation from the active parent.</summary>
    public event Action<AdvancedFeatureId>? LaunchRequested;

    private bool CanLaunch() => Availability.CanLaunch;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private void Launch()
    {
        LaunchRequested?.Invoke(Feature.Id);
    }
}
