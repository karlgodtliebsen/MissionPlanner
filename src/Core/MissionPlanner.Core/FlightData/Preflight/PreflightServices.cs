using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Commands;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Builds deterministic, explainable readiness assessments from promoted state.</summary>
public interface IPreflightAssessmentService
{
    /// <summary>Assesses the supplied immutable vehicle state.</summary>
    PreflightAssessment Assess(VehicleState state, DateTimeOffset now);
}

/// <summary>Runs the typed ArduPilot pre-arm diagnostic command.</summary>
public interface IPreflightCommandService
{
    /// <summary>Runs pre-arm checks for a disarmed live vehicle.</summary>
    Task<PreflightCommandResult> RunAsync(VehicleState state, CancellationToken cancellationToken);
}

/// <summary>Projects promoted telemetry into a conservative readiness assessment.</summary>
public sealed class PreflightAssessmentService : IPreflightAssessmentService
{
    private static readonly TimeSpan heartbeatMaximumAge = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan telemetryMaximumAge = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public PreflightAssessment Assess(VehicleState state, DateTimeOffset now)
    {
        var checks = new List<PreflightCheckResult>
        {
            Check("connection", PreflightCheckCategory.Connection, "Connection and heartbeat",
                now - state.Connection.LastHeartbeatAt <= heartbeatMaximumAge ? PreflightCheckStatus.Pass : PreflightCheckStatus.Stale,
                $"Connection is {state.Connection.State}; heartbeat age {(now - state.Connection.LastHeartbeatAt).TotalSeconds:0.0} s.",
                "HEARTBEAT", state.Connection.LastHeartbeatAt, "Restore the vehicle link and wait for a fresh heartbeat."),
            Check("identity", PreflightCheckCategory.Identity, "Firmware identity",
                state.Identity.Firmware.Family == FirmwareFamily.Unknown ? PreflightCheckStatus.NotAvailable : PreflightCheckStatus.Pass,
                state.Identity.Firmware.Family.ToString(), "HEARTBEAT/AUTOPILOT_VERSION", state.Connection.LastHeartbeatAt,
                "Wait for firmware identification before relying on family-specific checks."),
            Check("armed", PreflightCheckCategory.Flight, "Vehicle is disarmed",
                state.IsArmed ? PreflightCheckStatus.Warning : PreflightCheckStatus.Pass,
                state.IsArmed ? "Vehicle reports armed." : "Vehicle reports disarmed.", "HEARTBEAT", state.Connection.LastHeartbeatAt,
                "Disarm before configuration or ground inspection."),
            FreshnessCheck("gps", PreflightCheckCategory.Navigation, "GPS fix", state.Gps.ObservedAt,
                state.Gps.FixType >= GpsFixType.Fix3D && (state.Gps.SatellitesVisible ?? 0) >= 6,
                $"{state.Gps.FixType}; {state.Gps.SatellitesVisible?.ToString() ?? "unknown"} satellites.", "GPS_RAW_INT", now,
                "Move to open sky and verify the GPS installation."),
            FreshnessCheck("battery", PreflightCheckCategory.Power, "Battery", state.Power.ObservedAt,
                (state.Power.BatteryRemainingPercent ?? 0) >= 20 && (state.Power.BatteryVoltageVolts ?? 0) > 0,
                $"{state.Power.BatteryVoltageVolts?.ToString("0.00") ?? "unknown"} V; {state.Power.BatteryRemainingPercent?.ToString() ?? "unknown"}%.",
                "SYS_STATUS/BATTERY_STATUS", now, "Charge or replace the battery and verify the power monitor."),
            FreshnessCheck("system-health", PreflightCheckCategory.Sensors, "Autopilot sensor health", state.Health.SystemObservedAt,
                state.Health.SensorsPresent.HasValue && (state.Health.SensorsEnabled & state.Health.SensorsPresent) == state.Health.SensorsPresent
                    && (state.Health.SensorsHealthy & state.Health.SensorsPresent) == state.Health.SensorsPresent,
                $"Present={state.Health.SensorsPresent?.ToString("X8") ?? "unknown"}, enabled={state.Health.SensorsEnabled?.ToString("X8") ?? "unknown"}, healthy={state.Health.SensorsHealthy?.ToString("X8") ?? "unknown"}.",
                "SYS_STATUS", now, "Resolve reported sensor configuration or health failures."),
            FreshnessCheck("ekf", PreflightCheckCategory.Navigation, "Estimator health", state.Health.ObservedAt,
                state.Health.EkfHealthy == true, state.Health.EkfHealthy?.ToString() ?? "unknown", "EKF_STATUS_REPORT", now,
                "Wait for estimator convergence and inspect EKF diagnostics."),
        };

        AddUnavailable(checks, "home", PreflightCheckCategory.Navigation, "Home position", "No promoted home-position state is currently available.");
        AddUnavailable(checks, "fence", PreflightCheckCategory.Navigation, "Fence state", "No promoted fence-status state is currently available.");
        AddUnavailable(checks, "storage", PreflightCheckCategory.Diagnostics, "Storage and logging", "No cohesive storage-health state is currently promoted.");

        var overall = checks.Select(x => x.Status).OrderByDescending(Severity).First();
        return new PreflightAssessment(state.VehicleId, overall, now, checks);
    }

    private static PreflightCheckResult FreshnessCheck(string key, PreflightCheckCategory category, string title,
        DateTimeOffset? observedAt, bool passes, string value, string source, DateTimeOffset now, string remediation)
    {
        var status = observedAt is null ? PreflightCheckStatus.NotAvailable
            : now - observedAt > telemetryMaximumAge ? PreflightCheckStatus.Stale
            : passes ? PreflightCheckStatus.Pass : PreflightCheckStatus.Fail;
        return Check(key, category, title, status, value, source, observedAt, remediation);
    }

    private static PreflightCheckResult Check(string key, PreflightCheckCategory category, string title,
        PreflightCheckStatus status, string summary, string source, DateTimeOffset? observedAt, string remediation) =>
        new(key, category, title, status, summary, new PreflightEvidence(source, summary, observedAt), remediation, []);

    private static void AddUnavailable(ICollection<PreflightCheckResult> checks, string key, PreflightCheckCategory category, string title, string summary) =>
        checks.Add(Check(key, category, title, PreflightCheckStatus.NotAvailable, summary, "Not promoted", null, "No operator action is inferred from unavailable evidence."));

    private static int Severity(PreflightCheckStatus status) => status switch
    {
        PreflightCheckStatus.Fail => 5,
        PreflightCheckStatus.Stale => 4,
        PreflightCheckStatus.Warning => 3,
        PreflightCheckStatus.NotAvailable => 2,
        _ => 1,
    };
}

/// <summary>Executes pre-arm diagnostics through the acknowledged command workflow.</summary>
public sealed class PreflightCommandService(
    IVehicleCommandService commandService,
    IVehicleMessageStore messageStore,
    IReplaySessionManager? replay = null) : IPreflightCommandService
{
    /// <inheritdoc />
    public async Task<PreflightCommandResult> RunAsync(VehicleState state, CancellationToken cancellationToken)
    {
        if (replay?.Snapshot.State != ReplaySessionState.Unloaded)
            return new(null, [], "Pre-arm checks are unavailable during telemetry replay.");
        if (state.IsArmed)
            return new(null, [], "Disarm the vehicle before running pre-arm checks.");

        var start = DateTimeOffset.UtcNow;
        var response = await commandService.ExecuteExpertAsync(
            new ExpertVehicleCommand(state.VehicleId, MavLinkCommandIds.RunPrearmChecks, [0, 0, 0, 0, 0, 0, 0]), true, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        var diagnostics = messageStore.GetMessages(state.VehicleId)
            .Where(x => x.ReceivedAt >= start)
            .ToArray();
        return new(response, diagnostics, $"Command result: {response.Result}; captured {diagnostics.Length} diagnostic message(s).");
    }
}
