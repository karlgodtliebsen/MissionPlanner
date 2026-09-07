using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Inspector;

/// <summary>Owns inspector filtering, sorting and display freeze independently of collection.</summary>
public sealed partial class InspectorListViewModel(ILogger<InspectorListViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets or sets search text; supports id:, sys:, comp:, dir: tokens.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;
    /// <summary>Gets or sets the display-only pause.</summary>
    [ObservableProperty]
    public partial bool Frozen { get; set; }
    /// <summary>Gets or sets descending count sort instead of stable message-ID sort.</summary>
    [ObservableProperty]
    public partial bool SortByCount { get; set; }
    /// <summary>Gets the currently displayed statistics.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<InspectorRow> Rows { get; set; } = [];
    /// <summary>Gets or sets the selected aggregate.</summary>
    [ObservableProperty]
    public partial InspectorRow? Selected { get; set; }
    /// <summary>Requests detail selection from the parent.</summary>
    public event Action<InspectorKey?>? SelectionChanged;
    /// <summary>Requests a statistics reset.</summary>
    public event Action<bool>? ClearRequested;
    /// <summary>Requests an immutable display snapshot when freezing.</summary>
    public event Action<bool>? FreezeChanged;

    partial void OnSelectedChanged(InspectorRow? value) => SelectionChanged?.Invoke(value?.Key);
    partial void OnFrozenChanged(bool value) => FreezeChanged?.Invoke(value);
    [RelayCommand]
    private void Clear() => ClearRequested?.Invoke(true);
}
