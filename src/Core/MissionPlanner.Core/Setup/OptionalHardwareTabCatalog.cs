using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Stable keys for Optional Hardware workspace tabs.</summary>
public enum OptionalHardwareTabKey { RtkGpsInject, SikRadio, DroneCan, Joystick, BatteryMonitors, CanGpsOrder, CompassMotorCalibration, Rangefinder, Airspeed, OpticalFlow, OnboardOsd, CameraGimbal, MotorTest, BluetoothSetup, Parachute, Esp8266Setup, CubeIdUpdate, AntennaTracker, FftSetup }

/// <summary>Describes one stable Optional Hardware tab and its availability signature.</summary>
public sealed record OptionalHardwareTabDescriptor(OptionalHardwareTabKey Key, string Title, string Description, int Order, bool RequiresVehicle, bool RequiresParameters, bool SupportsOffline, IReadOnlyList<string> ParameterPrefixes, IReadOnlySet<FirmwareFamily>? FirmwareFamilies = null);

/// <summary>Availability result for one Optional Hardware tab.</summary>
public sealed record OptionalHardwareTabState(OptionalHardwareTabDescriptor Descriptor, bool IsAvailable, string ReasonUnavailable);

/// <summary>Defines and evaluates the deterministic Optional Hardware tab catalog.</summary>
public sealed class OptionalHardwareTabCatalog
{
    /// <summary>Gets descriptors in permanent UI order.</summary>
    public IReadOnlyList<OptionalHardwareTabDescriptor> Tabs { get; } = CreateTabs();
    /// <summary>Evaluates all tabs without changing index order.</summary>
    public IReadOnlyList<OptionalHardwareTabState> Evaluate(bool online, FirmwareFamily? family, IReadOnlyDictionary<string, VehicleParameter> parameters) => Tabs.Select(tab => Evaluate(tab, online, family, parameters)).ToArray();
    private static OptionalHardwareTabState Evaluate(OptionalHardwareTabDescriptor tab, bool online, FirmwareFamily? family, IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        if (!online && tab.RequiresVehicle) return new(tab, false, "Connect a vehicle to use this tool.");
        if (online && tab.FirmwareFamilies is { Count: > 0 } families && (family is null || !families.Contains(family.Value))) return new(tab, false, "Unsupported firmware family.");
        if (tab.RequiresParameters && !tab.ParameterPrefixes.Any(prefix => parameters.Keys.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))) return new(tab, false, "Required parameters were not reported.");
        return new(tab, online || tab.SupportsOffline, string.Empty);
    }
    private static IReadOnlyList<OptionalHardwareTabDescriptor> CreateTabs()
    {
        var matrix = new HashSet<FirmwareFamily> { FirmwareFamily.ArduCopter, FirmwareFamily.ArduPlane, FirmwareFamily.Rover, FirmwareFamily.ArduSub, FirmwareFamily.Blimp };
        return new[] { D(OptionalHardwareTabKey.RtkGpsInject,"RTK / GPS Inject",0,false,false,true), D(OptionalHardwareTabKey.SikRadio,"SiK Radio",1,false,false,true), D(OptionalHardwareTabKey.DroneCan,"DroneCAN / UAVCAN",2,false,false,true), D(OptionalHardwareTabKey.Joystick,"Joystick",3,false,false,true), D(OptionalHardwareTabKey.BatteryMonitors,"Battery Monitors",4,true,true,false,["BATT_","BATT2_"]), D(OptionalHardwareTabKey.CanGpsOrder,"CAN GPS Order",5,true,true,false,["GPS1_CAN_","GPS2_CAN_","GPS_CAN_"]), D(OptionalHardwareTabKey.CompassMotorCalibration,"Compass / Motor Calibration",6,true,false,false,families:matrix), D(OptionalHardwareTabKey.Rangefinder,"Rangefinder",7,true,true,false,["RNGFND"]), D(OptionalHardwareTabKey.Airspeed,"Airspeed",8,true,true,false,["ARSPD"]), D(OptionalHardwareTabKey.OpticalFlow,"Optical Flow",9,true,true,false,["FLOW_"]), D(OptionalHardwareTabKey.OnboardOsd,"Onboard OSD",10,true,true,false,["OSD"]), D(OptionalHardwareTabKey.CameraGimbal,"Camera / Gimbal",11,true,true,false,["CAM","MNT"]), D(OptionalHardwareTabKey.MotorTest,"Motor Test",12,true,true,false,["FRAME_","Q_FRAME_"],matrix), D(OptionalHardwareTabKey.BluetoothSetup,"Bluetooth Setup",13,false,false,true), D(OptionalHardwareTabKey.Parachute,"Parachute",14,true,true,false,["CHUTE_"]), D(OptionalHardwareTabKey.Esp8266Setup,"ESP8266 Setup",15,true,false,false), D(OptionalHardwareTabKey.CubeIdUpdate,"CubeID Update",16,true,false,false), D(OptionalHardwareTabKey.AntennaTracker,"Antenna Tracker",17,true,false,false), D(OptionalHardwareTabKey.FftSetup,"FFT Setup",18,true,true,false,["FFT_","INS_LOG_BAT"]) };
    }
    private static OptionalHardwareTabDescriptor D(OptionalHardwareTabKey key,string title,int order,bool vehicle,bool parameters,bool offline,IReadOnlyList<string>? prefixes=null,IReadOnlySet<FirmwareFamily>? families=null) => new(key,title,$"Configure {title}.",order,vehicle,parameters,offline,prefixes??[],families);
}
