namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines one curated field in a firmware-family tuning profile.</summary>
/// <param name="Key">The stable logical field key.</param>
/// <param name="Title">The user-facing field title.</param>
/// <param name="Description">The plain-language tuning explanation.</param>
/// <param name="FallbackUnits">Units used only when firmware metadata supplies none.</param>
/// <param name="Parameter">The exact parameter names and justified aliases.</param>
/// <param name="Warning">An optional control-stability warning.</param>
/// <param name="RecommendedValue">An authoritative recommendation, when one is available.</param>
/// <param name="RecommendationSource">The authoritative recommendation source.</param>
/// <param name="ExpertOnly">Whether the field must be hidden from Basic Tuning.</param>
public sealed record BasicTuningFieldDefinition(
    string Key,
    string Title,
    string Description,
    string FallbackUnits,
    ParameterFieldDefinition Parameter,
    string? Warning = null,
    double? RecommendedValue = null,
    string? RecommendationSource = null,
    bool ExpertOnly = false)
{
    /// <summary>Gets whether a recommendation has both a value and an authoritative source.</summary>
    public bool HasAuthoritativeRecommendation =>
        RecommendedValue is not null && !string.IsNullOrWhiteSpace(RecommendationSource);
}
