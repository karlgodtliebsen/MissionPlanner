using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Represents a complete local or vehicle fence plan.</summary>
/// <param name="ReturnPoint">The optional legacy fence return point.</param>
/// <param name="Areas">The polygon and circle areas.</param>
public sealed record FencePlan(GeoPosition? ReturnPoint, IReadOnlyList<FenceArea> Areas)
{
    /// <summary>Gets an empty fence plan.</summary>
    public static FencePlan Empty { get; } = new(null, []);
}
