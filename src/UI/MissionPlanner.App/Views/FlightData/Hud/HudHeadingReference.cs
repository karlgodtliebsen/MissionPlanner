namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>Display-only yaw reference for a top-view arrow; horizon roll/pitch are not transformed.</summary>
public sealed class HudHeadingReference
{
    /// <summary>Captured yaw, or null for unadjusted heading.</summary>
    public double? Offset { get; private set; }
    /// <summary>Captures finite yaw only when fresh.</summary>
    public void Reset(double yaw, bool fresh)
    {
        if (fresh && double.IsFinite(yaw))
        {
            Offset = yaw;
        }
    }
    /// <summary>Restores unadjusted heading.</summary>
    public void Restore() => Offset = null;
    /// <summary>Projects clockwise yaw in degrees with wraparound.</summary>
    public double Project(double yaw) => ((yaw - (Offset ?? 0)) % 360 + 360) % 360;
}
