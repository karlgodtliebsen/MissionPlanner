using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents a conservative, explainable preflight readiness assessment.</summary>
public partial class PreflightTabViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IPreflightAssessmentService assessmentService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IPreflightCommandService commandService;
    private readonly IDispatcher dispatcher;
    private readonly IDisposable stateSubscription;
    private readonly CancellationTokenSource lifetime = new();
    private int refreshPending;

    /// <summary>Initializes a transient Preflight tab view model.</summary>
    public PreflightTabViewModel(IActiveVehicleContext activeVehicle, IPreflightAssessmentService assessmentService, IDateTimeProvider dateTimeProvider,
        IPreflightCommandService commandService, IDomainEventHub eventHub, IDispatcher dispatcher)
    {
        this.activeVehicle = activeVehicle;
        this.assessmentService = assessmentService;
        this.dateTimeProvider = dateTimeProvider;
        this.commandService = commandService;
        this.dispatcher = dispatcher;
        activeVehicle.Changed += OnActiveVehicleChanged;
        stateSubscription = eventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        //  Refresh();
    }

    /// <summary>Gets the current readiness checks.</summary>
    public ObservableRangeCollection<PreflightCheckResult> Checks { get; } = [];

    /// <summary>Gets the assessment banner.</summary>
    [ObservableProperty]
    public partial string OverallStatus { get; private set; } = "No active vehicle";

    /// <summary>Gets the safety disclaimer.</summary>
    public string Disclaimer => "Operator assistance only — this assessment does not declare an aircraft safe to fly.";

    /// <summary>Gets the latest assessment time.</summary>
    [ObservableProperty]
    public partial string LastUpdated { get; private set; } = "Not assessed";

    /// <summary>Gets the latest pre-arm command result.</summary>
    [ObservableProperty]
    public partial string CommandResult { get; private set; } = "Pre-arm checks have not been requested.";

    /// <summary>Gets a copyable plain-text report.</summary>
    [ObservableProperty]
    public partial string Report { get; private set; } = string.Empty;

    /// <summary>Gets whether the active vehicle permits a pre-arm request.</summary>
    [ObservableProperty]
    public partial bool CanRunPrearm { get; private set; }

    [RelayCommand]
    private void Refresh()
    {
        ApplyAssessment();
    }

    [RelayCommand]
    private async Task RunPrearmAsync(CancellationToken cancellationToken)
    {
        var state = activeVehicle.State;
        if (state is null)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, activeVehicle.ConnectionCancellationToken);
        CommandResult = "Running pre-arm checks…";
        try
        {
            var result = await commandService.RunAsync(state, linked.Token);
            CommandResult = result.Diagnostics.Count == 0
                ? result.Summary
                : $"{result.Summary} {string.Join(" | ", result.Diagnostics.Select(x => x.Text))}";
            ApplyAssessment();
        }
        catch (OperationCanceledException) { CommandResult = "Pre-arm checks cancelled."; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        stateSubscription.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private void OnActiveVehicleChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(ApplyAssessment);
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && Interlocked.Exchange(ref refreshPending, 1) == 0)
        {
            _ = PublishLaterAsync(lifetime.Token);
        }

        return Task.CompletedTask;
    }

    private async Task PublishLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken);
            dispatcher.Dispatch(ApplyAssessment);
        }
        catch (OperationCanceledException) { }
        finally { Interlocked.Exchange(ref refreshPending, 0); }
    }

    private void ApplyAssessment()
    {
        var state = activeVehicle.State;
        CanRunPrearm = activeVehicle.IsOnline && state?.IsArmed == false;
        if (state is null)
        {
            OverallStatus = "NOT AVAILABLE — no active vehicle";
            LastUpdated = "Not assessed";
            Report = OverallStatus;
            return;
        }

        var assessment = assessmentService.Assess(state, dateTimeProvider.UtcNow);
        //List<PreflightCheckResult> checkSet = [];
        //foreach (var check in assessment.Checks)
        //{
        //    if (Checks.Contains(check))
        //    {
        //        continue;
        //    }

        //    checkSet.Add(check);
        //}
        //Checks.AddRange(checkSet);

        Checks.Clear();
        Checks.AddRange(assessment.Checks);

        OverallStatus = $"{assessment.OverallStatus.ToString().ToUpperInvariant()} — review every unavailable or actionable check";
        LastUpdated = assessment.AssessedAt.ToLocalTime().ToString("G");
        var report = new StringBuilder().AppendLine(Disclaimer).AppendLine(OverallStatus).AppendLine($"Assessed: {assessment.AssessedAt:O}");
        foreach (var check in assessment.Checks)
        {
            report.AppendLine($"[{check.Status}] {check.Category} / {check.Title}: {check.Summary} Source: {check.Evidence.Source}. Remediation: {check.Remediation}");
        }

        Report = report.ToString();
    }
}
