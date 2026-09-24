using Avalonia;
using Avalonia.Controls;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.Setup.Reporting;

namespace MissionPlanner.App.Controls;

/// <summary>Displays a subsystem's reporting documents with an independent visible-tab lifecycle.</summary>
public partial class SetupInformationView : UserControlViewBase<SetupInformationViewModel>
{
    /// <summary>Identifies the subsystem to report.</summary>
    public static readonly StyledProperty<SetupReportTopic> TopicProperty =
        AvaloniaProperty.Register<SetupInformationView, SetupReportTopic>(nameof(Topic));

    /// <summary>Provides the parent setup workflow's latest operation message.</summary>
    public static readonly StyledProperty<string?> WorkflowStatusProperty =
        AvaloniaProperty.Register<SetupInformationView, string?>(nameof(WorkflowStatus));

    /// <summary>Provides the parent setup workflow's latest error.</summary>
    public static readonly StyledProperty<string?> WorkflowErrorProperty =
        AvaloniaProperty.Register<SetupInformationView, string?>(nameof(WorkflowError));

    /// <summary>Initializes the report content using the standard view composition boundary.</summary>
    public SetupInformationView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            ReportContent.DataContext = ViewModel;
            // The outer control inherits the page context for its workflow-message bindings.
            ClearValue(DataContextProperty);
        }
    }

    /// <summary>Gets or sets the subsystem being reported.</summary>
    public SetupReportTopic Topic
    {
        get => GetValue(TopicProperty);
        set => SetValue(TopicProperty, value);
    }

    /// <summary>Gets or sets the latest setup operation message.</summary>
    public string? WorkflowStatus
    {
        get => GetValue(WorkflowStatusProperty);
        set => SetValue(WorkflowStatusProperty, value);
    }

    /// <summary>Gets or sets the latest setup operation error.</summary>
    public string? WorkflowError
    {
        get => GetValue(WorkflowErrorProperty);
        set => SetValue(WorkflowErrorProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!Design.IsDesignMode && ViewModel is not null &&
            (change.Property == TopicProperty || change.Property == WorkflowStatusProperty || change.Property == WorkflowErrorProperty))
        {
            ViewModel.Topic = Topic;
            ViewModel.WorkflowStatus = WorkflowStatus;
            ViewModel.WorkflowError = WorkflowError;
            ViewModel.Refresh();
        }
    }
}
