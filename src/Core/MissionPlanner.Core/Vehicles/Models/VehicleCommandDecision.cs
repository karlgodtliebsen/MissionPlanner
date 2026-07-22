namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the safety policy decision for a vehicle action.
/// </summary>
/// <param name="IsAllowed">Whether the action may be attempted.</param>
/// <param name="RequiresConfirmation">Whether explicit user confirmation is required.</param>
/// <param name="Reason">The denial or confirmation reason.</param>
public sealed record VehicleCommandDecision(bool IsAllowed, bool RequiresConfirmation, string? Reason)
{
    /// <summary>Creates an allowed decision.</summary>
    /// <param name="requiresConfirmation">Whether confirmation is required.</param>
    /// <param name="reason">The confirmation reason.</param>
    /// <returns>The allowed decision.</returns>
    public static VehicleCommandDecision Allow(bool requiresConfirmation = false, string? reason = null) =>
        new(true, requiresConfirmation, reason);

    /// <summary>Creates a denied decision.</summary>
    /// <param name="reason">The user-facing denial reason.</param>
    /// <returns>The denied decision.</returns>
    public static VehicleCommandDecision Deny(string reason) => new(false, false, reason);
}
