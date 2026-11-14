namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>The preferred Flight Data attitude presentation.</summary>
public enum AttitudeDisplayMode
{
    /// <summary>The cockpit heads-up display.</summary>
    Hud = 0,
    /// <summary>The illustrative 3D airframe.</summary>
    Frame = 1,
    /// <summary>The HUD and airframe together.</summary>
    Both = 2
}
