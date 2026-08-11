namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Describes whether a mission-map command can execute and why it is unavailable.</summary>
/// <param name="IsEnabled">Whether the command can execute.</param>
/// <param name="Reason">User-facing reason when disabled.</param>
public sealed record MissionMapCommandAvailability(bool IsEnabled, string? Reason = null)
{
    /// <summary>Gets the enabled availability value.</summary>
    public static MissionMapCommandAvailability Enabled { get; } = new(true);
    /// <summary>Creates a disabled value with an explanatory reason.</summary>
    public static MissionMapCommandAvailability Disabled(string reason) => new(false, reason);
}
