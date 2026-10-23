namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Stable keys for Optional Hardware workspace tabs.</summary>
public enum OptionalHardwareTabKey
{
    /// <summary>RTK and GPS correction injection.</summary>
    RtkGpsInject,
    /// <summary>SiK telemetry radio configuration.</summary>
    SikRadio,
    /// <summary>DroneCAN and UAVCAN node tools.</summary>
    DroneCan,
    /// <summary>Joystick setup.</summary>
    Joystick,
    /// <summary>Battery monitor parameters.</summary>
    BatteryMonitors,
    /// <summary>CAN GPS ordering.</summary>
    CanGpsOrder,
    /// <summary>Compass and motor calibration.</summary>
    CompassMotorCalibration,
    /// <summary>Rangefinder parameters.</summary>
    Rangefinder,
    /// <summary>Airspeed sensor parameters.</summary>
    Airspeed,
    /// <summary>Optical flow parameters.</summary>
    OpticalFlow,
    /// <summary>Onboard OSD configuration.</summary>
    OnboardOsd,
    /// <summary>Camera and gimbal parameters.</summary>
    CameraGimbal,
    /// <summary>Motor test.</summary>
    MotorTest,
    /// <summary>Bluetooth module setup.</summary>
    BluetoothSetup,
    /// <summary>Parachute parameters.</summary>
    Parachute,
    /// <summary>ESP8266 module setup.</summary>
    Esp8266Setup,
    /// <summary>CubeID firmware update.</summary>
    CubeIdUpdate,
    /// <summary>Antenna tracker setup.</summary>
    AntennaTracker,
    /// <summary>FFT setup.</summary>
    FftSetup,
    /// <summary>Vehicle SERIAL parameter configuration.</summary>
    SerialPorts,
    /// <summary>PX4Flow image stream and lens focusing.</summary>
    PX4Flow
}
