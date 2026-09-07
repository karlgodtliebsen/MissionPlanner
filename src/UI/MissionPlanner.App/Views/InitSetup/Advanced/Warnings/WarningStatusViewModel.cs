using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Warnings;

/// <summary>Owns the bounded warning status panel and targeted acknowledgement.</summary>
public sealed partial class WarningStatusViewModel(ILogger<WarningStatusViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets the latest state of each configured rule.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WarningSnapshot> Warnings { get; set; } = [];
    /// <summary>Gets or sets the selected warning.</summary>
    [ObservableProperty]
    public partial WarningSnapshot? Selected { get; set; }
    /// <summary>Requests acknowledgement of one rule.</summary>
    public event Action<Guid>? AcknowledgeRequested;

    [RelayCommand]
    private void Acknowledge()
    {
        if (Selected is not null)
        {
            AcknowledgeRequested?.Invoke(Selected.RuleId);
        }
    }
}
