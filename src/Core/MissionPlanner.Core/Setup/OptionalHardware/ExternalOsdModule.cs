using System.Text.RegularExpressions;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Discovers reported MAVLink stream-rate groups for an external telemetry OSD.</summary>
public sealed partial class ExternalOsdModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "external-osd";
    /// <inheritdoc />
    public string Title => "External OSD telemetry rates";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => parameters.Keys.Any(name => StreamSetting().IsMatch(name));

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = parameters.Keys.Where(name => StreamSetting().IsMatch(name)).Order(StringComparer.Ordinal)
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray();
        return new(Key, Title, "Choose the MAVLink channel connected to the external OSD. SR channel numbers are not necessarily SERIAL port numbers. Review bandwidth and apply only the selected channel's rates.", settings, [], null);
    }

    [GeneratedRegex(@"^SR[0-9]+_(EXT_STAT|EXTRA[123]|POSITION|RAW_CTRL|RAW_SENS|RC_CHAN)$", RegexOptions.CultureInvariant)]
    private static partial Regex StreamSetting();
}
