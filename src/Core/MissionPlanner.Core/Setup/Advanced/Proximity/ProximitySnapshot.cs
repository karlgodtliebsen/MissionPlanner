namespace MissionPlanner.Core.Setup.Advanced.Proximity;

/// <summary>Distinguishes usable obstacles from sensor and freshness limitations.</summary>
public enum ProximitySampleState
{
    /// <summary>A usable in-range obstacle.</summary>
    Valid,
    /// <summary>No usable measurement is supplied.</summary>
    Unknown,
    /// <summary>No obstacle within the reported range.</summary>
    OutOfRange,
    /// <summary>The reading is below the measurable minimum.</summary>
    TooClose,
    /// <summary>The observation exceeded the configured age limit.</summary>
    Stale,
    /// <summary>The measurement cannot be projected into the horizontal body plane.</summary>
    Unsupported
}

/// <summary>One source-preserving point with clockwise bearing from vehicle forward in degrees.</summary>
public sealed record ProximityPoint(string Source, int Index, string SensorType, string Orientation,
    double? BearingDegrees, double? DistanceMeters, double MinimumMeters, double MaximumMeters,
    double AngularWidthDegrees, byte? Covariance, byte? Quality, DateTimeOffset ObservedAt,
    double AgeSeconds, ProximitySampleState State);

/// <summary>Bounded normalized data for radar and diagnostic rendering.</summary>
public sealed record ProximitySnapshot(IReadOnlyList<ProximityPoint> Points, ProximityPoint? Nearest,
    long Malformed, long Unsupported, long Dropped)
{
    /// <summary>Gets an empty initial snapshot.</summary>
    public static ProximitySnapshot Empty { get; } = new([], null, 0, 0, 0);
}

/// <summary>Configures proximity freshness without changing vehicle telemetry rates.</summary>
public sealed class ProximityOptions
{
    /// <summary>Gets or sets the maximum accepted observation age.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(2);
}
