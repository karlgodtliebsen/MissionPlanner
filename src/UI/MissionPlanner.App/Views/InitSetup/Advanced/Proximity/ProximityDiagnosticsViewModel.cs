using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Proximity;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Proximity;

/// <summary>Owns bounded source diagnostics independently of radar rendering.</summary>
public sealed partial class ProximityDiagnosticsViewModel(ILogger<ProximityDiagnosticsViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets current measurements including stale, unsupported and invalid states.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProximityPoint> Points { get; set; } = [];
}
