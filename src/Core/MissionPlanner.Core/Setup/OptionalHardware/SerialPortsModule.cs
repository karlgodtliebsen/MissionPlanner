using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Discovers all reported serial settings with metadata-based receiver diagnostics.</summary>
public sealed class SerialPortsModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "serial";

    /// <inheritdoc />
    public string Title => "Serial Ports";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) =>
        SerialPortConfiguration.Discover(parameters.Keys).Count > 0;

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var names = SerialPortConfiguration.Discover(parameters.Keys).SelectMany(port => port.Names).ToArray();
        var settings = names.Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata))
            .OfType<PeripheralSetting>().ToArray();
        var fields = names.Append("RC_OPTIONS").Where(parameters.ContainsKey).Select(name =>
        {
            metadata.TryGetValue(name, out var definition);
            var value = parameters[name];
            var projected = ParameterFieldMetadata.Empty with
            {
                Options = definition?.GetValueOptions().Select(pair => new ParameterValueOption(pair.Key, pair.Value)).ToArray() ?? [],
                Bitmask = definition?.GetBitmaskOptions().Select(pair => new ParameterBitOption(pair.Key, pair.Value)).ToArray() ?? []
            };
            return new ParameterEditField(name, value.Type, value.Value, value.Value, value.Value, projected, null);
        });
        var warning = SerialPortConfiguration.Diagnose(fields);
        return new OptionalHardwareModuleView(Key, Title, "Configure serial parameters; changes may require a flight-controller reboot.",
            settings, string.IsNullOrEmpty(warning) ? [] : [new PeripheralValidationIssue(PeripheralIssueSeverity.Warning, warning)], null);
    }
}
