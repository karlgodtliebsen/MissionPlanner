using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Configuration;

/// <summary>Defines an explicit presence rule for a configuration field.</summary>
/// <param name="AllOf">Parameter names that must all be present.</param>
/// <param name="AnyOf">Parameter names of which at least one must be present; empty means no any-of requirement.</param>
public sealed record ParameterPresenceRule(IReadOnlyList<string> AllOf, IReadOnlyList<string> AnyOf)
{
    /// <summary>Gets a rule that imposes no additional presence requirements.</summary>
    public static ParameterPresenceRule Always { get; } = new([], []);

    /// <summary>Determines whether the available parameter set satisfies this rule.</summary>
    /// <param name="parameters">The available parameters.</param>
    /// <returns><see langword="true"/> when every explicit requirement is satisfied.</returns>
    public bool IsSatisfiedBy(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        AllOf.All(parameters.ContainsKey) && (AnyOf.Count == 0 || AnyOf.Any(parameters.ContainsKey));
}
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
    public static ParameterFieldDefinition Exact(string name) => new(name, [name]);

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
