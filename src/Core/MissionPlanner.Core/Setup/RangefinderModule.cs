using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Configures sparse rangefinder instances.</summary>
public sealed class RangefinderModule : IOptionalHardwareModule
{
    private const int MaximumInstances = 10;

    /// <inheritdoc />
    public string Key => "rangefinder";

    /// <inheritdoc />
    public string Title => "Rangefinder";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return Enumerable.Range(1, MaximumInstances).Any(instance => parameters.ContainsKey($"RNGFND{instance}_TYPE"));
    }

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = new List<PeripheralSetting>();
        for (var instance = 1; instance <= MaximumInstances; instance++)
        {
            if (!parameters.ContainsKey($"RNGFND{instance}_TYPE"))
            {
                continue;
            }

            foreach (var suffix in new[] { "_TYPE", "_ORIENT", "_MIN_CM", "_MAX_CM" })
            {
                if (PeripheralSettingFactory.TryBuild($"RNGFND{instance}{suffix}", parameters, metadata) is { } setting)
                {
                    settings.Add(setting);
                }
            }
        }

        return new OptionalHardwareModuleView(Key, Title, "Configure sparse rangefinder instances.", settings, [], null);
    }
}
