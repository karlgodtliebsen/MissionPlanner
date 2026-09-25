using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Hosts logging sections using typed DI activation.</summary>
public sealed partial class LogsViewModel : ViewModelBase
{
    private readonly ILogsViewFactory views;
    private readonly LogsNavigationState state;
    private readonly Dictionary<LogsSection, Control> children = [];

    /// <summary>Initializes the Logs host and restores its selected section.</summary>
    public LogsViewModel(ILogsViewFactory views, LogsNavigationState state,
        Utilities.Dispatching.IUiDispatcher dispatcher, Library.EventHub.Abstractions.IDomainEventHub events,
        ILogger<LogsViewModel> logger) : base(logger, dispatcher, events)
    {
        //this.views = views;
        //this.state = state;
        //selectedSection = state.SelectedSection;
        //SelectContent();
    }

    ///// <summary>Available sections in display order.</summary>
    //public IReadOnlyList<LogsSection> Sections { get; } = [LogsSection.Telemetry, LogsSection.Application];

    ///// <summary>Gets or sets the selected section.</summary>
    //[ObservableProperty]
    //private LogsSection selectedSection;

    ///// <summary>Gets the selected child view.</summary>
    //[ObservableProperty]
    //private Control? content;

    //partial void OnSelectedSectionChanged(LogsSection value)
    //{
    //    SelectContent();
    //    state.SelectedSection = value;
    //}

    //private void SelectContent()
    //{
    //    if (!children.TryGetValue(SelectedSection, out var view))
    //    {
    //        view = views.Create(SelectedSection);
    //        children.Add(SelectedSection, view);
    //    }
    //    Content = view;
    //}
}
