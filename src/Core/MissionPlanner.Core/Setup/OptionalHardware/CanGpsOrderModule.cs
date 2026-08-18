using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects only GPS/CAN node ordering and override parameters reported by the vehicle.</summary>
public sealed class CanGpsOrderModule : IOptionalHardwareModule
{
    private static readonly string[] names = ["GPS1_CAN_OVRIDE", "GPS2_CAN_OVRIDE", "GPS_CAN_NODEID1", "GPS_CAN_NODEID2"];
    /// <inheritdoc />
    public string Key => "can-gps-order";
    /// <inheritdoc />
    public string Title => "CAN GPS Order";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => names.Any(parameters.ContainsKey);
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata) =>
        new(Key, Title, "Configure detected CAN GPS node ordering and overrides.", names.Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray(), [], null);
}
