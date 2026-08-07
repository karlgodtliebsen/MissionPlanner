using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Contains an immutable preflight assessment for one vehicle snapshot.</summary>
public sealed record PreflightAssessment(VehicleId VehicleId, PreflightCheckStatus OverallStatus, DateTimeOffset AssessedAt, IReadOnlyList<PreflightCheckResult> Checks);
