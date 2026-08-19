using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects the camera and mount parameter families reported by the active firmware.</summary>
public sealed class CameraGimbalModule : IOptionalHardwareModule
{
    private static readonly string[] prefixes = ["CAM", "MNT", "MOUNT", "GMBL"];

    /// <inheritdoc />
    public string Key => "camera-gimbal";

    /// <inheritdoc />
    public string Title => "Camera / Gimbal";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        parameters.Keys.Any(IsCameraOrGimbalParameter);

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = parameters.Keys
            .Where(IsCameraOrGimbalParameter)
            .Order(StringComparer.Ordinal)
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .OfType<PeripheralSetting>()
            .ToArray();

        return new OptionalHardwareModuleView(
            Key,
            Title,
            "Configure the camera and gimbal parameters reported by this firmware. Live operation remains in Payload Control.",
            settings,
            [],
            null);
    }

    private static bool IsCameraOrGimbalParameter(string name) =>
        prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
}
