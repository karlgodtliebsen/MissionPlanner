using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Output;

/// <summary>Displays coalesced output counters independently of endpoint editing.</summary>
public sealed partial class OutputStatusViewModel(ILogger<OutputStatusViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets or sets the latest immutable output status.</summary>
    [ObservableProperty] public partial OutputSessionSnapshot Snapshot { get; set; } = new("Stopped", "No endpoint", 0, 0, 0, 0, null, null);
}
