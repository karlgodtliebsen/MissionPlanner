namespace MissionPlanner.Core.Vehicles;

/// <summary>Configurable monotonic liveness and notification thresholds.</summary>
public sealed class VehicleConnectionHealthOptions
{
    /// <summary>Valid packet silence before the connection is degraded.</summary>
    public TimeSpan DegradedAfter { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>Valid packet silence before the connection is terminated.</summary>
    public TimeSpan DisconnectedAfter { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Initial period suppressing repeated no-data notifications, never domain state.</summary>
    public TimeSpan NotificationGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Minimum interval between armed no-data notifications.</summary>
    public TimeSpan WarningRepeatInterval { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Low-frequency liveness evaluation interval.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Rejects invalid timing configurations.</summary>
    public void Validate()
    {
        if (DegradedAfter <= TimeSpan.Zero || DisconnectedAfter <= DegradedAfter ||
            NotificationGracePeriod < TimeSpan.Zero || WarningRepeatInterval <= TimeSpan.Zero || PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DegradedAfter), "Connection health thresholds must be positive and ordered.");
        }
    }
}
