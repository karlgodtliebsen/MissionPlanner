using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

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
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return parameters.ContainsKey("ARSPD_TYPE");
    }

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
