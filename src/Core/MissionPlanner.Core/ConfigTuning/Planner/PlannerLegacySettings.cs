namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures established MissionPlanner operational preferences.</summary>
public sealed record PlannerLegacySettings
{
    /// <summary>Gets the configured application layout mode.</summary>
    public string LayoutMode { get; init; } = "Advanced";
    /// <summary>Gets the preferred distance unit.</summary>
    public string DistanceUnit { get; init; } = "Meters";
    /// <summary>Gets the preferred altitude unit.</summary>
    public string AltitudeUnit { get; init; } = "Meters";
    /// <summary>Gets the preferred speed unit.</summary>
    public string SpeedUnit { get; init; } = "MetersPerSecond";
    /// <summary>Gets whether spoken notifications are enabled.</summary>
    public bool SpeechEnabled { get; init; }
    /// <summary>Gets the minimum severity used for spoken notifications.</summary>
    public string SpeechSeverity { get; init; } = "Warning";
    /// <summary>Gets the requested attitude telemetry rate in hertz.</summary>
    public int AttitudeRateHz { get; init; } = 4;
    /// <summary>Gets the requested position telemetry rate in hertz.</summary>
    public int PositionRateHz { get; init; } = 2;
    /// <summary>Gets the requested status telemetry rate in hertz.</summary>
    public int StatusRateHz { get; init; } = 2;
    /// <summary>Gets the requested RC telemetry rate in hertz.</summary>
    public int RcRateHz { get; init; } = 2;
    /// <summary>Gets the requested sensor telemetry rate in hertz.</summary>
    public int SensorRateHz { get; init; } = 2;
    /// <summary>Gets whether a USB connection resets the device.</summary>
    public bool ResetOnUsbConnect { get; init; }
    /// <summary>Gets whether ESP32 reset through RTS is disabled.</summary>
    public bool DisableEsp32RtsReset { get; init; } = true;
    /// <summary>Gets the maximum displayed vehicle-track length.</summary>
    public int TrackLength { get; init; } = 200;
    /// <summary>Gets whether distance to home is displayed.</summary>
    public bool ShowDistanceToHome { get; init; } = true;
    /// <summary>Gets whether waypoints are loaded when a vehicle connects.</summary>
    public bool LoadWaypointsOnConnect { get; init; }
    /// <summary>Gets whether the map rotates to the vehicle heading.</summary>
    public bool RotateMapToHeading { get; init; }
    /// <summary>Gets the MAVLink system identifier used by the ground station.</summary>
    public byte GcsSystemId { get; init; } = byte.MaxValue;
    /// <summary>Gets whether course over ground is displayed.</summary>
    public bool DisplayCourseOverGround { get; init; } = true;
    /// <summary>Gets whether vehicle heading is displayed.</summary>
    public bool DisplayHeading { get; init; } = true;
    /// <summary>Gets whether navigation bearing is displayed.</summary>
    public bool DisplayNavigationBearing { get; init; } = true;
    /// <summary>Gets whether vehicle turn radius is displayed.</summary>
    public bool DisplayTurnRadius { get; init; } = true;
    /// <summary>Gets whether the current navigation target is displayed.</summary>
    public bool DisplayTarget { get; init; } = true;
    /// <summary>Gets whether the aircraft tooltip is displayed.</summary>
    public bool DisplayAircraftToolTip { get; init; }
    /// <summary>Gets the configured aircraft direction-line length.</summary>
    public int AircraftLineLength { get; init; } = 500;
    /// <summary>Gets whether airports are displayed on the map.</summary>
    public bool ShowAirports { get; init; } = true;
    /// <summary>Gets whether ADS-B traffic is displayed.</summary>
    public bool ShowAdsb { get; init; }
    /// <summary>Gets whether no-fly zones are displayed.</summary>
    public bool ShowNoFlyZones { get; init; } = true;
    /// <summary>Gets whether temporary flight restrictions are displayed.</summary>
    public bool ShowTemporaryFlightRestrictions { get; init; } = true;
    /// <summary>Gets whether parameters are downloaded in the background.</summary>
    public bool DownloadParametersInBackground { get; init; } = true;
    /// <summary>Gets whether the vehicle is configured without an RC receiver.</summary>
    public bool NoRcReceiver { get; init; }
    /// <summary>Gets whether reduced processing for slower computers is enabled.</summary>
    public bool SlowComputerMode { get; init; }
    /// <summary>Gets the configured map-data access mode.</summary>
    public string MapAccessMode { get; init; } = "ServerAndCache";
}
