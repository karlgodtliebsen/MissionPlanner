using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Typed, vehicle-bound presentation of a domain readiness result.</summary>
public sealed class ReadinessCheckItem(PreflightCheckResult result, VehicleId vehicle, Action<VehicleId, string> evidence,
    Action<VehicleId, string> configure) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    /// <summary>Updates evidence in place so keyboard focus survives telemetry refreshes.</summary>
    public void Update(PreflightCheckResult next)
    {
        if (result == next)
        {
            return;
        }
        result = next;
        OnPropertyChanged(string.Empty);
    }
    /// <summary>Stable check key.</summary>
    public string Key => result.Key;
    /// <summary>Human-readable check name.</summary>
    public string Title => result.Title;
    /// <summary>Readable outcome with redundant symbol, independent of colour.</summary>
    public string ResultLabel => Label(result.Status);
    /// <summary>Explanation and observed values with units.</summary>
    public string Summary => result.Summary;
    /// <summary>Evidence provenance and precise timestamp.</summary>
    public string Evidence => $"{vehicle} · {result.Evidence.Source} · {result.Evidence.ObservedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "time unknown"}";
    /// <summary>Suggested review only; never a command.</summary>
    public string Remediation => result.Remediation;
    /// <summary>Whether navigation to parameter configuration is applicable.</summary>
    public bool HasConfiguration => result.RelatedParameters.Count > 0 || result.Key is "fc-arming" or "system-health" or "ekf" or "gps";
    /// <summary>Inspect evidence for the originating vehicle, retaining its identity.</summary>
    public ICommand EvidenceCommand { get; } = new RelayCommand(() => evidence(vehicle, result.Key));
    /// <summary>Navigation only; command targeting is unchanged.</summary>
    public ICommand ConfigureCommand { get; } = new RelayCommand(() => configure(vehicle, result.Key));
    /// <summary>Maps domain states to operator-facing labels.</summary>
    public static string Label(PreflightCheckStatus status) => status switch
    {
        PreflightCheckStatus.Pass => "✓ Passed",
        PreflightCheckStatus.Fail => "✖ Failed",
        PreflightCheckStatus.Warning => "⚠ Warning",
        PreflightCheckStatus.NotApplicable => "— Not applicable",
        PreflightCheckStatus.Stale => "? Unknown — stale evidence",
        _ => "? Unknown"
    };
}
