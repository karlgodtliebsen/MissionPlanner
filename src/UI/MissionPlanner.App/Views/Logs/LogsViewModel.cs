using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.FlightData.Tabs;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Remembers the chosen logs section for this application session.</summary>
public sealed class LogsNavigationState
{
    /// <summary>Telemetry is zero; application diagnostics is one.</summary>
    public int SelectedSection { get; set; }
}

/// <summary>Hosts existing telemetry tools and application diagnostics using DI-resolved views.</summary>
public sealed partial class LogsViewModel : ViewModelBase
{
    private readonly Func<int, Control> createView;
    private readonly LogsNavigationState state;
    private Control? telemetry;
    private Control? application;

    /// <summary>Initializes the root Logs host and restores its selected section.</summary>
    public LogsViewModel(Func<int, Control> createView, LogsNavigationState state, ILogger<LogsViewModel> logger,
        MissionPlanner.App.Utilities.Dispatching.IUiDispatcher dispatcher,
        MissionPlanner.Library.EventHub.Abstractions.IDomainEventHub events) : base(logger, dispatcher, events)
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
