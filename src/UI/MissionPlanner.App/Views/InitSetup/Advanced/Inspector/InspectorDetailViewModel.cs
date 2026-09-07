using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Inspector;

/// <summary>Owns selected decoded fields, raw bytes and explicit clipboard copying.</summary>
public sealed partial class InspectorDetailViewModel(ITextClipboardService clipboard,
    ILogger<InspectorDetailViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events)
    : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets the bounded selected row and field text.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CopyAsync()
    {
        await clipboard.SetTextAsync(Text);
    }
}
