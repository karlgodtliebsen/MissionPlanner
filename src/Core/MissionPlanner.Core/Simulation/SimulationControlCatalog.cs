using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides controls backed only by documented ArduPilot SITL parameters.</summary>
public sealed class SimulationControlCatalog : ISimulationControlCatalog
{
    private static readonly IReadOnlySet<FirmwareFamily> allSitlFamilies = new HashSet<FirmwareFamily> { FirmwareFamily.ArduCopter, FirmwareFamily.ArduPlane, FirmwareFamily.Rover, FirmwareFamily.ArduSub };

    private static readonly Uri simulationParametersDocumentation =
        new("https://ardupilot.org/dev/docs/SITL_simulation_parameters.html");

    private static readonly Uri sourceParameterDocumentation =
        new("https://github.com/ArduPilot/ardupilot/blob/master/libraries/SITL/SITL.cpp");

    /// <inheritdoc />
    public IReadOnlyList<SimulationControlDescriptor> Controls { get; } =
    [
        Value("wind-speed", "Wind speed", "Horizontal simulated wind speed.", "m/s", 0, 100, "SIM_WIND_SPD"),
        Value("wind-direction", "Wind direction", "True direction the simulated wind comes from.", "deg", 0, 360, "SIM_WIND_DIR"),
        Value("wind-turbulence", "Wind turbulence", "Random simulated wind variation.", "m/s", 0, 100, "SIM_WIND_TURB"),
        Fault(
            "gps-failure",
            "GPS signal loss",
            "Disables the primary simulated GPS for a bounded interval.",
            [new SimulationParameterBinding("SIM_GPS1_ENABLE", 0, 1), new SimulationParameterBinding("SIM_GPS_DISABLE", 1, 0)]),
        Fault(
            "compass-failure",
            "Compass 1 failure",
            "Injects the documented primary simulated compass failure.",
            [new SimulationParameterBinding("SIM_MAG1_FAIL", 1, 0)]),
        Fault(
            "rc-failure",
            "RC signal loss",
            "Simulates complete loss of RC input; RC failsafe behavior remains firmware-configured.",
            [new SimulationParameterBinding("SIM_RC_FAIL", 1, 0)]),
        new SimulationControlDescriptor(
            "battery-voltage",
            "Battery voltage",
            "Overrides simulated resting battery voltage temporarily and may trigger configured battery failsafes.",
            SimulationControlCategory.Fault,
            "V",
            0,
            100,
            true,
            TimeSpan.FromMinutes(5),
            [new SimulationParameterBinding("SIM_BATT_VOLTAGE")],
            allSitlFamilies,
            sourceParameterDocumentation),
        new SimulationControlDescriptor(
            "rangefinder-failure",
            "Rangefinder failure",
            "No bounded general-purpose rangefinder failure parameter is documented; availability remains explicit.",
            SimulationControlCategory.Sensor,
            string.Empty,
            0,
            1,
            true,
            TimeSpan.FromSeconds(30),
            [],
            allSitlFamilies,
            new Uri("https://ardupilot.org/dev/docs/adding_simulated_devices.html"))
    ];

    /// <inheritdoc />
    public IReadOnlyList<SimulationLocationPreset> Locations { get; } =
    [
        new("canberra-cmac", "Canberra — CMAC", new SimulationLocation(-35.363261, 149.165230, 584, 353)),
        new("copenhagen", "Copenhagen", new SimulationLocation(55.6761, 12.5683, 5, 0)),
        new("zero", "Equator / prime meridian", new SimulationLocation(0, 0, 0, 0))
    ];

    private static SimulationControlDescriptor Value(
        string key,
        string name,
        string description,
        string unit,
        double minimum,
        double maximum,
        string parameterName) =>
        new(
            key,
            name,
            description,
            SimulationControlCategory.Environment,
            unit,
            minimum,
            maximum,
            false,
            null,
            [new SimulationParameterBinding(parameterName)],
            allSitlFamilies,
            simulationParametersDocumentation);

    private static SimulationControlDescriptor Fault(
        string key,
        string name,
        string description,
        IReadOnlyList<SimulationParameterBinding> parameters) =>
        new(
            key,
            name,
            description,
            SimulationControlCategory.Fault,
            string.Empty,
            0,
            1,
            true,
            TimeSpan.FromSeconds(60),
            parameters,
            allSitlFamilies,
            simulationParametersDocumentation);
}
