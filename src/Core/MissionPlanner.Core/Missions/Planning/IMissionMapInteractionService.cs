using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Coordinates mutually exclusive, renderer-independent mission-map interactions.</summary>
public interface IMissionMapInteractionService
{
    /// <summary>Raised when interaction or temporary overlay state changes.</summary>
    event EventHandler? Changed;
    /// <summary>Gets the current interaction state.</summary>
    MissionMapInteractionState State { get; }
    /// <summary>Gets the current planning overlay snapshot.</summary>
    MissionPlanningOverlaySnapshot Overlay { get; }
    /// <summary>Starts an interaction, replacing any current temporary interaction.</summary>
    /// <param name="mode">Mode to enter.</param>
    /// <param name="prompt">User-facing instruction.</param>
    void Enter(MissionMapInteractionMode mode, string prompt);
    /// <summary>Routes a geographic map click to the current interaction.</summary>
    /// <returns><see langword="true"/> when the click was consumed.</returns>
    bool AcceptClick(GeoPosition position);
    /// <summary>Updates the pointer preview for the current interaction.</summary>
    void MovePointer(GeoPosition position);
    /// <summary>Completes the current interaction and retains its completed overlay.</summary>
    void Complete();
    /// <summary>Cancels the current interaction and its temporary overlay.</summary>
    void Cancel();
}
