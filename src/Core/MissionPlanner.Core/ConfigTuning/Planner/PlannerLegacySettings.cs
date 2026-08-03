namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures established MissionPlanner operational preferences.</summary>
public sealed record PlannerLegacySettings
{
    public string LayoutMode { get; init; } = "Advanced";
    public string DistanceUnit { get; init; } = "Meters";
    public string AltitudeUnit { get; init; } = "Meters";
    public string SpeedUnit { get; init; } = "MetersPerSecond";
    public bool SpeechEnabled { get; init; }
    public string SpeechSeverity { get; init; } = "Warning";
    public int AttitudeRateHz { get; init; } = 4;
    public int PositionRateHz { get; init; } = 2;
    public int StatusRateHz { get; init; } = 2;
    public int RcRateHz { get; init; } = 2;
    public int SensorRateHz { get; init; } = 2;
    public bool ResetOnUsbConnect { get; init; }
    public bool DisableEsp32RtsReset { get; init; } = true;
    public int TrackLength { get; init; } = 200;
    public bool ShowDistanceToHome { get; init; } = true;
    public bool LoadWaypointsOnConnect { get; init; }
    public bool RotateMapToHeading { get; init; }
    public byte GcsSystemId { get; init; } = byte.MaxValue;
    public bool DisplayCourseOverGround { get; init; } = true;
    public bool DisplayHeading { get; init; } = true;
    public bool DisplayNavigationBearing { get; init; } = true;
    public bool DisplayTurnRadius { get; init; } = true;
    public bool DisplayTarget { get; init; } = true;
    public bool DisplayAircraftToolTip { get; init; }
    public int AircraftLineLength { get; init; } = 500;
    public bool ShowAirports { get; init; } = true;
    public bool ShowAdsb { get; init; }
    public bool ShowNoFlyZones { get; init; } = true;
    public bool ShowTemporaryFlightRestrictions { get; init; } = true;
    public bool DownloadParametersInBackground { get; init; } = true;
    public bool NoRcReceiver { get; init; }
    public bool SlowComputerMode { get; init; }
    public string MapAccessMode { get; init; } = "ServerAndCache";
}
