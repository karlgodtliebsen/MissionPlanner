using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Provides shared helpers for building optional-hardware settings from metadata.</summary>
public static class PeripheralSettingFactory
{
    /// <summary>Builds a setting when the parameter is present, applying metadata options and reboot flags.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="parameters">The live parameter set.</param>
    /// <param name="metadata">The firmware parameter metadata.</param>
    /// <param name="isSecret">Whether the value is sensitive.</param>
    /// <returns>The setting, or null when the parameter is absent.</returns>
    public static PeripheralSetting? TryBuild(
        string name,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata,
        bool isSecret = false)
    {
        if (!parameters.TryGetValue(name, out var parameter))
        {
            return null;
        }

        metadata.TryGetValue(name, out var definition);
        var options = definition?.GetValueOptions()
            .OrderBy(option => option.Key)
            .Select(option => new PeripheralSettingOption(option.Key, option.Value))
            .ToArray() ?? [];
        return new PeripheralSetting(
            name,
            definition?.DisplayName ?? name,
            parameter.Value,
            parameter.Type,
            definition?.RebootRequired ?? false,
            options,
            isSecret);
    }
}
