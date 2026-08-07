using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Builds deterministic, explainable readiness assessments from promoted state.</summary>
public interface IPreflightAssessmentService
{
    /// <summary>Assesses the supplied immutable vehicle state.</summary>
    PreflightAssessment Assess(VehicleState state, DateTimeOffset now);
}
