using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Proximity;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Proximity;

/// <summary>Owns the radar's retained snapshot and display range.</summary>
public sealed partial class ProximityRadarViewModel(ILogger<ProximityRadarViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets the latest normalized snapshot.</summary>
    [ObservableProperty]
    public partial ProximitySnapshot Snapshot { get; set; } = ProximitySnapshot.Empty;
    /// <summary>Gets or sets the displayed radius in meters.</summary>
    [ObservableProperty]
    public partial double RangeMeters { get; set; } = 20;
}
