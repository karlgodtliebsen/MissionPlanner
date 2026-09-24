namespace MissionPlanner.Core.Setup.Reporting;

/// <summary>Subsystems with independent Information and Configuration views.</summary>
public enum SetupReportTopic
{
    /// <summary>Frame reporting.</summary>
    Frame,

    /// <summary>Accelerometer reporting.</summary>
    Accelerometer,

    /// <summary>Radio reporting.</summary>
    Radio,

    /// <summary>Servo output reporting.</summary>
    ServoOutput,

    /// <summary>Serial ports reporting.</summary>
    SerialPorts,

    /// <summary>ESC and motors reporting.</summary>
    EscMotor,

    /// <summary>Flight modes reporting.</summary>
    FlightModes,

    /// <summary>Failsafe reporting.</summary>
    FailSafe,

    /// <summary>Initial tuning reporting.</summary>
    InitialTune,

    /// <summary>Hardware identification reporting.</summary>
    HardwareId,

    /// <summary>Firmware reporting.</summary>
    Firmware,

    /// <summary>Airspeed reporting.</summary>
    Airspeed,

    /// <summary>Battery monitors reporting.</summary>
    Battery,

    /// <summary>Camera and gimbal reporting.</summary>
    CameraGimbal,

    /// <summary>CAN GPS order reporting.</summary>
    CanGpsOrder,

    /// <summary>Compass / motor calibration reporting.</summary>
    CompassMotor,

    /// <summary>DroneCAN / UAVCAN reporting.</summary>
    DroneCan,

    /// <summary>Vehicle identifiers reporting.</summary>
    Naming,

    /// <summary>Onboard OSD reporting.</summary>
    OnboardOsd,

    /// <summary>Optical flow reporting.</summary>
    OpticalFlow,

    /// <summary>Parachute reporting.</summary>
    Parachute,

    /// <summary>Rangefinder reporting.</summary>
    Rangefinder,

    /// <summary>Antenna tracker reporting.</summary>
    AntennaTracker,

    /// <summary>Bluetooth serial module reporting.</summary>
    Bluetooth,

    /// <summary>SiK radio reporting.</summary>
    SikRadio,

    /// <summary>RTK / GPS injection reporting.</summary>
    RtkGps,

    /// <summary>Joystick reporting.</summary>
    Joystick,

    /// <summary>CubeID update reporting.</summary>
    CubeId,

    /// <summary>ESP8266 reporting.</summary>
    Esp8266,

    /// <summary>FFT analysis reporting.</summary>
    Fft,

    /// <summary>Motor test reporting.</summary>
    MotorTest,

    /// <summary>ADS-B reporting.</summary>
    Adsb,

}
