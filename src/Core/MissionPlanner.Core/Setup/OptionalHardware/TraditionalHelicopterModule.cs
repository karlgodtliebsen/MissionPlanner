using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects traditional-helicopter swash, collective, rotor and governor parameters reported by the firmware.</summary>
public sealed class TraditionalHelicopterModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "traditional-helicopter";
    /// <inheritdoc />
    public string Title => "Traditional helicopter";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        parameters.ContainsKey("H_SW_TYPE") || parameters.ContainsKey("H_RSC_MODE");
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = parameters.Keys.Where(name => name.StartsWith("H_", StringComparison.Ordinal) ||
            name.StartsWith("H2_", StringComparison.Ordinal) || name.StartsWith("IM_STB_COL_", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .OfType<PeripheralSetting>().ToArray();
        return new(Key, Title, "Disconnect motor power and secure the rotor before setup. Review swash geometry and collective limits, configure rotor speed and governor, then verify servo assignments. Manual servo modes can move servos immediately. Restore normal mode before flight.", settings, [], null);
    }
}
