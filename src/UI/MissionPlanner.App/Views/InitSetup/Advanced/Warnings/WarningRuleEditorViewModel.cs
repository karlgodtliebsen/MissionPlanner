using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Warnings;

/// <summary>Owns rule editing, adjacent validation and side-effect-free preview.</summary>
public sealed partial class WarningRuleEditorViewModel(WarningSources sources,
    ILogger<WarningRuleEditorViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events)
    : ViewModelBase(logger, dispatcher, events)
{
    private Guid id = Guid.NewGuid();
    /// <summary>Gets supported telemetry sources in native units.</summary>
    public IReadOnlyList<WarningSourceDescriptor> Sources => sources.All;
    /// <summary>Gets supported comparisons.</summary>
    public IReadOnlyList<WarningComparison> Comparisons { get; } = Enum.GetValues<WarningComparison>();
    /// <summary>Gets supported severities.</summary>
    public IReadOnlyList<WarningSeverity> Severities { get; } = Enum.GetValues<WarningSeverity>();
    /// <summary>Gets or sets the rule name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "New warning";
    /// <summary>Gets or sets whether the rule participates in evaluation.</summary>
    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;
    /// <summary>Gets or sets the numeric source.</summary>
    [ObservableProperty]
    public partial WarningSourceDescriptor? Source { get; set; }
    /// <summary>Gets or sets the comparison.</summary>
    [ObservableProperty]
    public partial WarningComparison Comparison { get; set; }
    /// <summary>Gets or sets the first threshold.</summary>
    [ObservableProperty]
    public partial double Threshold { get; set; }
    /// <summary>Gets or sets the inclusive upper bound.</summary>
    [ObservableProperty]
    public partial double UpperThreshold { get; set; } = 100;
    /// <summary>Gets or sets visual urgency.</summary>
    [ObservableProperty]
    public partial WarningSeverity Severity { get; set; } = WarningSeverity.Warning;
    /// <summary>Gets or sets the notification template.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = "{name}: {value}";
    /// <summary>Gets or sets activation delay.</summary>
    [ObservableProperty]
    public partial double DelaySeconds { get; set; }
    /// <summary>Gets or sets hysteresis in native source units.</summary>
    [ObservableProperty]
    public partial double Hysteresis { get; set; }
    /// <summary>Gets or sets minimum time between notifications.</summary>
    [ObservableProperty]
    public partial double CooldownSeconds { get; set; } = 30;
    /// <summary>Gets or sets whether acknowledgement suppresses repetition.</summary>
    [ObservableProperty]
    public partial bool RequiresAcknowledgement { get; set; }
    /// <summary>Gets or sets a test input independent of live telemetry.</summary>
    [ObservableProperty]
    public partial double PreviewValue { get; set; }
    /// <summary>Gets the preview result.</summary>
    [ObservableProperty]
    public partial string PreviewResult { get; private set; } = string.Empty;
    /// <summary>Gets name validation errors.</summary>
    [ObservableProperty]
    public partial string NameError { get; private set; } = string.Empty;
    /// <summary>Gets source validation errors.</summary>
    [ObservableProperty]
    public partial string SourceError { get; private set; } = string.Empty;
    /// <summary>Gets comparison validation errors.</summary>
    [ObservableProperty]
    public partial string ThresholdError { get; private set; } = string.Empty;
    /// <summary>Gets timing validation errors.</summary>
    [ObservableProperty]
    public partial string TimingError { get; private set; } = string.Empty;
    /// <summary>Gets message validation errors.</summary>
    [ObservableProperty]
    public partial string MessageError { get; private set; } = string.Empty;
    /// <summary>Requests persistence after complete validation.</summary>
    public event Action<WarningRule>? SaveRequested;

    /// <summary>Loads a detached editor copy without modifying live rules.</summary>
    public void Edit(WarningRule? rule)
    {
        rule ??= new(Guid.NewGuid(), "New warning", true, sources.All[0].Key,
            WarningComparison.Less, 0, 100, WarningSeverity.Warning, "{name}: {value}");
        id = rule.Id;
        Name = rule.Name;
        Enabled = rule.Enabled;
        Source = Sources.SingleOrDefault(item => item.Key == rule.Source);
        Comparison = rule.Comparison;
        Threshold = rule.Threshold;
        UpperThreshold = rule.UpperThreshold;
        Severity = rule.Severity;
        Message = rule.Message;
        DelaySeconds = rule.DelaySeconds;
        Hysteresis = rule.Hysteresis;
        CooldownSeconds = rule.CooldownSeconds;
        RequiresAcknowledgement = rule.RequiresAcknowledgement;
        NameError = SourceError = ThresholdError = TimingError = MessageError = PreviewResult = string.Empty;
    }

    private WarningRule Build() => new(id, Name, Enabled, Source?.Key ?? string.Empty, Comparison,
        Threshold, UpperThreshold, Severity, Message, DelaySeconds, Hysteresis, CooldownSeconds, RequiresAcknowledgement);

    private bool Validate(WarningRule rule)
    {
        var errors = sources.Validate(rule);
        string Error(params string[] fields) => string.Join(" ", fields.Where(errors.ContainsKey).Select(field => errors[field]));
        NameError = Error(nameof(Name));
        SourceError = Error(nameof(Source));
        ThresholdError = Error(nameof(Comparison), nameof(Threshold), nameof(UpperThreshold), nameof(Hysteresis));
        TimingError = Error(nameof(DelaySeconds), nameof(CooldownSeconds));
        MessageError = Error(nameof(Message));
        return errors.Count == 0;
    }

    [RelayCommand]
    private void Save()
    {
        var rule = Build();
        if (Validate(rule))
        {
            SaveRequested?.Invoke(rule);
        }
    }

    [RelayCommand]
    private void Preview()
    {
        var rule = Build();
        if (Validate(rule))
        {
            PreviewResult = !double.IsFinite(PreviewValue) ? "Unavailable input" : WarningEngine.Matches(rule, PreviewValue)
                ? "Condition matches (preview only; delay and live state are unchanged)." : "Condition does not match.";
        }
    }
}
