using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning;

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
    public bool IsSatisfiedBy(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return AllOf.All(parameters.ContainsKey) && (AnyOf.Count == 0 || AnyOf.Any(parameters.ContainsKey));
    }
}
