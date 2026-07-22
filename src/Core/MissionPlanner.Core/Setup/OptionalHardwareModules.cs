using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Configures serial port protocols and baud rates with conflict detection.</summary>
public sealed class SerialPortsModule : IOptionalHardwareModule
{
    private const int MaximumPorts = 8;
    // MAVLink protocols (1, 2) are legitimately shared; other non-zero protocols are usually exclusive.
    private static readonly HashSet<int> sharedProtocols = [0, 1, 2];

    /// <inheritdoc />
    public string Key => "serial";

    /// <inheritdoc />
    public string Title => "Serial ports";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        Enumerable.Range(0, MaximumPorts + 1).Any(port => parameters.ContainsKey($"SERIAL{port}_PROTOCOL"));

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = new List<PeripheralSetting>();
        var protocolByPort = new Dictionary<int, int>();
        for (var port = 0; port <= MaximumPorts; port++)
        {
            if (PeripheralSettingFactory.TryBuild($"SERIAL{port}_PROTOCOL", parameters, metadata) is { } protocol)
            {
                settings.Add(protocol);
                protocolByPort[port] = (int)Math.Round(protocol.CurrentValue);
            }

            if (PeripheralSettingFactory.TryBuild($"SERIAL{port}_BAUD", parameters, metadata) is { } baud)
            {
                settings.Add(baud);
            }
        }

        var issues = protocolByPort
            .GroupBy(pair => pair.Value)
            .Where(group => !sharedProtocols.Contains(group.Key) && group.Count() > 1)
            .Select(group => new PeripheralValidationIssue(PeripheralIssueSeverity.Warning,
                $"Ports {string.Join(", ", group.Select(pair => pair.Key))} share serial protocol {group.Key}, which is usually exclusive."))
            .ToArray();
        return new OptionalHardwareModuleView(Key, Title, "Assign protocols and baud rates to each serial port.", settings, issues, null);
    }
}

/// <summary>Configures GPS type, ordering, and auto-switching across firmware versions.</summary>
public sealed class GpsModule : IOptionalHardwareModule
{
    private static readonly string[] candidateSettings =
    [
        "GPS_TYPE", "GPS1_TYPE", "GPS_TYPE2", "GPS2_TYPE", "GPS_AUTO_SWITCH",
        "GPS_PRIMARY", "GPS_AUTO_CONFIG", "GPS_RATE_MS", "GPS_INJECT_TO"
    ];

    /// <inheritdoc />
    public string Key => "gps";

    /// <inheritdoc />
    public string Title => "GPS / GNSS";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        parameters.ContainsKey("GPS_TYPE") || parameters.ContainsKey("GPS1_TYPE");

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = candidateSettings
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .Where(setting => setting is not null)
            .Select(setting => setting!)
            .ToArray();
        return new OptionalHardwareModuleView(Key, Title, "Configure GPS type, ordering, and auto-switching.", settings, [], null);
    }
}

/// <summary>Configures sparse rangefinder instances.</summary>
public sealed class RangefinderModule : IOptionalHardwareModule
{
    private const int MaximumInstances = 10;

    /// <inheritdoc />
    public string Key => "rangefinder";

    /// <inheritdoc />
    public string Title => "Rangefinder";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        Enumerable.Range(1, MaximumInstances).Any(instance => parameters.ContainsKey($"RNGFND{instance}_TYPE"));

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = new List<PeripheralSetting>();
        for (var instance = 1; instance <= MaximumInstances; instance++)
        {
            if (!parameters.ContainsKey($"RNGFND{instance}_TYPE"))
            {
                continue;
            }

            foreach (var suffix in new[] { "_TYPE", "_ORIENT", "_MIN_CM", "_MAX_CM" })
            {
                if (PeripheralSettingFactory.TryBuild($"RNGFND{instance}{suffix}", parameters, metadata) is { } setting)
                {
                    settings.Add(setting);
                }
            }
        }

        return new OptionalHardwareModuleView(Key, Title, "Configure sparse rangefinder instances.", settings, [], null);
    }
}

/// <summary>Configures airspeed sensors and their use flags.</summary>
public sealed class AirspeedModule : IOptionalHardwareModule
{
    private static readonly string[] candidateSettings =
    [
        "ARSPD_TYPE", "ARSPD_USE", "ARSPD_PIN", "ARSPD_RATIO", "ARSPD2_TYPE", "ARSPD2_USE"
    ];

    /// <inheritdoc />
    public string Key => "airspeed";

    /// <inheritdoc />
    public string Title => "Airspeed";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => parameters.ContainsKey("ARSPD_TYPE");

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = candidateSettings
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .Where(setting => setting is not null)
            .Select(setting => setting!)
            .ToArray();
        return new OptionalHardwareModuleView(Key, Title, "Configure airspeed sensors and their use flags.", settings, [], null);
    }
}

/// <summary>Exposes CAN driver and protocol visibility and editing.</summary>
public sealed class CanBusModule : IOptionalHardwareModule
{
    private static readonly string[] candidateSettings =
    [
        "CAN_P1_DRIVER", "CAN_P1_BITRATE", "CAN_D1_PROTOCOL", "CAN_P2_DRIVER", "CAN_D2_PROTOCOL"
    ];

    /// <inheritdoc />
    public string Key => "can";

    /// <inheritdoc />
    public string Title => "CAN bus";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => parameters.ContainsKey("CAN_P1_DRIVER");

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = candidateSettings
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .Where(setting => setting is not null)
            .Select(setting => setting!)
            .ToArray();
        return new OptionalHardwareModuleView(Key, Title, "Review CAN drivers and protocols.", settings, [], null);
    }
}
