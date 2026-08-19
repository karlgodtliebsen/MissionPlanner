using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Defines and evaluates the deterministic Optional Hardware tab catalog.</summary>
public sealed class OptionalHardwareTabCatalog
{
    /// <summary>Gets descriptors in permanent UI order.</summary>
    public IReadOnlyList<OptionalHardwareTabDescriptor> Tabs { get; } = CreateTabs();

    /// <summary>Evaluates all tabs without changing index order.</summary>
    public IReadOnlyList<OptionalHardwareTabState> Evaluate(bool online, FirmwareFamily? family, IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return Tabs.Select(tab => Evaluate(tab, online, family, parameters)).ToArray();
    }

    private static OptionalHardwareTabState Evaluate(OptionalHardwareTabDescriptor tab, bool online, FirmwareFamily? family, IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return !online && tab.RequiresVehicle
            ? new OptionalHardwareTabState(tab, false, "Connect a vehicle to use this tool.")
            : online && tab.FirmwareFamilies is
            {
                Count: > 0
            }
                         families
                     &&
                     (family is null || !families.Contains(family.Value))
                ? new OptionalHardwareTabState(tab, false, "Unsupported firmware family.")
                : tab.RequiresParameters &&
                  !tab.ParameterPrefixes.Any(prefix => parameters.Keys.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))
                    ? new OptionalHardwareTabState(tab, false, "Required parameters were not reported.")
                    : new OptionalHardwareTabState(tab, online || tab.SupportsOffline, string.Empty);
    }

    private static IReadOnlyList<OptionalHardwareTabDescriptor> CreateTabs()
    {
        var matrix = new HashSet<FirmwareFamily>
        {
            FirmwareFamily.ArduCopter,
            FirmwareFamily.ArduPlane,
            FirmwareFamily.Rover,
            FirmwareFamily.ArduSub,
            FirmwareFamily.Blimp
        };

        return new[]
        {
            //do not format this list, it is in the order of the tabs in the UI
            TabDescriptor(OptionalHardwareTabKey.RtkGpsInject, "RTK / GPS Inject", 0, false, false, true), TabDescriptor(OptionalHardwareTabKey.SikRadio, "SiK Radio", 1, false, false, true), TabDescriptor(OptionalHardwareTabKey.DroneCan, "DroneCAN / UAVCAN", 2, false, false, true), TabDescriptor(OptionalHardwareTabKey.Joystick, "Joystick", 3, false, false, true), TabDescriptor(OptionalHardwareTabKey.BatteryMonitors, "Battery Monitors", 4, true, true, false, ["BATT_", "BATT2_"]), TabDescriptor(OptionalHardwareTabKey.CanGpsOrder, "CAN GPS Order", 5, true, true, false, ["GPS1_CAN_", "GPS2_CAN_", "GPS_CAN_"]), TabDescriptor(OptionalHardwareTabKey.CompassMotorCalibration, "Compass / Motor Calibration", 6, true, false, false, families: matrix), TabDescriptor(OptionalHardwareTabKey.Rangefinder, "Rangefinder", 7, true, true, false, ["RNGFND"]), TabDescriptor(OptionalHardwareTabKey.Airspeed, "Airspeed", 8, true, true, false, ["ARSPD"]), TabDescriptor(OptionalHardwareTabKey.OpticalFlow, "Optical Flow", 9, true, true, false, ["FLOW_"]), TabDescriptor(OptionalHardwareTabKey.OnboardOsd, "Onboard OSD", 10, true, true, false, ["OSD"]), TabDescriptor(OptionalHardwareTabKey.CameraGimbal, "Camera / Gimbal", 11, true, true, false, ["CAM", "MNT"]), TabDescriptor(OptionalHardwareTabKey.MotorTest, "Motor Test", 12, true, true, false, ["FRAME_", "Q_FRAME_"], matrix), TabDescriptor(OptionalHardwareTabKey.BluetoothSetup, "Bluetooth Setup", 13, false, false, true), TabDescriptor(OptionalHardwareTabKey.Parachute, "Parachute", 14, true, true, false, ["CHUTE_"]), TabDescriptor(OptionalHardwareTabKey.Esp8266Setup, "ESP8266 Setup", 15, true, false, false), TabDescriptor(OptionalHardwareTabKey.CubeIdUpdate, "CubeID Update", 16, true, false, false), TabDescriptor(OptionalHardwareTabKey.AntennaTracker, "Antenna Tracker", 17, true, true, false, ["SERVO", "YAW2SRV_", "PITCH2SRV_"], new HashSet<FirmwareFamily> { FirmwareFamily.AntennaTracker }), TabDescriptor(OptionalHardwareTabKey.FftSetup, "FFT Setup", 18, true, true, false, ["FFT_", "INS_LOG_BAT"])
        };
    }

    private static OptionalHardwareTabDescriptor TabDescriptor(OptionalHardwareTabKey key, string title, int order, bool vehicle, bool parameters, bool offline, IReadOnlyList<string>? prefixes = null, IReadOnlySet<FirmwareFamily>? families = null)
    {
        return new OptionalHardwareTabDescriptor(key, title, $"Configure {title}.", order, vehicle, parameters, offline, prefixes ?? [], families);
    }
}
