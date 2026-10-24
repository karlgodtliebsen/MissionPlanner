using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects supported compass learning and calibration-fitness controls.</summary>
public sealed class CompassAdvancedModule : IOptionalHardwareModule
{
    private static readonly string[] names = ["COMPASS_CAL_FIT", "COMPASS_LEARN"];
    /// <inheritdoc />
    public string Key => "compass-advanced";
    /// <inheritdoc />
    public string Title => "Compass advanced settings";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => names.Any(parameters.ContainsKey);
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata) =>
        new(Key, Title, "Review calibration fitness and learning settings against the firmware metadata. A looser acceptance threshold is not proof of a good calibration.",
            names.Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray(), [], null);
}
