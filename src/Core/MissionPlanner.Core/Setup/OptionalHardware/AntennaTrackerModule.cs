using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects tracker-specific controllers, geometry and servo configuration; the service additionally gates firmware identity.</summary>
public sealed class AntennaTrackerModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "antenna-tracker";
    /// <inheritdoc />
    public string Title => "ArduPilot AntennaTracker";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => parameters.ContainsKey("YAW_RANGE") || parameters.ContainsKey("SERVO_YAW_TYPE");
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata) =>
        new(Key, Title, "Configure a stopped, disarmed tracker. Check yaw/pitch mechanics and servo types before applying changes. Tests enter ServoTest briefly and request Stop afterwards.",
            parameters.Keys.Where(IsSetting).Order(StringComparer.Ordinal).Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray(), [], null);

    /// <summary>Identifies reported tracker controller, range and output settings.</summary>
    public static bool IsSetting(string name) => name.StartsWith("YAW", StringComparison.Ordinal) || name.StartsWith("PITCH", StringComparison.Ordinal) ||
        name.StartsWith("PIDY_", StringComparison.Ordinal) || name.StartsWith("PIDP_", StringComparison.Ordinal) || name.StartsWith("SERVO", StringComparison.Ordinal) ||
        name.StartsWith("RC1_", StringComparison.Ordinal) || name.StartsWith("RC2_", StringComparison.Ordinal) || name.StartsWith("ONOFF_", StringComparison.Ordinal) ||
        name is "ALT_SOURCE" or "AHRS_ORIENTATION" or "START_LATITUDE" or "START_LONGITUDE" or "STARTUP_DELAY" or "SYSID_TARGET" or "MAV_SYSID_TARGET" or "DISTANCE_MIN" or "SCAN_SPEED";
}
