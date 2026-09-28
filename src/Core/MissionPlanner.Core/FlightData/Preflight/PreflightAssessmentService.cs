using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Projects promoted telemetry into a conservative readiness assessment.</summary>
public sealed class PreflightAssessmentService(IVehicleParameterRegistry? parameters = null, IVehicleLiveDiagnostics? diagnostics = null) : IPreflightAssessmentService
{
    private static readonly TimeSpan heartbeatMaximumAge = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan telemetryMaximumAge = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public PreflightAssessment Assess(VehicleState state, DateTimeOffset now)
    {
        var checks = new List<PreflightCheckResult>
        {
            Check("connection", PreflightCheckCategory.Connection, "Connection and heartbeat",
                state.Connection.State == VehicleConnectionState.Online && now >= state.LastHeartbeatAt && now - state.LastHeartbeatAt <= heartbeatMaximumAge ? PreflightCheckStatus.Pass : PreflightCheckStatus.Stale,
                $"Connection is {state.Connection.State}; heartbeat age {(now - state.Connection.LastHeartbeatAt).TotalSeconds:0.0} s.",
                "HEARTBEAT", state.Connection.LastHeartbeatAt, "Restore the vehicle link and wait for a fresh heartbeat."),
            Check("identity", PreflightCheckCategory.Identity, "Firmware identity",
                state.Identity.Firmware.Family == FirmwareFamily.Unknown ? PreflightCheckStatus.NotAvailable : PreflightCheckStatus.Pass,
                state.Identity.Firmware.Family.ToString(), "HEARTBEAT/AUTOPILOT_VERSION", state.Connection.LastHeartbeatAt,
                "Wait for firmware identification before relying on family-specific checks."),
            Check("armed", PreflightCheckCategory.Flight, "Vehicle is disarmed",
                now - state.LastHeartbeatAt > heartbeatMaximumAge || state.Connection.State != VehicleConnectionState.Online ? PreflightCheckStatus.Stale : state.IsArmed ? PreflightCheckStatus.Warning : PreflightCheckStatus.Pass,
                state.IsArmed ? "Vehicle reports armed." : "Vehicle reports disarmed.", "HEARTBEAT", state.Connection.LastHeartbeatAt,
                "Disarm before configuration or ground inspection."),
            FreshnessCheck("gps", PreflightCheckCategory.Navigation, "GPS fix", state.Gps.ObservedAt,
                state.Gps.FixType >= GpsFixType.Fix3D,
                $"{state.Gps.FixType}; {state.Gps.SatellitesVisible?.ToString() ?? "unknown"} satellites.", "GPS_RAW_INT", now,
                "Move to open sky and verify the GPS installation."),
            FreshnessCheck("system-health", PreflightCheckCategory.Sensors, "Autopilot sensor health", state.Health.SystemObservedAt,
                state.Health.SensorsPresent is { } present && state.Health.SensorsEnabled is { } enabled && state.Health.SensorsHealthy is { } healthy
                    && (enabled & present) != 0 ? (healthy & enabled & present) == (enabled & present) : null,
                $"Present={state.Health.SensorsPresent?.ToString("X8") ?? "unknown"}, enabled={state.Health.SensorsEnabled?.ToString("X8") ?? "unknown"}, healthy={state.Health.SensorsHealthy?.ToString("X8") ?? "unknown"}.",
                "SYS_STATUS", now, "Resolve reported sensor configuration or health failures."),
            FreshnessCheck("ekf", PreflightCheckCategory.Navigation, "Estimator health", state.Health.ObservedAt,
                state.Health.EkfHealthy, state.Health.EkfHealthy?.ToString() ?? "unknown", "EKF_STATUS_REPORT", now,
                "Wait for estimator convergence and inspect EKF diagnostics.")
        };

        var online = state.Connection.State == VehicleConnectionState.Online && now >= state.LastHeartbeatAt && now - state.LastHeartbeatAt <= heartbeatMaximumAge &&
            diagnostics?.GetSnapshot(state.VehicleId)?.Disconnected != true;
        var batteryEvidence = diagnostics?.GetBatteryArmingEvidence(state.VehicleId) ?? [];
        float? Parameter(string name) => parameters?.GetParameter(state.VehicleId, name)?.Value;
        var numbers = state.Power.Batteries.Keys.Select(id => (int)id + 1).Append(1)
            .Concat(Enumerable.Range(2, 15).Where(number => Parameter($"BATT{number}_MONITOR") is not null))
            .Concat(batteryEvidence.Where(item => item.BatteryNumber.HasValue).Select(item => item.BatteryNumber!.Value)).Distinct().Order().ToArray();
        foreach (var number in numbers)
        {
            state.Power.Batteries.TryGetValue((byte)(number - 1), out var sample);
            if (number == 1 && !state.Power.HasSpecificPrimaryBattery && numbers.Any(other => other != 1 && Parameter($"BATT{other}_MONITOR") is > 0))
            {
                // SYS_STATUS cannot establish the battery instance in a multi-monitor installation.
                sample = null;
            }
            checks.Add(BatteryReadiness.Assess(number, sample, Parameter, batteryEvidence, online, now));
        }
        foreach (var reason in batteryEvidence.Where(item => item.BatteryNumber is null && item.ResolvedAt is null))
        {
            checks.Add(Check("battery-unassigned", PreflightCheckCategory.Power, "FC battery blocker (instance unspecified)",
                online && now >= reason.ObservedAt && now - reason.ObservedAt <= TimeSpan.FromSeconds(30) ? PreflightCheckStatus.Fail : PreflightCheckStatus.NotAvailable,
                reason.Message + " — battery instance cannot be inferred; recovery requires FC evidence.", "FC STATUSTEXT", reason.ObservedAt, "Inspect FC messages."));
        }
        var arming = diagnostics?.GetArming(state.VehicleId);
        checks.Add(Check("fc-arming", PreflightCheckCategory.Flight, "FC-reported arming readiness",
            !online ? PreflightCheckStatus.Stale : arming?.IsReadyToArm == true ? PreflightCheckStatus.Pass :
            arming?.IsReadyToArm == false || arming?.Reasons.Count > 0 ? PreflightCheckStatus.Fail : PreflightCheckStatus.NotAvailable,
            arming is null ? "FC readiness unknown" : arming.Summary + "\n" + string.Join("\n", arming.Reasons),
            "FC health / pre-arm evidence", state.Health.SystemObservedAt, "Review FC messages; NextGen checks are separate assessments."));

        // GPS is not a universal prerequisite for manual Copter flight. Other profiles stay unknown unless established.
        var gpsIndex = checks.FindIndex(check => check.Key == "gps");
        if (state.Identity.Firmware.Family == FirmwareFamily.ArduCopter && state.CustomMode is 0 or 1 or 2)
        {
            checks[gpsIndex] = checks[gpsIndex] with { Status = PreflightCheckStatus.NotApplicable,
                Summary = "This Copter mode does not require a GPS position fix. " + checks[gpsIndex].Summary,
                Remediation = "Other FC arming checks still apply; this local result does not establish flight readiness." };
        }
        else if (state.Identity.Firmware.Family != FirmwareFamily.ArduCopter || state.CustomMode is not (3 or 4 or 5 or 6 or 7 or 16 or 17 or 21))
        {
            checks[gpsIndex] = checks[gpsIndex] with { Status = PreflightCheckStatus.NotAvailable,
                Summary = "GPS requirement for this vehicle/mode is not established. " + checks[gpsIndex].Summary };
        }
        else
        {
            checks[gpsIndex] = checks[gpsIndex] with
            {
                Status = checks[gpsIndex].Status == PreflightCheckStatus.Fail ? PreflightCheckStatus.Warning : checks[gpsIndex].Status,
                Summary = "This mode requires a position solution. GPS evidence is advisory: alternative position sources are not assessed. " + checks[gpsIndex].Summary
            };
        }
        if (!online)
        {
            for (var index = 0; index < checks.Count; index++)
            {
                if (checks[index].Status == PreflightCheckStatus.Pass && checks[index].Key != "identity")
                {
                    checks[index] = checks[index] with { Status = PreflightCheckStatus.Stale };
                }
            }
        }

        AddUnavailable(checks, "home", PreflightCheckCategory.Navigation, "Home position", "No promoted home-position state is currently available.");
        AddUnavailable(checks, "fence", PreflightCheckCategory.Navigation, "Fence state", "No promoted fence-status state is currently available.");
        AddUnavailable(checks, "storage", PreflightCheckCategory.Diagnostics, "Storage and logging", "No cohesive storage-health state is currently promoted.");

        var overall = checks.Select(x => x.Status).OrderByDescending(Severity).First();
        return new PreflightAssessment(state.VehicleId, overall, now, checks);
    }

    private static PreflightCheckResult FreshnessCheck(string key, PreflightCheckCategory category, string title,
        DateTimeOffset? observedAt, bool? passes, string value, string source, DateTimeOffset now, string remediation)
    {
        var status = observedAt is null || passes is null ? PreflightCheckStatus.NotAvailable
            : now < observedAt || now - observedAt > telemetryMaximumAge ? PreflightCheckStatus.Stale
            : passes == true ? PreflightCheckStatus.Pass : PreflightCheckStatus.Fail;
        return Check(key, category, title, status, value, source, observedAt, remediation);
    }

    private static PreflightCheckResult Check(string key, PreflightCheckCategory category, string title,
        PreflightCheckStatus status, string summary, string source, DateTimeOffset? observedAt, string remediation)
    {
        return new PreflightCheckResult(key, category, title, status, summary, new PreflightEvidence(source, summary, observedAt), remediation, []);
    }

    private static void AddUnavailable(ICollection<PreflightCheckResult> checks, string key, PreflightCheckCategory category, string title, string summary)
    {
        checks.Add(Check(key, category, title, PreflightCheckStatus.NotAvailable, summary, "Not promoted", null, "No operator action is inferred from unavailable evidence."));
    }

    private static int Severity(PreflightCheckStatus status)
    {
        return status switch
        {
            PreflightCheckStatus.Fail => 5,
            PreflightCheckStatus.Stale => 4,
            PreflightCheckStatus.Warning => 3,
            PreflightCheckStatus.NotAvailable => 2,
            var _ => 1
        };
    }
}
