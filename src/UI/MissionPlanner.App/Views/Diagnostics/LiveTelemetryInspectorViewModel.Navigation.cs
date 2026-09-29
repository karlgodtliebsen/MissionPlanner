using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.Diagnostics;

public sealed partial class LiveTelemetryInspectorViewModel
{
    private readonly IPreflightAssessmentService? assessmentService;
    private readonly INavigationService? navigation;
    private readonly IVehicleMessageStore? messageStore;
    private readonly MessagesTabViewModel? messages;
    private readonly StatusTabViewModel? status;
    private readonly IReplaySessionManager? replay;
    private readonly Dictionary<VehicleId, long> readThrough = [];
    private bool followingActive;
    private DateTimeOffset summaryUpdatedAt;
    private VehicleId? readinessVehicle;
    private DateTimeOffset? readinessSession;

    /// <summary>The shared side-panel destinations.</summary>
    public IReadOnlyList<string> Destinations { get; } = ["Readiness", "Messages", "Inspector"];
    /// <summary>Current destination; controls remain mounted to retain scroll and filters.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReadiness), nameof(ShowMessages), nameof(ShowInspector))]
    public partial string Destination { get; set; } = "Inspector";
    /// <summary>Whether the assessment view is selected.</summary>
    public bool ShowReadiness => Destination == "Readiness";
    /// <summary>Whether the message view is selected.</summary>
    public bool ShowMessages => Destination == "Messages";
    /// <summary>Whether the technical view is selected.</summary>
    public bool ShowInspector => Destination == "Inspector";
    /// <summary>Whether the detailed decoded/promoted telemetry table is selected.</summary>
    public bool ShowStatus => SelectedPanel == "Decoded";
    /// <summary>Current blockers for the vehicle the diagnostic entry point opens; unknown checks are not failures.</summary>
    [ObservableProperty]
    public partial int BlockerCount { get; private set; }
    /// <summary>Retained unread warning/error messages for the inspected or active vehicle.</summary>
    [ObservableProperty]
    public partial int UnreadWarnings { get; private set; }
    /// <summary>Persistent active-vehicle readiness summary.</summary>
    [ObservableProperty]
    public partial string ActiveReadinessSummary { get; private set; } = "Readiness unknown — no active vehicle";
    /// <summary>Compact inspected mode and FC arming state.</summary>
    [ObservableProperty]
    public partial string FlightSummary { get; private set; } = "Mode / readiness unknown";
    /// <summary>Explicit active/historical selection status.</summary>
    [ObservableProperty]
    public partial string SelectionSummary { get; private set; } = "No vehicle selected";
    /// <summary>Expanded technical identity and timestamps.</summary>
    [ObservableProperty]
    public partial string TechnicalDetails { get; private set; } = "No evidence";
    /// <summary>Selected readiness explanation.</summary>
    [ObservableProperty]
    public partial string ReadinessSummary { get; private set; } = "Unknown — no telemetry";
    /// <summary>Persistent feedback when a requested navigation cannot be completed.</summary>
    [ObservableProperty]
    public partial string NavigationMessage { get; private set; } = string.Empty;
    /// <summary>Non-passing checks, with failures first.</summary>
    public ObservableCollection<ReadinessCheckItem> ReadinessChecks { get; } = [];
    /// <summary>Passed checks, collapsed by default.</summary>
    public ObservableCollection<ReadinessCheckItem> PassedChecks { get; } = [];

    private void FollowSelection()
    {
        followingActive = true;
        try
        {
            SelectedVehicle = activeVehicle.VehicleId;
        }
        finally
        {
            followingActive = false;
        }
    }

    partial void OnIsVehiclePinnedChanged(bool value)
    {
        summaryUpdatedAt = default;
        RefreshActiveSummary();
    }

    private bool HasEvidenceSession(VehicleId vehicle, DateTimeOffset session)
    {
        if (diagnostics.GetSnapshot(vehicle).SessionStartedAt == session)
        {
            return true;
        }
        NavigationMessage = "This evidence belongs to an earlier connection session. Review the refreshed readiness checks; navigation was not retargeted to the new session.";
        return false;
    }

    [RelayCommand]
    private void FollowActiveVehicle()
    {
        IsVehiclePinned = false;
        FollowSelection();
        Refresh();
    }

    [RelayCommand]
    private void OpenDestination(string? destination)
    {
        Destination = Destinations.Contains(destination) ? destination! : "Inspector";
        Open();
    }

    [RelayCommand]
    private void OpenActiveReadiness()
    {
        FollowActiveVehicle();
        OpenDestination("Readiness");
    }

    [RelayCommand]
    private Task OpenLogs() => navigation?.NavigateAsync(MissionPlannerRoutes.Logs) ?? Task.CompletedTask;

    [RelayCommand]
    private void MarkMessagesRead()
    {
        if (SelectedVehicle is { } id && messageStore is not null)
        {
            readThrough[id] = messageStore.GetMessages(id).Select(item => item.Identity).DefaultIfEmpty().Max();
            summaryUpdatedAt = default;
            RefreshActiveSummary();
        }
    }

    /// <summary>Opens evidence without changing the operational active vehicle.</summary>
    public void OpenEvidence(VehicleId vehicle, string key)
    {
        IsVehiclePinned = vehicle != activeVehicle.VehicleId || IsVehiclePinned;
        followingActive = true;
        try
        {
            SelectedVehicle = vehicle;
        }
        finally
        {
            followingActive = false;
        }
        Destination = key == "fc-arming" ? "Messages" : "Inspector";
        SelectedPanel = key.StartsWith("battery", StringComparison.Ordinal) ? "Power" : "Status";
        IsOpen = true;
        Refresh();
    }

    private async void Configure(VehicleId vehicle, string key)
    {
        if (vehicle != activeVehicle.VehicleId)
        {
            NavigationMessage = "Configuration belongs to the active vehicle. Select the intended vehicle explicitly before configuring; inspection did not change it.";
            return;
        }
        if (navigation is not null)
        {
            try
            {
                await navigation.NavigateAsync(key.StartsWith("battery", StringComparison.Ordinal)
                    ? MissionPlannerRoutes.SetupOptionalHardware + "#BatteryMonitors"
                    : key == "fc-arming" ? MissionPlannerRoutes.SetupArming : MissionPlannerRoutes.ConfigurationParametersEditor);
                Close();
            }
            catch (Exception exception)
            {
                NavigationMessage = $"Could not open configuration: {exception.Message}";
            }
        }
    }

    private void RefreshActiveSummary()
    {
        if (presentationDisposed || clock.GetUtcNow() - summaryUpdatedAt < TimeSpan.FromSeconds(1))
        {
            return;
        }
        summaryUpdatedAt = clock.GetUtcNow();
        var state = activeVehicle.State;
        var assessment = state is null ? null : assessmentService?.Assess(state, summaryUpdatedAt);
        var activeBlockers = assessment?.Checks.Count(check => check.Status == PreflightCheckStatus.Fail) ?? 0;
        ActiveReadinessSummary = assessment is null ? "Readiness unknown — no active vehicle" :
            $"{state!.VehicleId} · {activeBlockers} blockers · {ReadinessCheckItem.Label(assessment.OverallStatus)}";
        var id = IsVehiclePinned ? SelectedVehicle : activeVehicle.VehicleId;
        var inspectedState = id == activeVehicle.VehicleId ? state :
            id is { } inspected ? diagnostics.GetSnapshot(inspected)?.State : null;
        var inspectedAssessment = ReferenceEquals(inspectedState, state) ? assessment :
            inspectedState is null ? null : assessmentService?.Assess(inspectedState, summaryUpdatedAt);
        BlockerCount = inspectedAssessment?.Checks.Count(check => check.Status == PreflightCheckStatus.Fail) ?? 0;
        UnreadWarnings = id is { } vehicle && messageStore is not null
            ? messageStore.GetMessages(vehicle).Count(item => (int)item.Severity <= 4 && item.Identity > readThrough.GetValueOrDefault(vehicle)) : 0;
    }

    private void RefreshSharedViews(VehicleLiveDiagnosticSnapshot snapshot, VehicleArmingDiagnostic arming)
    {
        var state = snapshot.State;
        FlightSummary = $"{state?.Flight.Mode.ToString() ?? "Mode unknown"} · {arming.Summary}";
        SelectionSummary = snapshot.VehicleId == activeVehicle.VehicleId ? "Inspecting active vehicle" :
            $"PINNED / HISTORICAL: {snapshot.VehicleId} · Active: {activeVehicle.State?.DisplayName ?? "Vehicle"} ({activeVehicle.VehicleId?.ToString() ?? "none"})";
        if (snapshot.Disconnected || state?.Connection.State == VehicleConnectionState.Offline)
        {
            SelectionSummary += " · DISCONNECTED";
        }
        else if (state is null || clock.GetUtcNow() - state.LastHeartbeatAt > TimeSpan.FromSeconds(5))
        {
            SelectionSummary += " · STALE / UNKNOWN";
        }
        if (replay?.Snapshot.IsTransmissionProhibited == true)
        {
            SelectionSummary += " · REPLAY / read only";
        }
        var firmware = state?.Identity.Firmware;
        var version = firmware?.FlightVersion;
        TechnicalDetails = $"Firmware: {firmware?.Family.ToString() ?? "unknown"} " +
            (version is null ? "version unknown" : $"{version.Major}.{version.Minor}.{version.Patch} {version.ReleaseType}") +
            $"\nTransport: {snapshot.Transport ?? "unknown"}\n" +
            $"Last data: {snapshot.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff zzz}\nUTC: {snapshot.UpdatedAt:O}\nHeartbeat: {state?.LastHeartbeatAt:O}";
        if (IsFrozen)
        {
            return;
        }
        status?.Inspect(snapshot.State);
        if (state is null || assessmentService is null)
        {
            ReadinessChecks.Clear();
            PassedChecks.Clear();
            ReadinessSummary = "Unknown — no retained telemetry for this vehicle";
            return;
        }
        var assessment = assessmentService.Assess(state, clock.GetUtcNow());
        ReadinessSummary = $"NextGen assessment: {ReadinessCheckItem.Label(assessment.OverallStatus)}. FC readiness is shown separately. Operator assistance, not flight approval.";
        if (readinessVehicle != snapshot.VehicleId || readinessSession != snapshot.SessionStartedAt)
        {
            ReadinessChecks.Clear();
            PassedChecks.Clear();
            readinessVehicle = snapshot.VehicleId;
            readinessSession = snapshot.SessionStartedAt;
        }
        UpdateChecks(ReadinessChecks, assessment.Checks.Where(item => item.Status != PreflightCheckStatus.Pass)
            .OrderBy(item => item.Status == PreflightCheckStatus.Fail ? 0 : item.Status == PreflightCheckStatus.Warning ? 1 : 2).ToArray(), snapshot.VehicleId, snapshot.SessionStartedAt);
        UpdateChecks(PassedChecks, assessment.Checks.Where(item => item.Status == PreflightCheckStatus.Pass).ToArray(), snapshot.VehicleId, snapshot.SessionStartedAt);
    }

    private void UpdateChecks(ObservableCollection<ReadinessCheckItem> target, IReadOnlyList<PreflightCheckResult> checks, VehicleId vehicle, DateTimeOffset session)
    {
        foreach (var removed in target.Where(item => checks.All(check => check.Key != item.Key)).ToArray())
        {
            target.Remove(removed);
        }
        for (var index = 0; index < checks.Count; index++)
        {
            var check = checks[index];
            var item = target.FirstOrDefault(row => row.Key == check.Key);
            if (item is null)
            {
                target.Insert(index, new(check, vehicle, (id, key) =>
                {
                    if (HasEvidenceSession(id, session))
                    {
                        OpenEvidence(id, key);
                    }
                }, (id, key) =>
                {
                    if (HasEvidenceSession(id, session))
                    {
                        Configure(id, key);
                    }
                }));
            }
            else
            {
                item.Update(check);
                var oldIndex = target.IndexOf(item);
                if (oldIndex != index)
                {
                    target.Move(oldIndex, index);
                }
            }
        }
    }
}

