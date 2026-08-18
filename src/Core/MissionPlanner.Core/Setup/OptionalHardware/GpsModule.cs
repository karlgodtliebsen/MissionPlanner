using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

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
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return parameters.ContainsKey("GPS_TYPE") || parameters.ContainsKey("GPS1_TYPE");
    }

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
