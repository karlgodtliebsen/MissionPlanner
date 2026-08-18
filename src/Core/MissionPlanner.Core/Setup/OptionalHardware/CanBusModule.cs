using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

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
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return parameters.ContainsKey("CAN_P1_DRIVER");
    }

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
