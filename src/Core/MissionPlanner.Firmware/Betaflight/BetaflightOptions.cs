namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Bounded timing policy for optional runtime discovery.</summary>
public sealed class BetaflightOptions
{
    /// <summary>Gets or sets the per-command response deadline.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMilliseconds(400);
    /// <summary>Gets or sets the entire snapshot's probe budget.</summary>
    public TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(8);
    /// <summary>Gets or sets the maximum age of positive and negative discovery evidence.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(30);
}
