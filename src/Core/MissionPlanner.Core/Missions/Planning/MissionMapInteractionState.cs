using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Immutable state for the current mission-map interaction.</summary>
/// <param name="Mode">Active interaction mode.</param>
/// <param name="Prompt">User-facing instruction.</param>
/// <param name="Positions">Positions collected by the interaction.</param>
/// <param name="PointerPosition">Latest pointer position, when relevant.</param>
public sealed record MissionMapInteractionState(
    MissionMapInteractionMode Mode,
    string Prompt,
    IReadOnlyList<GeoPosition> Positions,
    GeoPosition? PointerPosition)
{
    /// <summary>Gets the inactive interaction state.</summary>
    public static MissionMapInteractionState None { get; } = new(MissionMapInteractionMode.None, string.Empty, [], null);
}
