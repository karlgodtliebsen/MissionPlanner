namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Reports fence geometry and cross-parameter validation.</summary>
/// <param name="Issues">The validation problems.</param>
public sealed record FenceValidationResult(IReadOnlyList<FenceValidationIssue> Issues)
{
    /// <summary>Gets whether no validation problems were found.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Gets a successful validation result.</summary>
    public static FenceValidationResult Valid { get; } = new([]);
}
