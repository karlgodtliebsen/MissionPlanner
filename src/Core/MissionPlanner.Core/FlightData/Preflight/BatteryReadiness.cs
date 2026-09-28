using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>One shared, read-only battery assessment; percentage is never a readiness criterion.</summary>
public static class BatteryReadiness
{
    /// <summary>Assesses an exact one-based battery instance using received settings and session evidence.</summary>
    public static PreflightCheckResult Assess(int number, VehicleBatteryState? sample, Func<string, float?> parameter,
        IReadOnlyList<BatteryArmingEvidence> evidence, bool online, DateTimeOffset now)
    {
        var prefix = number == 1 ? "BATT_" : $"BATT{number}_";
        var monitor = parameter(prefix + "MONITOR");
        var minimum = parameter(prefix + "ARM_VOLT");
        var low = parameter(prefix + "LOW_VOLT");
        var critical = parameter(prefix + "CRT_VOLT");
        var reason = evidence.Where(item => item.BatteryNumber == number).OrderByDescending(item => item.ObservedAt).FirstOrDefault();
        var voltage = sample?.VoltageVolts;
        var valid = voltage is { } volts && double.IsFinite(volts) && volts >= 0;
        var fresh = online && sample is not null && now >= sample.ObservedAt && now - sample.ObservedAt <= TimeSpan.FromSeconds(10);
        var values = $"Voltage: {(valid ? $"{voltage:0.00} V" : "unavailable/invalid")}; reported percentage: {(sample?.RemainingPercent is >= 0 and <= 100 ? $"{sample.RemainingPercent}% (not verified state of charge)" : "unknown")}.";
        PreflightCheckStatus status;
        string explanation;
        if (reason is { ResolvedAt: null })
        {
            var current = online && now >= reason.ObservedAt && now - reason.ObservedAt <= TimeSpan.FromSeconds(30);
            status = current ? PreflightCheckStatus.Fail : PreflightCheckStatus.NotAvailable;
            explanation = current ? $"FC blocker: {reason.Message}. Telemetry alone cannot override this blocker."
                : $"Historical FC blocker, recovery unconfirmed: {reason.Message}. Silence is not recovery.";
        }
        else if (monitor == 0)
        {
            status = PreflightCheckStatus.NotApplicable;
            explanation = "Battery monitoring is explicitly disabled; readiness is not assessed.";
        }
        else if (monitor is null || !float.IsFinite(monitor.Value) || monitor < 0)
        {
            status = PreflightCheckStatus.NotAvailable;
            explanation = "Monitor configuration has not been established.";
        }
        else if (!valid || sample is null)
        {
            status = PreflightCheckStatus.NotAvailable;
            explanation = "A valid voltage measurement is required.";
        }
        else if (!fresh)
        {
            status = PreflightCheckStatus.Stale;
            explanation = "Battery evidence is stale or the vehicle is disconnected.";
        }
        else if (critical is > 0 && float.IsFinite(critical.Value) && voltage < critical ||
                 minimum is > 0 && float.IsFinite(minimum.Value) && voltage < minimum)
        {
            status = PreflightCheckStatus.Fail;
            explanation = "Measured voltage is below a configured critical or arming minimum.";
        }
        else if (low is > 0 && float.IsFinite(low.Value) && voltage < low)
        {
            status = PreflightCheckStatus.Warning;
            explanation = "Measured voltage is below the configured low-voltage threshold.";
        }
        else if (minimum is not > 0 || !float.IsFinite(minimum.Value))
        {
            status = PreflightCheckStatus.NotAvailable;
            explanation = "No positive arming-voltage minimum is available; a percentage cannot establish readiness.";
        }
        else
        {
            status = PreflightCheckStatus.Pass;
            explanation = "NextGen: measured voltage meets the configured thresholds. This is not FC arming approval.";
        }
        string Threshold(float? value) => value is { } v && float.IsFinite(v) ? $"{v:0.00} V{(v == 0 ? " (disabled)" : "")}" : "unknown";
        var summary = $"{explanation}\n{values}\nArming minimum: {Threshold(minimum)}; low: {Threshold(low)}; critical: {Threshold(critical)}.";
        if (reason is not null)
        {
            summary += $"\nFC evidence received at {reason.ObservedAt:O}.";
        }
        if (reason?.ResolvedAt is { } recovered)
        {
            summary += $"\nPrevious FC blocker resolved by subsequent FC readiness/armed evidence at {recovered:O}.";
        }
        return new(number == 1 ? "battery" : $"battery-{number}", PreflightCheckCategory.Power, $"Battery {number}", status,
            summary, new("Battery telemetry + received parameters + FC evidence", summary, sample?.ObservedAt),
            "Inspect battery monitoring, wiring and voltage settings; verify the physical battery independently.",
            [prefix + "MONITOR", prefix + "ARM_VOLT", prefix + "LOW_VOLT", prefix + "CRT_VOLT"]);
    }
}
