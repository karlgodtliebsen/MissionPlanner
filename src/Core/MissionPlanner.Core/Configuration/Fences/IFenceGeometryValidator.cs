namespace MissionPlanner.Core.Configuration.Fences;

/// <summary>Validates fence geometry and related parameter relationships before upload.</summary>
public interface IFenceGeometryValidator
{
    /// <summary>Validates the plan's geometry and protocol limits.</summary>
    /// <param name="plan">The plan to validate.</param>
    /// <returns>The validation result.</returns>
    FenceValidationResult Validate(FencePlan plan);

    /// <summary>Validates cross-field fence parameter relationships.</summary>
    /// <param name="session">The shared parameter session containing fence fields.</param>
    /// <returns>The validation result.</returns>
    FenceValidationResult ValidateParameters(IParameterEditSession session);
}
