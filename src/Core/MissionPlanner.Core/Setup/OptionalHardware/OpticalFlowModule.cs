using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects supported legacy and current optical-flow parameters.</summary>
public sealed class OpticalFlowModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "optical-flow";

    /// <inheritdoc />
    public string Title => "Optical Flow";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return parameters.Keys.Any(name => name.StartsWith("FLOW_", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        return BuildModule(Key, Title, parameters, metadata, "FLOW_");
    }

    internal static OptionalHardwareModuleView BuildModule(string key, string title, IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata, string prefix)
    {
        return new OptionalHardwareModuleView(key, title, $"Configure {title} using reported firmware metadata.", parameters.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order().Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray(), [], null);
    }
}
