using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Nmea;

/// <summary>Displays only the latest complete NMEA batch and sentence counters.</summary>
public sealed partial class NmeaPreviewViewModel(ILogger<NmeaPreviewViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets or sets the latest batch.</summary>
    [ObservableProperty] public partial NmeaBatch Batch { get; set; } = new("", 0, false, "No scheduled batch.");
    /// <summary>Gets or sets sentence accounting.</summary>
    [ObservableProperty] public partial string Counters { get; set; } = "No sentences scheduled.";
}
