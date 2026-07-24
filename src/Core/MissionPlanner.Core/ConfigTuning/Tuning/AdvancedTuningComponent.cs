using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines one repeated component in an advanced tuning descriptor.</summary>
/// <param name="Key">The stable component key and parameter suffix.</param>
/// <param name="Title">The component title.</param>
/// <param name="Description">The plain-language component explanation.</param>
/// <param name="FallbackUnits">Units used when firmware metadata supplies none.</param>
public sealed record AdvancedTuningComponent(
    string Key,
    string Title,
    string Description,
    string FallbackUnits);
