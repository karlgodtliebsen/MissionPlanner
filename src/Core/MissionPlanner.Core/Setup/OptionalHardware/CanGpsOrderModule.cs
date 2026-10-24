using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects only GPS/CAN node ordering and override parameters reported by the vehicle.</summary>
public sealed class CanGpsOrderModule : IOptionalHardwareModule
{
    private static readonly string[] names = ["GPS1_CAN_OVRIDE", "GPS2_CAN_OVRIDE", "GPS1_CAN_NODEID", "GPS2_CAN_NODEID", "GPS_CAN_NODEID1", "GPS_CAN_NODEID2"];
    private static bool IsDetectedNode(string name) => name is "GPS1_CAN_NODEID" or "GPS2_CAN_NODEID" or "GPS_CAN_NODEID1" or "GPS_CAN_NODEID2";
    /// <inheritdoc />
    public string Key => "can-gps-order";
    /// <inheritdoc />
    public string Title => "CAN GPS Order";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => names.Any(parameters.ContainsKey);
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var detected = names.Where(IsDetectedNode)
            .Where(parameters.ContainsKey).Select(name => parameters[name].Value)
            .Where(value => float.IsFinite(value) && value is > 0 and <= 127 && value == MathF.Truncate(value))
            .Distinct().Order().ToArray();
        var options = new[] { new PeripheralSettingOption(0, "Automatic assignment") }
            .Concat(detected.Select(value => new PeripheralSettingOption((int)value, $"Detected CAN GPS node {(int)value}"))).ToArray();
        var settings = names.Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>()
            .Select(setting => IsDetectedNode(setting.Name)
                ? setting with { ReadOnly = true, Description = "Node reported by the flight controller; use the override fields to assign receivers." }
                : setting with { Options = options, Minimum = 0, Maximum = 127, RebootRequired = true }).ToArray();
        return new(Key, Title, "Choose a detected GPS node or automatic assignment. A stored but undetected override is preserved for review. Clear one override before swapping assignments, apply the new assignments, then reboot and verify GPS1/GPS2.", settings, [], null);
    }
}
