using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Identifies a preflight readiness check category.</summary>
public enum PreflightCheckCategory
{
    /// <summary>Link and heartbeat checks.</summary>
    Connection,
    /// <summary>Firmware and vehicle identity checks.</summary>
    Identity,
    /// <summary>Armed and flight-state checks.</summary>
    Flight,
    /// <summary>Onboard sensor checks.</summary>
    Sensors,
    /// <summary>Position and estimator checks.</summary>
    Navigation,
    /// <summary>Power-system checks.</summary>
    Power,
    /// <summary>Operator-control link checks.</summary>
    Radio,
    /// <summary>Storage, logging, and diagnostic checks.</summary>
    Diagnostics,
}

/// <summary>Describes the outcome of an individual readiness check.</summary>
public enum PreflightCheckStatus
{
    /// <summary>Available evidence meets the rule.</summary>
    Pass,
    /// <summary>Available evidence requires operator attention.</summary>
    Warning,
    /// <summary>Available evidence fails the rule.</summary>
    Fail,
    /// <summary>The latest evidence is too old.</summary>
    Stale,
    /// <summary>The required evidence is unavailable or unsupported.</summary>
    NotAvailable,
}

/// <summary>Provides the observation supporting a readiness result.</summary>
public sealed record PreflightEvidence(string Source, string Value, DateTimeOffset? ObservedAt);

/// <summary>Contains one explainable preflight readiness result.</summary>
public sealed record PreflightCheckResult(
    string Key,
    PreflightCheckCategory Category,
    string Title,
    PreflightCheckStatus Status,
    string Summary,
    PreflightEvidence Evidence,
    string Remediation,
    IReadOnlyList<string> RelatedParameters);

/// <summary>Contains an immutable preflight assessment for one vehicle snapshot.</summary>
public sealed record PreflightAssessment(
    VehicleId VehicleId,
    PreflightCheckStatus OverallStatus,
    DateTimeOffset AssessedAt,
    IReadOnlyList<PreflightCheckResult> Checks);

/// <summary>Describes an acknowledged pre-arm request and captured diagnostics.</summary>
public sealed record PreflightCommandResult(
    VehicleCommandResponse? Response,
    IReadOnlyList<VehicleStatusText> Diagnostics,
    string Summary);
