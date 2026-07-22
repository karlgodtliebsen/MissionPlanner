using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Assesses arming, failsafe, and safety configuration from live parameters and state.</summary>
public interface ISafetyAssessmentService
{
    /// <summary>Builds an evidence-based safety assessment for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The safety assessment.</returns>
    SafetyAssessment BuildAssessment(VehicleId vehicleId);
}
