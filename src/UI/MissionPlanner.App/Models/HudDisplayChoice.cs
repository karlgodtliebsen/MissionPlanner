using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Models;

/// <summary>A named attitude presentation shared by Flight Data and Preferences.</summary>
public sealed record HudDisplayChoice(string Label, AttitudeDisplayMode Mode)
{
    /// <summary>The available presentations, in menu order.</summary>
    public static IReadOnlyList<HudDisplayChoice> All { get; } =
        [new("HUD", AttitudeDisplayMode.Hud), new("3D frame", AttitudeDisplayMode.Frame), new("Both", AttitudeDisplayMode.Both)];

    /// <summary>Whether to show the cockpit HUD.</summary>
    public bool ShowHud => Mode is AttitudeDisplayMode.Hud or AttitudeDisplayMode.Both;

    /// <summary>Whether to show the illustrative frame.</summary>
    public bool ShowFrame => Mode is AttitudeDisplayMode.Frame or AttitudeDisplayMode.Both;

    /// <summary>Resolves a saved preference to its menu item.</summary>
    public static HudDisplayChoice Resolve(AttitudeDisplayMode mode) => All.FirstOrDefault(choice => choice.Mode == mode) ?? All[2];
}
