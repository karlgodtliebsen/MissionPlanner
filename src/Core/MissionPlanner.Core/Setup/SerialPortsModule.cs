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
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return Enumerable.Range(0, MaximumPorts + 1).Any(port => parameters.ContainsKey($"SERIAL{port}_PROTOCOL"));
    }

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
