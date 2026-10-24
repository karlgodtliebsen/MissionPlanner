using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects motor protocol and endpoint settings separately from actuator execution.</summary>
public sealed class EscMotorModule : IOptionalHardwareModule
{
    private static readonly string[] names = ["MOT_PWM_TYPE", "MOT_PWM_MIN", "MOT_PWM_MAX", "MOT_SPIN_ARM", "MOT_SPIN_MIN", "MOT_SPIN_MAX"];

    /// <inheritdoc />
    public string Key => "esc-motor";
    /// <inheritdoc />
    public string Title => "ESC and motor output";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => names.Any(parameters.ContainsKey);

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = names.Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray();
        return new(Key, Title, "Review the reported output protocol and endpoints. Digital ESC protocols do not require endpoint calibration. Protocol changes may require a reboot.", settings, [], null);
    }
}
