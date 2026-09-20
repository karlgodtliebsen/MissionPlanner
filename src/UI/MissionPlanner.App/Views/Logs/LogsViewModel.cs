using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Hosts existing telemetry tools and application diagnostics using DI-resolved views.</summary>
public sealed partial class LogsViewModel : ViewModelBase
{
    private readonly Func<int, Control> createView;
    private readonly LogsNavigationState state;
    private Control? telemetry;
    private Control? application;

    /// <summary>Initializes the root Logs host and restores its selected section.</summary>
    public LogsViewModel(Func<int, Control> createView, LogsNavigationState state,
        Utilities.Dispatching.IUiDispatcher dispatcher, Library.EventHub.Abstractions.IDomainEventHub events, ILogger<LogsViewModel> logger) : base(logger, dispatcher, events)
    {
        this.createView = createView;
        this.state = state;
        selectedSection = state.SelectedSection;
        SelectContent();
    }

    /// <summary>Gets or sets the selected section index.</summary>
    [ObservableProperty]
    private int selectedSection;

    /// <summary>Gets the selected child view; only this view is attached to the visual tree.</summary>
    [ObservableProperty]
    private Control? content;

    partial void OnSelectedSectionChanged(int value)
    {
        state.SelectedSection = value == 1 ? 1 : 0;
        SelectContent();
    }

    private void SelectContent()
    {
        Content = SelectedSection == 1
            ? application ??= createView(1)
            : telemetry ??= createView(0);
    }
}
