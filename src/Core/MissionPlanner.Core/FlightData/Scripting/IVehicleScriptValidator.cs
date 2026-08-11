namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Validates an entire script before execution.</summary>
public interface IVehicleScriptValidator
{
    /// <summary>Validates schema, limits, and allow-listed actions.</summary>
    VehicleScriptValidationResult Validate(VehicleScriptDocument document);
}
