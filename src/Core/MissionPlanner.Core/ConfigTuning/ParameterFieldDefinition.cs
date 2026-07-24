using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Defines one logical configuration field and its explicitly ordered firmware aliases.</summary>
/// <param name="Key">The stable logical field key.</param>
/// <param name="ParameterNames">The accepted parameter names in precedence order.</param>
/// <param name="Presence">The additional presence rule, or <see langword="null"/> when none is required.</param>
public sealed record ParameterFieldDefinition(
    string Key,
    IReadOnlyList<string> ParameterNames,
    ParameterPresenceRule? Presence = null)
{
    /// <summary>Creates a definition for one exact parameter name.</summary>
    /// <param name="name">The exact parameter name.</param>
    /// <returns>The exact-name definition.</returns>
    public static ParameterFieldDefinition Exact(string name)
    {
        return new ParameterFieldDefinition(name, [name]);
    }

    /// <summary>Resolves the first explicitly declared alias that is present on the vehicle.</summary>
    /// <param name="parameters">The available live parameter set.</param>
    /// <returns>The resolved parameter name, or <see langword="null"/> when unsupported.</returns>
    public string? Resolve(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(Key) || ParameterNames.Count == 0 ||
            ParameterNames.Any(string.IsNullOrWhiteSpace) || Presence?.IsSatisfiedBy(parameters) == false)
        {
            return null;
        }

        return ParameterNames.FirstOrDefault(parameters.ContainsKey);
    }
}
